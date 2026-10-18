using Arena.Core;
using UnityEngine;

namespace Arena
{
    // Arma física (Fase 1). A arma é um Rigidbody preso a um "pulso" invisível (Rigidbody cinemático que segue a mão)
    // por uma ConfigurableJoint com mola. Por isso ela BATE E PARA em inimigos, paredes e chão, a arma pesada atrasa
    // e pende (gravidade), e a rigidez vem da Força (WeaponPhysics no Core).
    // Mede a velocidade da ponta e acerta inimigos por varredura; pode ser arremessada (grip) e chamada de volta (gatilho).
    // Quando a arma fica presa longe da mão aparece uma "mão fantasma" no lugar do controle.
    public sealed class PlayerWeapon : MonoBehaviour
    {
        public WeaponDef Def { get; private set; }
        public bool Held => mode == Mode.Held;
        public bool Thrown => mode == Mode.Thrown;
        public Vector3 BasePoint { get; private set; }
        public Vector3 TipPoint { get; private set; }
        public Vector3 TipVelocity { get; private set; }
        public float TipSpeed { get; private set; }
        public Vector3 BasePrev => basePrev;
        public Vector3 TipPrev => tipPrev;
        public Vector3 BladeDir => (TipPoint - BasePoint).normalized;
        // Distância entre o cabo da arma e a mão (m): quanto a física está segurando a arma longe do controle.
        public float Separation { get; private set; }

        // Inclinação da lâmina em relação ao controle (graus). Ajustar no Quest: negativo = lâmina mais para cima.
        public float VrGripPitch = -40f;

        enum Mode { Held, Thrown, Recalling, Embedded }
        Mode mode = Mode.Held;

        ArenaPlayer player;
        Transform visual;
        Rigidbody body, wrist;
        ConfigurableJoint joint;
        Vector3 basePrev, tipPrev, targetPos;
        Quaternion targetRot = Quaternion.identity;
        float desktopSwingT = -1f, desktopPointT, farTime, hapticCd;
        bool hasPrev, hasTarget;
        // Fase 3: lâmina cravada
        float bleedAcc, thrustT = -1f;
        Vector3 physTipVel;   // velocidade da ponta no último passo de física
        float whooshCd, lastTipSpeed, soundCd;

        // Fase 4: segunda mão
        public bool TwoHanded { get; private set; }
        Rigidbody wrist2;
        ConfigurableJoint joint2;
        float grip2Z, stretchT;
        Vector3 target2Pos;
        public Vector3 SecondGripWorld => visual ? visual.TransformPoint(new Vector3(0f, 0f, grip2Z)) : Vector3.zero;
        public static bool DebugStab;   // registra no console os valores de cada contato (para calibrar)
        Transform staffCrystal, ghost;
        Material ghostMat;

        public Transform Visual => visual;
        public Rigidbody Body => body;
        public Vector3 RigVelocity => rigVel;   // velocidade do corpo do jogador (andar/esquivar)
        public Vector3 PhysTipVelocity => physTipVel;   // velocidade da ponta no passo de física ANTES do contato
        public Vector3 CrystalPoint => staffCrystal ? staffCrystal.position : TipPoint;

        public void Init(ArenaPlayer p)
        {
            player = p;
            wrist = new GameObject("Pulso").AddComponent<Rigidbody>();
            wrist.isKinematic = true;
            wrist.interpolation = RigidbodyInterpolation.Interpolate;
            ghostMat = ArenaUtil.Unlit(new Color(0.75f, 0.85f, 1f, 0f));
            ghost = ArenaUtil.Prim(PrimitiveType.Cube, null, Vector3.zero, new Vector3(0.07f, 0.05f, 0.12f), ghostMat).transform;
            ghost.name = "MaoFantasma";
            ghost.gameObject.SetActive(false);
        }

        public Rarity Rarity { get; private set; }

        public void Equip(WeaponDef def, Rarity rarity = Rarity.Comum)
        {
            if (body) ReleaseStab(false);
            Def = def; Rarity = rarity;
            if (visual) Destroy(visual.gameObject);
            visual = BuildVisual(def, rarity).transform;
            BuildGlove();
            if (!hasTarget) { targetPos = player.RightHand.position; targetRot = player.RightHand.rotation; }
            Attach();
            hasPrev = false;
        }

        // Prende a arma ao pulso: alinha os dois na pose da mão e cria a junta (a rotação-alvo da junta é relativa à pose inicial).
        void Attach()
        {
            UnpinCorpse();
            ReleaseStab(false);
            ReleaseSecondHand();
            mode = Mode.Held;
            body.isKinematic = false;
            body.useGravity = true;
            // A junta grava a rotação relativa a partir dos TRANSFORMS: os dois precisam estar alinhados e sincronizados.
            wrist.transform.SetPositionAndRotation(targetPos, targetRot);
            visual.SetPositionAndRotation(targetPos, targetRot);
            wrist.position = targetPos; wrist.rotation = targetRot;
            body.position = targetPos; body.rotation = targetRot;
            Physics.SyncTransforms();
            body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero;
            if (joint) Destroy(joint);
            joint = visual.gameObject.AddComponent<ConfigurableJoint>();
            joint.connectedBody = wrist;
            joint.autoConfigureConnectedAnchor = false;
            joint.anchor = Vector3.zero;
            joint.connectedAnchor = Vector3.zero;
            joint.linearLimit = new SoftJointLimit { limit = WeaponPhysics.GripLimit };
            joint.linearLimitSpring = new SoftJointLimitSpring { spring = WeaponPhysics.GripLimitSpring, damper = WeaponPhysics.GripLimitDamper };
            SetGripLocked(true);
            joint.angularXMotion = joint.angularYMotion = joint.angularZMotion = ConfigurableJointMotion.Free;
            joint.rotationDriveMode = RotationDriveMode.Slerp;
            joint.enableCollision = false;
            joint.enablePreprocessing = false;   // recomendação da Unity para objetos segurados (evita reações extremas)
            ApplyDrives();
            farTime = 0f;
        }

        bool gripLocked;
        float contactRelax;

        void SetGripLocked(bool locked)
        {
            if (!joint) return;
            gripLocked = locked;
            var m = locked ? ConfigurableJointMotion.Limited : ConfigurableJointMotion.Free;
            joint.xMotion = joint.yMotion = joint.zMotion = m;
        }

        void ApplyDrives()
        {
            if (!joint) return;
            float str = TwoHandRules.EffectiveStrength(player.State.Strength, TwoHanded);
            float m = body.mass;
            float wl = WeaponPhysics.LinearOmega(Def, str), wa = WeaponPhysics.AngularOmega(Def, str);
            float kl = WeaponPhysics.LinearSpringHolding(m, wl);
            var lin = new JointDrive
            {
                positionSpring = kl,
                positionDamper = WeaponPhysics.DamperFor(kl, m),
                maximumForce = WeaponPhysics.MaxForce(Def),
            };
            joint.xDrive = joint.yDrive = joint.zDrive = lin;
            // Inércia em torno do cabo (onde está a mão), não do centro de massa.
            var it = body.inertiaTensor;
            float com = body.centerOfMass.magnitude;
            float inertia = Mathf.Max(it.x, Mathf.Max(it.y, it.z)) + m * com * com;
            float ka = WeaponPhysics.AngularSpringHolding(inertia, wa, m, com);
            joint.slerpDrive = new JointDrive
            {
                // Com as duas mãos a rotação vem da linha entre elas: a mola de rotação da mão principal afrouxa.
                positionSpring = ka * (TwoHanded ? TwoHandRules.MainAngularScale : 1f),
                positionDamper = WeaponPhysics.DamperFor(ka * (TwoHanded ? TwoHandRules.MainAngularScale : 1f), inertia),
                maximumForce = float.MaxValue,
            };
        }

        GameObject BuildVisual(WeaponDef def, Rarity rarity)
        {
            var root = new GameObject(def.Name);
            staffCrystal = WeaponModels.Build(root.transform, def, rarity);
            foreach (var b in WeaponModels.Colliders(def)) AddBox(root, b.Center, b.Size);
            Setup(root, WeaponPhysics.Mass(def), WeaponModels.CenterOfMass(def));
            return root;
        }

        // Mão que segura a arma (VR): fica no cabo, junto com a arma. Se a arma para numa parede, a mão para junto;
        // o controle de verdade aparece como a mão fantasma translúcida.
        Transform glove;
        static Material gloveMat;

        void BuildGlove()
        {
            if (gloveMat == null) gloveMat = ArenaUtil.Lit(new Color(0.24f, 0.16f, 0.1f));
            glove = new GameObject("Luva").transform;
            glove.SetParent(visual, false);
            glove.localPosition = new Vector3(0f, 0f, 0.02f);
            ArenaUtil.Prim(PrimitiveType.Cube, glove, new Vector3(0f, -0.012f, 0f), new Vector3(0.085f, 0.06f, 0.095f), gloveMat);       // palma e dedos fechados no cabo
            ArenaUtil.Prim(PrimitiveType.Cube, glove, new Vector3(0f, 0.03f, 0.005f), new Vector3(0.08f, 0.03f, 0.085f), gloveMat);      // costas da mão
            ArenaUtil.Prim(PrimitiveType.Capsule, glove, new Vector3(-0.045f, 0.01f, -0.01f), new Vector3(0.026f, 0.03f, 0.026f), gloveMat); // polegar
            ArenaUtil.Prim(PrimitiveType.Cylinder, glove, new Vector3(0f, -0.005f, -0.075f), new Vector3(0.07f, 0.03f, 0.07f), gloveMat)
                .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);                                                                  // punho
            foreach (var col in glove.GetComponentsInChildren<Collider>()) Destroy(col);
            glove.gameObject.SetActive(false);
        }

        static PhysicsMaterial weaponMat;
        static void AddBox(GameObject go, Vector3 center, Vector3 size)
        {
            if (weaponMat == null)
                weaponMat = new PhysicsMaterial("Arma") { bounciness = 0.05f, dynamicFriction = 0.35f, staticFriction = 0.4f, bounceCombine = PhysicsMaterialCombine.Minimum };
            var c = go.AddComponent<BoxCollider>();
            c.center = center; c.size = size; c.sharedMaterial = weaponMat;
        }

        void Setup(GameObject go, float mass, Vector3 centerOfMass)
        {
            body = go.AddComponent<Rigidbody>();
            body.mass = mass;
            body.centerOfMass = centerOfMass;
            body.interpolation = RigidbodyInterpolation.None;   // a física roda na frequência da tela (ArenaGame)
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;   // lâmina fina e rápida não atravessa
            body.maxAngularVelocity = 50f;
            body.solverIterations = 12;
            body.solverVelocityIterations = 4;
            go.AddComponent<WeaponImpactRelay>().Init(this);
        }

        // ---------- Fase 4: segunda mão ----------
        // A mão esquerda segura o cabo num ponto (grip2Z): uma segunda mola de posição prende esse ponto à mão.
        // A rotação da arma passa a vir da linha entre as duas mãos, e a Força conta mais (TwoHandRules).
        public bool TryGrabSecondHand(Vector3 handWorld)
        {
            if (mode != Mode.Held || TwoHanded || Def == null) return false;
            float z = TwoHandRules.ClampToGrip(Def.Kind, visual.InverseTransformPoint(handWorld).z);
            var gripWorld = visual.TransformPoint(new Vector3(0f, 0f, z));
            if (Vector3.Distance(handWorld, gripWorld) > TwoHandRules.GrabRadius) return false;

            grip2Z = z;
            if (!wrist2)
            {
                wrist2 = new GameObject("Pulso2").AddComponent<Rigidbody>();
                wrist2.isKinematic = true;
                wrist2.interpolation = RigidbodyInterpolation.Interpolate;
            }
            wrist2.transform.position = gripWorld;   // começa no próprio cabo: a arma não dá tranco ao pegar
            wrist2.position = gripWorld;
            target2Pos = handWorld;
            Physics.SyncTransforms();
            joint2 = visual.gameObject.AddComponent<ConfigurableJoint>();
            joint2.connectedBody = wrist2;
            joint2.autoConfigureConnectedAnchor = false;
            joint2.anchor = new Vector3(0f, 0f, z);
            joint2.connectedAnchor = Vector3.zero;
            joint2.xMotion = joint2.yMotion = joint2.zMotion = ConfigurableJointMotion.Free;
            joint2.angularXMotion = joint2.angularYMotion = joint2.angularZMotion = ConfigurableJointMotion.Free;
            joint2.enablePreprocessing = false;
            TwoHanded = true;
            stretchT = 0f;
            ApplyDrives();
            ArenaInput.Haptic(0.4f, 0.05f, left: true);
            return true;
        }

        public void SetSecondHandTarget(Vector3 handWorld) => target2Pos = handWorld;

        public void ReleaseSecondHand()
        {
            if (joint2) Destroy(joint2);
            joint2 = null;
            if (!TwoHanded) return;
            TwoHanded = false;
            ApplyDrives();
        }

        // Desktop: ponto do cabo onde a "mão esquerda" seguraria na pose atual (para testar sem VR).
        public Vector3 DesktopSecondHandPoint()
        {
            var (min, max) = TwoHandRules.GripRange(Def.Kind);
            float z = Def.Kind == WeaponKind.Espada ? min : Mathf.Lerp(min, max, 0.6f);
            return targetPos + targetRot * new Vector3(0f, 0f, z);
        }

        // Desktop: o mesmo ponto, mas no cabo como ele está agora (a arma pesada pende; é ali que a mão pega).
        public Vector3 DesktopSecondHandGrabPoint()
        {
            var (min, max) = TwoHandRules.GripRange(Def.Kind);
            float z = Def.Kind == WeaponKind.Espada ? min : Mathf.Lerp(min, max, 0.6f);
            return visual.TransformPoint(new Vector3(0f, 0f, z));
        }

        void UpdateSecondHandStretch(float dt)
        {
            if (!TwoHanded) return;
            // Mãos afastadas além do que o cabo permite: a mão esquerda solta.
            if (Vector3.Distance(target2Pos, SecondGripWorld) > TwoHandRules.MaxStretch) stretchT += dt; else stretchT = 0f;
            if (stretchT > TwoHandRules.StretchSeconds) ReleaseSecondHand();
            if (joint2)
            {
                float m = body.mass, w = WeaponPhysics.LinearOmega(Def, TwoHandRules.EffectiveStrength(player.State.Strength, true));
                float k = WeaponPhysics.LinearSpringHolding(m, w);
                joint2.xDrive = joint2.yDrive = joint2.zDrive = new JointDrive
                {
                    positionSpring = k, positionDamper = WeaponPhysics.DamperFor(k, m), maximumForce = WeaponPhysics.MaxForce(Def),
                };
            }
        }

        // ---------- Arremessar / chamar de volta ----------
        public bool Recalling => mode == Mode.Recalling;

        // drop = soltou o grip (VR): devagar só cai da mão; rápido arremessa na direção da mão.
        public void Throw(Vector3 velocity, bool drop = false)
        {
            if (!Held) return;
            ReleaseStab(false);
            ReleaseSecondHand();
            mode = Mode.Thrown;
            if (joint) Destroy(joint);
            var v = drop ? (velocity.magnitude < WeaponPhysics.ThrowMinHandSpeed ? velocity : velocity * 1.6f)
                  : velocity.magnitude < 3f ? player.HeadForward * 9f + Vector3.up * 2f : velocity * 1.6f;
            body.linearVelocity = v;
            body.angularVelocity = visual.right * 15f;
        }

        // Soltou o grip enquanto a arma vinha voando: ela cai onde está.
        public void CancelRecall()
        {
            if (mode != Mode.Recalling) return;
            mode = Mode.Thrown;
            body.isKinematic = false;
            body.linearVelocity = Vector3.zero;
        }

        public void Recall()
        {
            if (Held) return;
            if (mode == Mode.Embedded) return;   // cravada: segurar o gatilho para arrancar (UpdateEmbedded)
            mode = Mode.Recalling;
            body.isKinematic = true;
        }

        // ---------- Desktop ----------
        public void DesktopSwing() { if (desktopSwingT < 0f) desktopSwingT = 0f; }
        public void DesktopPoint(float seconds) => desktopPointT = seconds;
        public void DesktopThrust() { if (thrustT < 0f && desktopSwingT < 0f) thrustT = 0f; }
        bool thrustHold;
        public void DesktopThrustHold(bool held) => thrustHold = held;

        void DesktopPose(float dt, out Vector3 pos, out Quaternion rot, bool parry)
        {
            var cam = player.Head;
            Vector3 lp; Vector3 dir;
            if (thrustT >= 0f)
            {
                // Estocada: a lâmina vai reta para frente e volta.
                // 0-30%: aponta a lâmina para frente parada; 30-60%: avança reto; depois volta.
                thrustT += dt / 0.5f;
                float t = Mathf.Min(thrustT, 1f);
                // Enquanto T estiver apertado fica estendida (dá para manter a lâmina cravada).
                if (thrustHold && thrustT > 0.6f) thrustT = 0.6f;
                float e = t < 0.3f ? 0f : t < 0.6f ? Mathf.SmoothStep(0f, 1f, (t - 0.3f) / 0.3f) : 1f - Mathf.SmoothStep(0f, 1f, (t - 0.6f) / 0.4f);
                lp = Vector3.Lerp(new Vector3(0.18f, -0.22f, 0.12f), new Vector3(0.1f, -0.2f, 0.8f), e);
                dir = Vector3.forward;
                if (thrustT >= 1f) thrustT = -1f;
            }
            else if (desktopSwingT >= 0f)
            {
                float dur = 0.28f + Def.Weight * 0.012f;
                desktopSwingT += dt / dur;
                float t = Mathf.Min(desktopSwingT, 1f);
                float e = t * t * (3f - 2f * t);
                lp = Vector3.Lerp(new Vector3(0.38f, -0.05f, 0.3f), new Vector3(-0.25f, -0.4f, 0.35f), e);
                dir = Vector3.Slerp(new Vector3(0.9f, 0.6f, 0.5f), new Vector3(-0.6f, -0.9f, 0.5f), e);
                if (desktopSwingT >= 1.15f) desktopSwingT = -1f;
            }
            else if (parry) { lp = new Vector3(0.25f, -0.3f, 0.5f); dir = new Vector3(-1f, 0.35f, 0.25f); }
            else if (desktopPointT > 0f) { lp = new Vector3(0.2f, -0.25f, 0.45f); dir = Vector3.forward; }
            else { lp = new Vector3(0.28f, -0.32f, 0.35f); dir = new Vector3(0.15f, 0.55f, 1f); }
            desktopPointT -= dt;
            pos = cam.TransformPoint(lp);
            rot = cam.rotation * Quaternion.LookRotation(dir.normalized, Vector3.up);
        }

        // ---------- Loop (Update, chamado pelo ArenaPlayer depois do movimento) ----------
        public void Tick(float dt, bool vr, bool desktopParry)
        {
            if (Def == null || dt <= 0f) return;
            CarryWithRig(dt);
            // Alvo = pose da mão (VR) ou pose animada (desktop). A física persegue esse alvo no FixedUpdate.
            if (vr) { targetPos = player.RightHand.position; targetRot = player.RightHand.rotation * Quaternion.Euler(VrGripPitch, 0, 0); }
            else DesktopPose(dt, out targetPos, out targetRot, desktopParry);
            hasTarget = true;

            if (mode == Mode.Recalling)
            {
                var to = targetPos - visual.position;
                visual.position += to.normalized * Mathf.Min(to.magnitude, 14f * dt);
                visual.Rotate(Vector3.right, 720f * dt, Space.Self);
                body.position = visual.position; body.rotation = visual.rotation;
                if (to.magnitude < 0.25f) { Attach(); ArenaInput.Haptic(0.6f, 0.08f); }
            }

            UpdateSecondHandStretch(dt);
            Separation = mode == Mode.Held ? Vector3.Distance(visual.position, targetPos) : 0f;
            if (glove) glove.gameObject.SetActive(vr && mode == Mode.Held);
            UpdateGhost();

            basePrev = BasePoint; tipPrev = TipPoint;
            BasePoint = visual.TransformPoint(new Vector3(0, 0, 0.12f));
            TipPoint = visual.TransformPoint(new Vector3(0, 0, Def.Length));
            strikeAPrev = strikeA; strikeBPrev = strikeB;
            var strike = WeaponStrike.For(Def);
            strikeA = visual.TransformPoint(strike.A.ToU()); strikeB = visual.TransformPoint(strike.B.ToU());
            if (!hasPrev) { basePrev = BasePoint; tipPrev = TipPoint; strikeAPrev = strikeA; strikeBPrev = strikeB; hasPrev = true; }
            TipVelocity = (TipPoint - tipPrev) / dt;
            TipSpeed = TipVelocity.magnitude;
            // Som do golpe cortando o ar.
            whooshCd -= dt;
            if (mode == Mode.Held && TipSpeed > 4.5f && lastTipSpeed <= 4.5f && whooshCd <= 0f)
            {
                whooshCd = 0.25f;
                ArenaAudio.Play(Sfx.Whoosh, TipPoint, Mathf.Clamp01(TipSpeed / 10f), Def.Weight > 5f ? 0.7f : 1.1f);
            }
            lastTipSpeed = TipSpeed;

            if (pinnedCorpse || pinJoint) UpdatePin();
            if (Stuck) UpdateStab(dt);
            else if (TipSpeed > Combat.MinSwingSpeed) CheckHits();
            hapticCd -= dt;
            soundCd -= dt;
        }

        // Quando o jogador anda, esquiva ou gira, a arma vai junto com o corpo (antes ela "corria atrás" da mão e tremia).
        // A mola só cuida do movimento da mão em relação ao corpo. Também move os pontos do quadro anterior, então
        // a velocidade da ponta passa a ser relativa ao jogador: andar com a espada parada não conta como golpe.
        Vector3 rigPrevPos, rigVel;
        Vector3 strikeA, strikeB, strikeAPrev, strikeBPrev;   // parte que corta (lâmina da espada, gume do machado)
        Quaternion rigPrevRot = Quaternion.identity;
        bool hasRigPrev;

        void CarryWithRig(float dt)
        {
            var rig = player.rigRoot;
            if (hasRigPrev)
            {
                var dRot = rig.rotation * Quaternion.Inverse(rigPrevRot);
                Vector3 Map(Vector3 p) => rig.position + dRot * (p - rigPrevPos);
                rigVel = (rig.position - rigPrevPos) / dt;
                bool moved = (rig.position - rigPrevPos).sqrMagnitude > 1e-10f || Quaternion.Angle(rig.rotation, rigPrevRot) > 0.01f;
                if (moved && mode == Mode.Held)
                {
                    var p = Map(body.position); var r = dRot * body.rotation;
                    body.position = p; body.rotation = r;
                    visual.SetPositionAndRotation(p, r);
                    body.linearVelocity = dRot * body.linearVelocity;
                    wrist.position = Map(wrist.position); wrist.rotation = dRot * wrist.rotation;
                    if (wrist2) wrist2.position = Map(wrist2.position);
                }
                if (moved)
                {
                    BasePoint = Map(BasePoint); TipPoint = Map(TipPoint);
                    strikeA = Map(strikeA); strikeB = Map(strikeB);
                }
            }
            rigPrevPos = rig.position; rigPrevRot = rig.rotation; hasRigPrev = true;
        }

        void FixedUpdate()
        {
            if (Def == null || !hasTarget) return;
            // Velocidade da ponta relativa ao jogador (andar ou esquivar com a espada parada não crava).
            if (body && !body.isKinematic) physTipVel = body.GetPointVelocity(visual.TransformPoint(new Vector3(0, 0, Def.Length))) - rigVel;
            wrist.MovePosition(targetPos);
            wrist.MoveRotation(targetRot);
            if (TwoHanded && wrist2) wrist2.MovePosition(target2Pos);
            if (mode != Mode.Held) return;
            ApplyDrives();   // a Força muda ao subir de nível
            // Encostada em algo: solta o limite (para no obstáculo). Livre no ar: cabo travado na mão.
            contactRelax -= Time.fixedDeltaTime;
            bool wantLocked = contactRelax <= 0f;
            if (wantLocked != gripLocked) SetGripLocked(wantLocked);
            // Inimigo espetado pesa na lâmina e freia o movimento ao longo dela (sensação da lâmina entrando).
            if (skewered)
            {
                var at = visual.TransformPoint(new Vector3(0f, 0f, Def.Length - skewerCenterFromTip));
                body.AddForceAtPosition(Vector3.down * skewered.Def.Mass * StabRules.SkewerWeightKgPerMass * 9.81f, at);
                var axis = visual.forward;
                body.AddForce(-axis * Vector3.Dot(body.linearVelocity - rigVel, axis) * StabRules.SlideDrag, ForceMode.Acceleration);
            }
            // Presa longe da mão por muito tempo: volta para a mão — mas NUNCA atravessando parede ou chão (a mão fica
            // travada na arma do outro lado; ao voltar a mão, a mola traz a arma). Controle sem rastreamento também não conta.
            bool tracked = !player.IsVr || OVRInput.GetControllerPositionTracked(OVRInput.Controller.RTouch);
            if (tracked && Vector3.Distance(body.position, targetPos) > WeaponPhysics.SnapDistance && !WallBetween(targetPos, body.position))
            {
                farTime += Time.fixedDeltaTime;
                if (farTime > WeaponPhysics.SnapSeconds) Attach();
            }
            else farTime = 0f;
        }

        static readonly RaycastHit[] wallHits = new RaycastHit[8];
        bool WallBetween(Vector3 a, Vector3 b)
        {
            var d = b - a;
            int n = Physics.RaycastNonAlloc(a, d.normalized, wallHits, d.magnitude, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = wallHits[i].collider;
                if (c.attachedRigidbody == body || c.GetComponentInParent<ArenaEnemy>()) continue;
                return true;
            }
            return false;
        }

        // Mão fantasma: um contorno translúcido no lugar do controle quando a arma está presa longe dele.
        void UpdateGhost()
        {
            bool show = player.IsVr && mode == Mode.Held && Separation > WeaponPhysics.GhostHandDistance;
            ghost.gameObject.SetActive(show);
            if (!show) return;
            ghost.SetPositionAndRotation(player.RightHand.position, player.RightHand.rotation);
            float a = Mathf.InverseLerp(WeaponPhysics.GhostHandDistance, 0.35f, Separation);
            ghostMat.color = new Color(0.75f, 0.85f, 1f, Mathf.Lerp(0.15f, 0.5f, a));
        }

        // Chamado pelo WeaponImpactRelay: vibração proporcional ao impacto.
        public void OnImpact(Collision c)
        {
            contactRelax = WeaponPhysics.ContactRelaxSeconds;
            var enemy = c.collider.GetComponentInParent<ArenaEnemy>();
            // Fase 3: estocada com a ponta crava a lâmina.
            if (enemy && TryStab(enemy, c)) return;
            // Fase 2: o inimigo atingido reage ao impulso (empurrão, inclinação, cambaleio).
            if (enemy && mode != Mode.Recalling)
            {
                var dir = TipVelocity.sqrMagnitude > 0.25f ? TipVelocity
                        : enemy.transform.position - (c.contactCount > 0 ? c.GetContact(0).point : visual.position);
                enemy.PhysicalImpact(c.impulse.magnitude, dir, c.contactCount > 0 ? enemy.HeightFraction(c.GetContact(0).point) : 0.5f);
            }
            // Arma batendo em parede/chão: som metálico proporcional ao impacto.
            if (!enemy && mode != Mode.Recalling && soundCd <= 0f && c.contactCount > 0 && !c.collider.GetComponent<RagdollBone>())
            {
                float s = WeaponPhysics.HapticFromImpulse(c.impulse.magnitude, Def);
                if (s > 0.15f) { soundCd = 0.12f; ArenaAudio.Play(Sfx.Block, c.GetContact(0).point, s * 0.7f, Def.Weight > 5f ? 0.6f : 1.2f); }
            }
            if (mode != Mode.Held || hapticCd > 0f || !player.IsVr) return;
            float amp = WeaponPhysics.HapticFromImpulse(c.impulse.magnitude, Def);
            if (amp < 0.08f) return;
            hapticCd = 0.06f;
            ArenaInput.Haptic(amp, 0.04f + 0.08f * amp);
        }

        // ---------- Fase 3: espetar e cravar (refeito depois do teste no Quest) ----------
        // Estocada com a ponta (StabRules):
        //  - Slime/goblin (Skewer): a lâmina atravessa e o inimigo fica ESPETADO nela, pesando na mão. Golpe rápido o
        //    arremessa (bate e leva dano pela velocidade); apontar a espada para baixo faz ele escorregar e cair.
        //  - Chefe/boneco (Embed): a espada fica CRAVADA no monstro e sai da mão; segurar o gatilho para arrancar.
        ArenaEnemy skewered;
        float skewerT, skewerCenterY, skewerHapticCd;
        Vector3 skewerLocal;   // centro do inimigo no espaço da arma (z = ao longo da lâmina)
        Vector3 skewerWorld;   // centro do inimigo no mundo no quadro anterior
        float skewerVp, stabSpeed;   // velocidade de penetração (m/s, diminui com o amortecedor) e da estocada
        bool skewerDeep, skewerThrough;
        float pressureT;
        ArenaEnemy pressureEnemy;

        public bool IsSkewering(ArenaEnemy e) => skewered && skewered == e;
        // Ponto da lâmina dentro do corpo onde o cadáver fica preso (na linha da lâmina, antes da ponta).
        public Vector3 SkewerWorldPoint => visual.TransformPoint(new Vector3(0f, 0f, Mathf.Min(skewerLocal.z, Def.Length - 0.08f)));

        // Pressão (B&S): ponta encostada no inimigo, empurrando devagar ao longo da lâmina, também fura.
        public void OnContactStay(Collision c)
        {
            contactRelax = WeaponPhysics.ContactRelaxSeconds;
            if (mode != Mode.Held || Stuck || c.contactCount == 0) { pressureT = 0f; return; }
            var enemy = c.collider.GetComponentInParent<ArenaEnemy>();
            if (!enemy || enemy.Dead || !enemy.Body || !StabRules.CanPierce(Def.Kind)) { pressureT = 0f; return; }
            var contact = c.GetContact(0).point;
            var bladeDir = visual.forward;
            float fromTip = Vector3.Distance(contact, visual.TransformPoint(new Vector3(0, 0, Def.Length)));
            enemy.BodyAxis(out var a, out var b);
            var ab = b - a;
            var closest = a + ab * Mathf.Clamp01(Vector3.Dot(contact - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
            var into = closest - contact;
            float align = into.sqrMagnitude > 1e-6f ? Vector3.Dot(bladeDir, into.normalized) : 1f;
            float axial = Vector3.Dot(physTipVel, bladeDir);
            if (fromTip <= StabRules.TipZone && axial >= StabRules.PressureMinAxialSpeed && align >= StabRules.PressureMinAlignment)
            {
                if (pressureEnemy != enemy) { pressureEnemy = enemy; pressureT = 0f; }
                pressureT += Time.fixedDeltaTime;
                if (pressureT >= StabRules.PressureHoldSeconds) { pressureT = 0f; TryStab(enemy, c, pressure: true); }
            }
            else pressureT = 0f;
        }

        // ---------- Corpo preso na lâmina (B&S) ----------
        // O inimigo morto espetado vira ragdoll pendurado na espada: uma junta prende uma parte do corpo à lâmina, livre
        // para deslizar AO LONGO dela (com freio) e girar. Sai puxando até passar da ponta ou com uma sacudida forte (a junta quebra).
        RagdollCorpse pinnedCorpse;
        Rigidbody pinBody;
        ConfigurableJoint pinJoint;

        public void PinCorpse(RagdollCorpse corpse, Vector3 point)
        {
            if (!corpse || mode != Mode.Held) return;
            var rb = corpse.NearestBody(point);
            if (!rb) return;
            UnpinCorpse();
            corpse.ScaleMass(StabRules.PinnedCorpseMassScale);
            foreach (var wc in visual.GetComponents<Collider>())
                foreach (var cc in corpse.GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(wc, cc, true);
            var j = rb.gameObject.AddComponent<ConfigurableJoint>();
            j.connectedBody = body;
            j.autoConfigureConnectedAnchor = false;
            // Pose física (o corpo do cadáver é interpolado: o transform fica atrasado durante a física).
            var inv = Quaternion.Inverse(rb.rotation);
            j.anchor = inv * (point - rb.position);
            j.connectedAnchor = Quaternion.Inverse(body.rotation) * (point - body.position);
            j.axis = inv * (body.rotation * Vector3.right);
            j.secondaryAxis = inv * (body.rotation * Vector3.up);
            j.xMotion = j.yMotion = ConfigurableJointMotion.Locked;
            j.zMotion = ConfigurableJointMotion.Free;   // desliza ao longo da lâmina
            j.angularXMotion = j.angularYMotion = j.angularZMotion = ConfigurableJointMotion.Free;
            j.zDrive = new JointDrive { positionSpring = 0f, positionDamper = corpse.TotalMass * StabRules.PinnedSlideDamper, maximumForce = float.MaxValue };
            j.enablePreprocessing = false;
            j.breakForce = corpse.TotalMass * 9.81f * StabRules.PinnedBreakG;
            corpse.Pinned = true;
            pinnedCorpse = corpse; pinBody = rb; pinJoint = j;
        }

        void UpdatePin()
        {
            if (!pinnedCorpse) { pinnedCorpse = null; pinJoint = null; return; }
            if (!pinJoint || !pinBody) { UnpinCorpse(); return; }   // a junta quebrou (sacudida forte)
            var anchorWorld = pinBody.transform.TransformPoint(pinJoint.anchor);
            if (visual.InverseTransformPoint(anchorWorld).z > Def.Length + 0.04f) UnpinCorpse();   // escorregou pela ponta
        }

        void UnpinCorpse()
        {
            if (pinJoint) Destroy(pinJoint);
            if (pinnedCorpse) { pinnedCorpse.Pinned = false; pinnedCorpse.ScaleMass(1f / StabRules.PinnedCorpseMassScale); }
            pinnedCorpse = null; pinJoint = null; pinBody = null;
        }
        float skewerCenterFromTip => Def.Length - skewerLocal.z;
        ArenaEnemy embeddedIn;
        Vector3 embedLocalPos;
        Quaternion embedLocalRot;
        public float PullProgress { get; private set; }   // 0..1 ao arrancar a espada cravada
        public bool Embedded => mode == Mode.Embedded;

        bool TryStab(ArenaEnemy enemy, Collision c, bool pressure = false)
        {
            if (mode != Mode.Held || Stuck || enemy.Dead || !enemy.Body || c.contactCount == 0) return false;
            var contact = c.GetContact(0).point;
            // Velocidade da ponta medida no passo de física ANTES do contato (a do quadro anterior mistura a rotação da arma).
            float speed = physTipVel.magnitude;
            var bladeDir = visual.TransformDirection(Vector3.forward);
            float alignment = speed > 0.01f ? Vector3.Dot(bladeDir, physTipVel / speed) : 0f;
            float fromTip = Vector3.Distance(contact, visual.TransformPoint(new Vector3(0, 0, Def.Length)));
            if (DebugStab) Debug.Log($"[Cravar] vel={speed:0.0} alinh={alignment:0.00} distPonta={fromTip:0.00}");
            if (pressure ? !(StabRules.CanPierce(Def.Kind) && fromTip <= StabRules.TipZone)
                         : !StabRules.CanStab(Def.Kind, speed, alignment, fromTip)) return false;
            if (pressure) speed = Mathf.Max(speed, StabRules.MinSpeed);   // empurrão lento: dano de uma estocada fraca

            ReleaseSecondHand();
            foreach (var col in visual.GetComponents<Collider>()) Physics.IgnoreCollision(col, enemy.BodyCollider, true);
            bool head = WeakPoints.IsHead(enemy.Def.Kind, enemy.HeightFraction(contact));
            bleedAcc = 0f;
            ArenaFx.Burst(contact, new Color(0.7f, 0.12f, 0.16f), 14, 3f);
            ArenaInput.Haptic(1f, 0.12f);

            if (StabRules.StyleFor(enemy.Def.Kind) == StabStyle.Skewer)
            {
                skewered = enemy;
                skewerT = 0f;
                // A velocidade do impacto continua empurrando a lâmina para dentro (pressão: entra devagar).
                skewerVp = pressure ? 0.15f : speed * StabRules.InitialVelocityMultiplier;
                skewerDeep = skewerThrough = false;
                stabSpeed = speed;
                skewerCenterY = enemy.Center.y - enemy.transform.position.y;
                // Começa onde ele está: a lâmina vai entrando conforme você empurra (sem puxar o inimigo para a lâmina).
                skewerLocal = visual.InverseTransformPoint(enemy.Center);
                skewerLocal.z = StabRules.StartZ(skewerLocal.z, enemy.Radius, Def.Length);
                skewerWorld = visual.TransformPoint(skewerLocal);
                enemy.SetImpaled(true);
                if (DebugStab) Debug.Log("[Cravar] ESPETOU " + enemy.Def.Name);
            }
            else
            {
                // Crava e solta da mão: a arma passa a seguir o monstro.
                embeddedIn = enemy;
                mode = Mode.Embedded;
                if (joint) Destroy(joint);
                body.isKinematic = true;
                // Afunda só um pouco (antes pulava 25 cm de uma vez).
                var deeper = visual.position + bladeDir * 0.05f;
                visual.position = deeper; body.position = deeper;
                embedLocalPos = enemy.transform.InverseTransformPoint(visual.position);
                embedLocalRot = Quaternion.Inverse(enemy.transform.rotation) * visual.rotation;
                PullProgress = 0f;
                if (DebugStab) Debug.Log("[Cravar] CRAVOU em " + enemy.Def.Name);
                ArenaGame.Instance?.Message("Espada cravada! Segure o gatilho para arrancar", 1.5f);
            }
            enemy.TakeStab(speed, physTipVel, Def, player.State, head);
            return true;
        }

        public bool Stuck => skewered || embeddedIn;

        void UpdateStab(float dt)
        {
            if (skewered) UpdateSkewer(dt);
            else if (embeddedIn) UpdateEmbedded(dt);
        }

        void Bleed(ArenaEnemy e, float dt)
        {
            bleedAcc += StabRules.BleedDps * dt;
            if (bleedAcc < StabRules.BleedDps * StabRules.BleedTick) return;
            e.ApplyDamage(bleedAcc, 0.3f, null, 0f, new Color(0.85f, 0.2f, 0.2f), canCrit: false);
            bleedAcc = 0f;
        }

        void UpdateSkewer(float dt)
        {
            var e = skewered;
            if (!e || e.Dead) { ReleaseSkewer(Vector3.zero); return; }
            skewerT += dt;
            // Onde o inimigo estaria se ficasse parado no mundo: a lâmina andou por baixo dele.
            // (usa a posição que a própria espada deu a ele no quadro anterior: IA, gravidade ou empurrões não contam)
            var free = visual.InverseTransformPoint(skewerWorld);
            float zPrev = skewerLocal.z;
            skewerLocal.z = StabRules.SlideZ(zPrev, free.z, e.Radius, Def.Length);
            (skewerLocal.z, skewerVp) = StabRules.Penetrate(skewerLocal.z, skewerVp, dt, e.Radius, Def.Length);
            if (!skewerDeep && StabRules.IsDeep(skewerLocal.z, Def.Length))
            {
                skewerDeep = true;
                e.ApplyDamage(StabRules.StabDamage(stabSpeed, Def, true, true) * StabRules.DeepDamageMultiplier, 0.3f, null, 0f, new Color(0.9f, 0.15f, 0.2f), canCrit: false);
                ArenaFx.FloatText("FUNDO!", e.Center + Vector3.up * 0.55f, new Color(1f, 0.4f, 0.4f), 0.9f);
                ArenaInput.Haptic(0.8f, 0.08f);
                if (!skewered) return;
            }
            if (!skewerThrough && StabRules.IsThrough(skewerLocal.z, e.Radius, Def.Length))
            {
                skewerThrough = true;
                ArenaFx.FloatText("ATRAVESSOU!", e.Center + Vector3.up * 0.75f, new Color(1f, 0.3f, 0.3f), 1f);
                ArenaFx.Burst(visual.TransformPoint(new Vector3(0, 0, Def.Length)), new Color(0.7f, 0.12f, 0.16f), 12, 2.5f);
                ArenaInput.Haptic(1f, 0.1f);
            }
            // Para os lados ele vai junto com a lâmina; o desvio do centro some devagar (a lâmina "acha" o meio).
            float decay = Mathf.Clamp01(1f - dt * 1.5f);
            skewerLocal.x *= decay; skewerLocal.y *= decay;
            var feet = visual.TransformPoint(skewerLocal) - Vector3.up * skewerCenterY;
            if (feet.y < 0f) feet.y = 0f;
            e.transform.position = feet;
            skewerWorld = feet + Vector3.up * skewerCenterY;
            // Lâmina deslizando dentro do corpo: vibração proporcional à velocidade do deslize.
            float slide = Mathf.Abs(skewerLocal.z - zPrev) / Mathf.Max(dt, 1e-4f);
            skewerHapticCd -= dt;
            if (slide > 0.2f && skewerHapticCd <= 0f) { skewerHapticCd = 0.05f; ArenaInput.Haptic(Mathf.Clamp01(0.25f + slide * 0.35f), 0.05f); }
            Bleed(e, dt);
            if (!skewered) return;
            // Puxou a espada até a ponta sair do corpo: solta no lugar.
            if (StabRules.PulledOut(skewerLocal.z, e.Radius, Def.Length)) { ReleaseSkewer(Vector3.zero); return; }
            if (skewerT < StabRules.FlingMinTime) return;
            // Sacudida forte PARA O LADO (ignora o movimento ao longo da lâmina): arremessa na direção do golpe.
            var lateral = TipVelocity - BladeDir * Vector3.Dot(TipVelocity, BladeDir);
            if (lateral.magnitude > StabRules.FlingMinSpeed)
            {
                var v = (lateral + rigVel) * StabRules.FlingMultiplier(e.Def.Kind);
                ReleaseSkewer(Vector3.ClampMagnitude(v, 14f));
            }
        }

        void ReleaseSkewer(Vector3 launchVelocity)
        {
            var e = skewered;
            skewered = null;
            if (!e) return;
            if (e.BodyCollider && visual)
                foreach (var col in visual.GetComponents<Collider>()) Physics.IgnoreCollision(col, e.BodyCollider, false);
            e.SetImpaled(false);
            if (!e.Dead)
            {
                e.Launch(launchVelocity);
                if (launchVelocity.magnitude > 3f) ArenaInput.Haptic(0.8f, 0.08f);
                if (launchVelocity.magnitude > 3f) ArenaAudio.Play(Sfx.Whoosh, e.Center, 1f, 0.8f);
            }
        }

        void UpdateEmbedded(float dt)
        {
            var e = embeddedIn;
            if (!e || e.Dead) { FinishEmbed(false); return; }
            var p = e.transform.TransformPoint(embedLocalPos);
            var r = e.transform.rotation * embedLocalRot;
            visual.SetPositionAndRotation(p, r);
            body.position = p; body.rotation = r;
            Bleed(e, dt);
            if (!embeddedIn) return;
            // Segurar o gatilho arranca (mais tempo no chefe). Soltar perde o progresso aos poucos.
            float need = StabRules.PullSeconds(e.Def.Kind);
            if (player.Input.TriggerHeld) PullProgress += dt / need;
            else PullProgress = Mathf.Max(0f, PullProgress - dt * StabRules.PullDecay / need);
            if (PullProgress >= 1f)
            {
                e.ApplyDamage(StabRules.RipDamage, 0.6f, null, 0f, new Color(1f, 0.4f, 0.4f), canCrit: false);
                ArenaFx.FloatText("ARRANCOU", e.Center + Vector3.up * 0.5f, new Color(1f, 0.5f, 0.5f), 1.1f);
                ArenaAudio.Play(Sfx.Rip, TipPoint);
                ArenaFx.Burst(TipPoint, new Color(0.7f, 0.12f, 0.16f), 18, 4f);
                ArenaInput.Haptic(1f, 0.15f);
                FinishEmbed(true);
            }
        }

        void FinishEmbed(bool returnToHand)
        {
            var e = embeddedIn;
            embeddedIn = null;
            PullProgress = 0f;
            if (e && e.BodyCollider && visual)
                foreach (var col in visual.GetComponents<Collider>()) Physics.IgnoreCollision(col, e.BodyCollider, false);
            Attach();   // volta para a mão (se o monstro morreu, também volta: nada de espada perdida)
        }

        void ReleaseStab(bool _)
        {
            if (skewered) ReleaseSkewer(Vector3.zero);
            if (embeddedIn) { embeddedIn = null; PullProgress = 0f; }
        }

        // O inimigo vai sumir (morreu ou foi removido): solta a espada.
        public void OnEnemyRemoved(ArenaEnemy e)
        {
            if (skewered == e) ReleaseSkewer(Vector3.zero);
            if (embeddedIn == e) FinishEmbed(true);
        }


        void CheckHits()
        {
            var strike = WeaponStrike.For(Def);
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            var strikeVel = (strikeB - strikeBPrev) / dt;
            // Qualidade do corte: espada = qualquer fio (perpendicular à lâmina) ou estocada; machado = gume na frente.
            float edge = strike.HasSingleEdge
                ? WeaponStrike.SingleEdgeAlignment(visual.TransformDirection(strike.EdgeDir.ToU()).ToN(), strikeVel.ToN())
                : Geometry.EdgeAlignment(BladeDir.ToN(), TipVelocity.ToN());
            float speed = Mathf.Min(Mathf.Max(TipSpeed, strikeVel.magnitude), Combat.SpeedCap);
            float radius = strike.HasSingleEdge ? 0.06f : 0.1f;
            for (int i = ArenaEnemy.All.Count - 1; i >= 0; i--)
            {
                var e = ArenaEnemy.All[i];
                if (e == null || e.Dead) continue;
                e.BodyAxis(out var a, out var b);
                if (!Geometry.SweptBladeHits(strikeAPrev.ToN(), strikeBPrev.ToN(), strikeA.ToN(), strikeB.ToN(), a.ToN(), b.ToN(), e.Radius + radius))
                    continue;
                // Altura do acerto no corpo: cabeça causa mais dano.
                float height = Geometry.ClosestParamOnSecond(strikeA.ToN(), strikeB.ToN(), a.ToN(), b.ToN());
                // Estocada: a ponta vai na direção da lâmina (vira perfuração).
                float thrust = TipSpeed > 0.01f ? Vector3.Dot(BladeDir, TipVelocity / TipSpeed) : 0f;
                float mult = TwoHandRules.DamageMultiplier(Def, TwoHanded);
                if (Thrown) mult *= DamageTypes.ThrownMultiplier(Def.Kind, DamageTypes.Classify(Def.Kind, edge, thrust));   // adaga arremessada x3
                if (e.TakeMeleeHit(speed, edge, strikeVel, Def, player.State, mult, height, thrust))
                {
                    // Ponto do acerto na lâmina (para o esqueleto saber qual parte foi cortada).
                    float s = Geometry.ClosestParamOnSecond(a.ToN(), b.ToN(), strikeA.ToN(), strikeB.ToN());
                    e.OnMeleeHitAt(Vector3.Lerp(strikeA, strikeB, s), speed, edge, strikeVel, Def);
                }
            }
        }

        void OnDestroy()
        {
            if (visual) Destroy(visual.gameObject);
            if (wrist) Destroy(wrist.gameObject);
            if (wrist2) Destroy(wrist2.gameObject);
            if (ghost) Destroy(ghost.gameObject);
        }
    }

    // Repassa as colisões do corpo da arma para a PlayerWeapon (que vive em outro GameObject).
    public sealed class WeaponImpactRelay : MonoBehaviour
    {
        PlayerWeapon owner;
        public void Init(PlayerWeapon w) => owner = w;
        void OnCollisionEnter(Collision c) { if (owner) owner.OnImpact(c); }
        void OnCollisionStay(Collision c) { if (owner) owner.OnContactStay(c); }
    }
}
