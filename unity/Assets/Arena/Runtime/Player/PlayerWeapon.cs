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

        enum Mode { Held, Thrown, Recalling }
        Mode mode = Mode.Held;

        ArenaPlayer player;
        Transform visual;
        Rigidbody body, wrist;
        ConfigurableJoint joint;
        Vector3 basePrev, tipPrev, targetPos;
        Quaternion targetRot = Quaternion.identity;
        float desktopSwingT = -1f, desktopPointT, farTime, hapticCd;
        bool hasPrev, hasTarget;
        Transform staffCrystal, ghost;
        Material ghostMat;

        public Transform Visual => visual;
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

        public void Equip(WeaponDef def)
        {
            Def = def;
            if (visual) Destroy(visual.gameObject);
            visual = BuildVisual(def).transform;
            if (!hasTarget) { targetPos = player.RightHand.position; targetRot = player.RightHand.rotation; }
            Attach();
            hasPrev = false;
        }

        // Prende a arma ao pulso: alinha os dois na pose da mão e cria a junta (a rotação-alvo da junta é relativa à pose inicial).
        void Attach()
        {
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
            joint.xMotion = joint.yMotion = joint.zMotion = ConfigurableJointMotion.Free;
            joint.angularXMotion = joint.angularYMotion = joint.angularZMotion = ConfigurableJointMotion.Free;
            joint.rotationDriveMode = RotationDriveMode.Slerp;
            joint.enableCollision = false;
            joint.enablePreprocessing = false;   // recomendação da Unity para objetos segurados (evita reações extremas)
            ApplyDrives();
            farTime = 0f;
        }

        void ApplyDrives()
        {
            if (!joint) return;
            float str = player.State.Strength;
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
                positionSpring = ka,
                positionDamper = WeaponPhysics.DamperFor(ka, inertia),
                maximumForce = float.MaxValue,
            };
        }

        GameObject BuildVisual(WeaponDef def)
        {
            var root = new GameObject(def.Name);
            var t = root.transform;
            staffCrystal = null;
            float mass = WeaponPhysics.Mass(def);
            switch (def.Kind)
            {
                case WeaponKind.Espada:
                {
                    var metal = ArenaUtil.Lit(new Color(0.78f, 0.8f, 0.84f), new Color(0.08f, 0.08f, 0.1f));
                    ArenaUtil.Prim(PrimitiveType.Cube, t, new Vector3(0, 0, 0.55f), new Vector3(0.05f, 0.012f, 0.85f), metal);
                    ArenaUtil.Prim(PrimitiveType.Cube, t, new Vector3(0, 0, 1.0f), new Vector3(0.035f, 0.01f, 0.1f), metal);
                    ArenaUtil.Prim(PrimitiveType.Cube, t, new Vector3(0, 0, 0.11f), new Vector3(0.22f, 0.03f, 0.04f), ArenaUtil.Lit(new Color(0.54f, 0.42f, 0.16f)));
                    var h = ArenaUtil.Prim(PrimitiveType.Cylinder, t, Vector3.zero, new Vector3(0.036f, 0.1f, 0.036f), ArenaUtil.Lit(new Color(0.23f, 0.14f, 0.08f))).transform;
                    h.localRotation = Quaternion.Euler(90, 0, 0);
                    AddBox(root, new Vector3(0, 0, 0.6f), new Vector3(0.05f, 0.03f, 0.95f));     // lâmina
                    AddBox(root, new Vector3(0, 0, 0.11f), new Vector3(0.22f, 0.03f, 0.04f));    // guarda
                    Setup(root, mass, new Vector3(0, 0, 0.15f));                                   // equilíbrio perto da guarda
                    break;
                }
                case WeaponKind.Machado:
                {
                    var h = ArenaUtil.Prim(PrimitiveType.Cylinder, t, new Vector3(0, 0, 0.45f), new Vector3(0.05f, 0.575f, 0.05f), ArenaUtil.Lit(new Color(0.35f, 0.23f, 0.12f))).transform;
                    h.localRotation = Quaternion.Euler(90, 0, 0);
                    var steel = ArenaUtil.Lit(new Color(0.6f, 0.63f, 0.67f), new Color(0.06f, 0.06f, 0.07f));
                    ArenaUtil.Prim(PrimitiveType.Cube, t, new Vector3(0, -0.13f, 0.92f), new Vector3(0.06f, 0.38f, 0.26f), steel);
                    ArenaUtil.Prim(PrimitiveType.Cube, t, new Vector3(0, 0.06f, 0.92f), new Vector3(0.05f, 0.1f, 0.1f), steel);
                    AddBox(root, new Vector3(0, 0, 0.5f), new Vector3(0.05f, 0.05f, 0.9f));      // cabo
                    AddBox(root, new Vector3(0, -0.08f, 0.92f), new Vector3(0.06f, 0.48f, 0.26f)); // cabeça
                    Setup(root, mass, new Vector3(0, -0.05f, 0.75f));                              // peso na cabeça: pende
                    break;
                }
                default:
                {
                    var h = ArenaUtil.Prim(PrimitiveType.Cylinder, t, new Vector3(0, 0, 0.45f), new Vector3(0.05f, 0.65f, 0.05f), ArenaUtil.Lit(new Color(0.29f, 0.2f, 0.14f))).transform;
                    h.localRotation = Quaternion.Euler(90, 0, 0);
                    staffCrystal = ArenaUtil.Prim(PrimitiveType.Sphere, t, new Vector3(0, 0, 1.14f), Vector3.one * 0.12f, ArenaUtil.Lit(new Color(0.5f, 0.7f, 1f), new Color(0.3f, 0.5f, 1f))).transform;
                    AddBox(root, new Vector3(0, 0, 0.55f), new Vector3(0.06f, 0.06f, 1.2f));
                    Setup(root, mass, new Vector3(0, 0, 0.4f));
                    break;
                }
            }
            return root;
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
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;   // lâmina fina e rápida não atravessa
            body.maxAngularVelocity = 50f;
            body.solverIterations = 12;
            body.solverVelocityIterations = 4;
            go.AddComponent<WeaponImpactRelay>().Init(this);
        }

        // ---------- Arremessar / chamar de volta ----------
        public void Throw(Vector3 velocity)
        {
            if (!Held) return;
            mode = Mode.Thrown;
            if (joint) Destroy(joint);
            var v = velocity.magnitude < 3f ? player.HeadForward * 9f + Vector3.up * 2f : velocity * 1.6f;
            body.linearVelocity = v;
            body.angularVelocity = visual.right * 15f;
        }

        public void Recall()
        {
            if (Held) return;
            mode = Mode.Recalling;
            body.isKinematic = true;
        }

        // ---------- Desktop ----------
        public void DesktopSwing() { if (desktopSwingT < 0f) desktopSwingT = 0f; }
        public void DesktopPoint(float seconds) => desktopPointT = seconds;

        void DesktopPose(float dt, out Vector3 pos, out Quaternion rot, bool parry)
        {
            var cam = player.Head;
            Vector3 lp; Vector3 dir;
            if (desktopSwingT >= 0f)
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

            Separation = mode == Mode.Held ? Vector3.Distance(visual.position, targetPos) : 0f;
            UpdateGhost();

            basePrev = BasePoint; tipPrev = TipPoint;
            BasePoint = visual.TransformPoint(new Vector3(0, 0, 0.12f));
            TipPoint = visual.TransformPoint(new Vector3(0, 0, Def.Length));
            if (!hasPrev) { basePrev = BasePoint; tipPrev = TipPoint; hasPrev = true; }
            TipVelocity = (TipPoint - tipPrev) / dt;
            TipSpeed = TipVelocity.magnitude;

            if (TipSpeed > Combat.MinSwingSpeed) CheckHits();
            hapticCd -= dt;
        }

        void FixedUpdate()
        {
            if (Def == null || !hasTarget) return;
            wrist.MovePosition(targetPos);
            wrist.MoveRotation(targetRot);
            if (mode != Mode.Held) return;
            ApplyDrives();   // a Força muda ao subir de nível
            // Presa longe da mão por muito tempo (atrás de uma parede, por exemplo): volta para a mão.
            if (Vector3.Distance(body.position, targetPos) > WeaponPhysics.SnapDistance)
            {
                farTime += Time.fixedDeltaTime;
                if (farTime > WeaponPhysics.SnapSeconds) Attach();
            }
            else farTime = 0f;
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
            // Fase 2: o inimigo atingido reage ao impulso (empurrão, inclinação, cambaleio).
            var enemy = c.collider.GetComponentInParent<ArenaEnemy>();
            if (enemy && mode != Mode.Recalling)
            {
                var dir = TipVelocity.sqrMagnitude > 0.25f ? TipVelocity
                        : enemy.transform.position - (c.contactCount > 0 ? c.GetContact(0).point : visual.position);
                enemy.PhysicalImpact(c.impulse.magnitude, dir);
            }
            if (mode != Mode.Held || hapticCd > 0f || !player.IsVr) return;
            float amp = WeaponPhysics.HapticFromImpulse(c.impulse.magnitude, Def);
            if (amp < 0.08f) return;
            hapticCd = 0.06f;
            ArenaInput.Haptic(amp, 0.04f + 0.08f * amp);
        }

        void CheckHits()
        {
            var bladeDir = BladeDir.ToN();
            float edge = Geometry.EdgeAlignment(bladeDir, TipVelocity.ToN());
            for (int i = ArenaEnemy.All.Count - 1; i >= 0; i--)
            {
                var e = ArenaEnemy.All[i];
                if (e == null || e.Dead) continue;
                e.BodyAxis(out var a, out var b);
                if (Geometry.SweptBladeHits(basePrev.ToN(), tipPrev.ToN(), BasePoint.ToN(), TipPoint.ToN(), a.ToN(), b.ToN(), e.Radius + 0.1f))
                    e.TakeMeleeHit(Mathf.Min(TipSpeed, Combat.SpeedCap), edge, TipVelocity, Def, player.State);
            }
        }

        void OnDestroy()
        {
            if (visual) Destroy(visual.gameObject);
            if (wrist) Destroy(wrist.gameObject);
            if (ghost) Destroy(ghost.gameObject);
        }
    }

    // Repassa as colisões do corpo da arma para a PlayerWeapon (que vive em outro GameObject).
    public sealed class WeaponImpactRelay : MonoBehaviour
    {
        PlayerWeapon owner;
        public void Init(PlayerWeapon w) => owner = w;
        void OnCollisionEnter(Collision c) { if (owner) owner.OnImpact(c); }
    }
}
