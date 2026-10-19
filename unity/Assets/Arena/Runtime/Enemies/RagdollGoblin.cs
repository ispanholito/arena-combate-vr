using System.Collections.Generic;
using Arena.Core;
using UnityEngine;

namespace Arena
{
    // Goblin com CORPO FÍSICO (ragdoll ativo) — passo A1 do plano (docs/plano-objetivo-final.md), em teste no Treino.
    // 11 ossos (Rigidbody + ConfigurableJoint). Os "músculos" são as molas das juntas puxando a pose da animação.
    // A bacia fica presa por uma mola forte a um ponto invisível que a IA move (em pé); golpe forte ou ser arrastado
    // solta essa mola (cai) e ele se levanta depois.
    // Interações (como no Blade & Sorcery):
    //  - golpe: a arma empurra o osso de verdade; dano pela velocidade do contato; corte rápido de fio arranca o membro;
    //  - perfurar: estocada com a ponta prende a lâmina no osso por uma junta que só desliza ao longo dela (com freio);
    //    puxar até a ponta sair solta; estocada na cabeça mata e o corpo fica pendurado na lâmina;
    //  - agarrar: a mão prende num ponto da SUPERFÍCIE do osso (a mão fica por fora); levantar/arrastar derruba.
    public sealed class RagdollGoblin : MonoBehaviour
    {
        public static readonly List<RagdollGoblin> All = new List<RagdollGoblin>();

        sealed class Bone
        {
            public RagBone Id;
            public Rigidbody Rb;
            public ConfigurableJoint Joint;   // até o osso pai (null na bacia)
            public Bone Parent;
            public Collider Col;
            public bool Severed;
            public float Mass;
            // Para remontar (esqueleto): pontos da junta no próprio osso e no pai, e a rotação relativa original.
            public Vector3 AnchorLocal, ParentAnchorLocal;
            public Quaternion RelRot;
            public Vector3 JointLocalBuild;   // ponto da junta em coordenadas do corpo (para recriar os limites)
        }

        readonly Dictionary<RagBone, Bone> bones = new Dictionary<RagBone, Bone>();
        readonly List<Bone> boneList = new List<Bone>();
        public EnemyState State { get; private set; }
        Rigidbody anchor;
        ConfigurableJoint anchorJoint;
        float totalMass;
        Vector3 PelvisOffset => new Vector3(0f, 0.55f * k, 0f);   // bacia em relação aos pés (ponto da IA)
        // Passo A4: o mesmo corpo serve para goblin, esqueleto e orc (tamanho, peso, cores, arma e regras em RagdollSpec).
        RagdollSpec spec = new RagdollSpec();
        float k = 1f;
        Vector3 weaponTip;   // ponta da arma no espaço da própria arma
        public EnemyKind Kind => State != null ? State.Def.Kind : EnemyKind.Goblin;

        enum Mode { Stand, Down, Dead }
        Mode mode = Mode.Stand;
        float muscle = 1f, flopT, downT, standT, deadT, hitCd, phase, attackT = -1f, attackCd = 1.5f, bleedAcc;
        enum Attack { None, Windup, Strike, Recover }
        Attack attack;
        bool strikeHit, canAttack = true, canStand = true;
        Material skin, cloth, club;
        // Passo A3: repertório de ataques, fichas, cerco, guarda e clava física (desarmável).
        EnemyAttack curAttack = EnemyAttack.Cima;
        Rigidbody clubRb;
        FixedJoint clubJoint;
        bool Armed => clubJoint;
        float guardT, guardCd, strafeT, retreatT, retreatCheckT;
        Vector3 clubPrevA, clubPrevB;
        bool hasClubPrev;
        int strafeDir = 1;
        bool riposte;
        Vector3 lungeDir;
        int TokenId => GetInstanceID();
        static Difficulty Diff => SaveSystem.Options.Difficulty;

        public bool Dead => mode == Mode.Dead;
        public Vector3 Center => bones[RagBone.Tronco].Rb.position;

        ArenaPlayer Player => ArenaGame.Instance ? ArenaGame.Instance.Player : null;

        // ---------- Criação ----------
        // shared/proxy: na Arena/Dungeon o corpo usa a vida do representante (PhysicalGoblinEnemy).
        public static RagdollGoblin Spawn(Vector3 feet, Vector3 lookAt, EnemyState shared = null, ArenaEnemy proxy = null)
        {
            var go = new GameObject((shared != null ? shared.Def.Name : "Goblin") + " (fisico)");
            go.transform.position = feet;
            var dir = (lookAt - feet); dir.y = 0f;
            go.transform.rotation = Quaternion.LookRotation(dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.forward);
            var g = go.AddComponent<RagdollGoblin>();
            g.shared = shared; g.proxy = proxy;
            g.Build();
            return g;
        }

        public static void ClearAll()
        {
            foreach (var g in All.ToArray()) if (g) Destroy(g.gameObject);
            All.Clear();
        }

        EnemyState shared;
        ArenaEnemy proxy;
        bool Proxied => proxy;

        // Pés: o ponto da IA em pé; caído, embaixo da bacia.
        public Vector3 FeetPosition
        {
            get
            {
                if (mode == Mode.Stand || !bones.TryGetValue(RagBone.Bacia, out var b) || !b.Rb) return anchor ? anchor.position : transform.position;
                var p = b.Rb.position; return new Vector3(p.x, anchor ? anchor.position.y : 0f, p.z);
            }
        }

        void Build()
        {
            State = shared ?? new EnemyState(EnemyDef.Goblin);
            spec = RagdollSpec.For(State.Def.Kind);
            k = spec.Scale;
            skin = ArenaUtil.Lit(new Color(spec.Skin.r, spec.Skin.g, spec.Skin.b));
            cloth = ArenaUtil.Lit(new Color(spec.Cloth.r, spec.Cloth.g, spec.Cloth.b));
            club = ArenaUtil.Lit(spec.Weapon == RagWeapon.Clava ? new Color(0.42f, 0.27f, 0.15f) : new Color(0.62f, 0.64f, 0.68f), spec.Weapon == RagWeapon.Clava ? (Color?)null : new Color(0.08f, 0.08f, 0.1f));
            anchor = gameObject.AddComponent<Rigidbody>();
            anchor.isKinematic = true;

            // Medidas em metros a partir dos pés; x = direita, z = frente.
            AddBox(RagBone.Bacia, null, new Vector3(0, 0.55f, 0), new Vector3(0.30f, 0.16f, 0.18f), 6f, cloth, Vector3.zero);
            AddBox(RagBone.Tronco, RagBone.Bacia, new Vector3(0, 0.80f, 0), new Vector3(0.34f, 0.32f, 0.20f), 8f, cloth, new Vector3(0, 0.63f, 0));
            AddSphere(RagBone.Cabeca, RagBone.Tronco, new Vector3(0, 1.13f, 0), 0.19f, 3f, new Vector3(0, 0.97f, 0));
            foreach (int s in new[] { -1, 1 })
            {
                bool right = s > 0;
                var arm = right ? RagBone.BracoD : RagBone.BracoE;
                var fore = right ? RagBone.AntebracoD : RagBone.AntebracoE;
                AddLimb(arm, RagBone.Tronco, new Vector3(0.23f * s, 0.93f, 0), new Vector3(0.23f * s, 0.69f, 0), 0.055f, 1.5f, skin);
                AddLimb(fore, arm, new Vector3(0.23f * s, 0.69f, 0), new Vector3(0.23f * s, 0.46f, 0), 0.05f, 1.2f, skin);
                var thigh = right ? RagBone.CoxaD : RagBone.CoxaE;
                var shin = right ? RagBone.CanelaD : RagBone.CanelaE;
                AddLimb(thigh, RagBone.Bacia, new Vector3(0.1f * s, 0.5f, 0), new Vector3(0.1f * s, 0.28f, 0), 0.07f, 2.5f, cloth);
                AddLimb(shin, thigh, new Vector3(0.1f * s, 0.28f, 0), new Vector3(0.1f * s, 0.04f, 0), 0.06f, 2f, skin);
            }
            BuildClub();
            // Rosto: olhos (e orelhas no goblin) presos à cabeça.
            var head = bones[RagBone.Cabeca].Rb.transform;
            var eye = ArenaUtil.Lit(new Color(spec.Eye.r, spec.Eye.g, spec.Eye.b), new Color(spec.Eye.r * 0.6f, spec.Eye.g * 0.5f, spec.Eye.b * 0.3f));
            foreach (int s in new[] { -1, 1 })
            {
                ArenaUtil.Prim(PrimitiveType.Sphere, head, new Vector3(s * 0.075f, 0.03f, 0.16f) * k, Vector3.one * 0.06f * k, eye);
                if (spec.Ears)
                    ArenaUtil.Prim(PrimitiveType.Capsule, head, new Vector3(s * 0.22f, 0.05f, 0) * k, new Vector3(0.06f, 0.12f, 0.06f) * k, skin)
                        .transform.localRotation = Quaternion.Euler(0, 0, -s * 65f);
            }
            if (spec.Weapon == RagWeapon.Machado)   // orc: ombreiras
                foreach (int s in new[] { -1, 1 })
                    ArenaUtil.Prim(PrimitiveType.Sphere, bones[RagBone.Tronco].Rb.transform, new Vector3(s * 0.2f, 0.15f, 0) * k, new Vector3(0.18f, 0.1f, 0.18f) * k, cloth);
            // Ossos do mesmo corpo não colidem entre si (evita tremedeira); o resto do mundo sim.
            var cols = BodyColliders();
            for (int i = 0; i < cols.Count; i++) for (int j = i + 1; j < cols.Count; j++) Physics.IgnoreCollision(cols[i], cols[j]);
            foreach (var b in boneList) totalMass += b.Rb.mass;

            // Bacia presa ao ponto da IA (fica em pé).
            anchorJoint = bones[RagBone.Bacia].Rb.gameObject.AddComponent<ConfigurableJoint>();
            anchorJoint.connectedBody = anchor;
            anchorJoint.xMotion = anchorJoint.yMotion = anchorJoint.zMotion = ConfigurableJointMotion.Free;
            anchorJoint.angularXMotion = anchorJoint.angularYMotion = anchorJoint.angularZMotion = ConfigurableJointMotion.Free;
            anchorJoint.rotationDriveMode = RotationDriveMode.Slerp;
            anchorJoint.enablePreprocessing = false;
            SetAnchorStrength(1f);
            All.Add(this);
        }

        // Arma (clava, espada ou machado): corpo físico próprio preso na mão por uma junta fixa. Golpe forte no braço ou nela = solta.
        void BuildClub()
        {
            var fr = bones[RagBone.AntebracoD];
            float L = spec.WeaponLength, r = spec.WeaponRadius;
            var go = new GameObject(spec.Weapon.ToString());
            go.transform.SetPositionAndRotation(transform.TransformPoint(new Vector3(0.23f, 0.46f, 0f) * k + Vector3.forward * (L * 0.5f - 0.03f)), transform.rotation);
            clubRb = go.AddComponent<Rigidbody>();
            clubRb.mass = spec.WeaponMass;
            clubRb.interpolation = RigidbodyInterpolation.Interpolate;
            clubRb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            var col = go.AddComponent<CapsuleCollider>();
            col.direction = 2; col.radius = r; col.height = L;
            weaponTip = new Vector3(0f, 0f, L * 0.5f - 0.01f);
            switch (spec.Weapon)
            {
                case RagWeapon.Espada:
                    ArenaUtil.Prim(PrimitiveType.Cube, go.transform, new Vector3(0, 0, 0.08f), new Vector3(0.02f, 0.06f, L * 0.8f), club);
                    ArenaUtil.Prim(PrimitiveType.Cube, go.transform, new Vector3(0, 0, -L * 0.33f), new Vector3(0.04f, 0.18f, 0.03f), cloth);
                    break;
                case RagWeapon.Machado:
                    ArenaUtil.Prim(PrimitiveType.Cylinder, go.transform, Vector3.zero, new Vector3(0.05f, L * 0.5f, 0.05f), ArenaUtil.Lit(new Color(0.35f, 0.24f, 0.13f)))
                        .transform.localRotation = Quaternion.Euler(90, 0, 0);
                    ArenaUtil.Prim(PrimitiveType.Cube, go.transform, new Vector3(0, -0.08f, L * 0.36f), new Vector3(0.04f, 0.2f, 0.16f), club);
                    break;
                default:
                    ArenaUtil.Prim(PrimitiveType.Capsule, go.transform, Vector3.zero, new Vector3(r * 2f, L * 0.5f, r * 2f), club).transform.localRotation = Quaternion.Euler(90, 0, 0);
                    break;
            }
            go.AddComponent<RagdollBone>().Init(this, RagBone.Clava);
            clubJoint = go.AddComponent<FixedJoint>();
            clubJoint.connectedBody = fr.Rb;
            clubJoint.enablePreprocessing = false;
        }

        // Colisores do corpo e da clava (para ignorar entre si e para a lâmina atravessar quando espeta).
        List<Collider> BodyColliders()
        {
            var cols = new List<Collider>();
            foreach (var b in boneList) if (b.Rb) cols.AddRange(b.Rb.GetComponents<Collider>());
            if (clubRb && Armed) cols.AddRange(clubRb.GetComponents<Collider>());
            return cols;
        }

        void Disarm(Vector3 dir)
        {
            if (!Armed) return;
            Destroy(clubJoint);
            clubJoint = null;
            clubRb.AddForce(dir.normalized * 3f + Vector3.up * 1.5f, ForceMode.VelocityChange);
            attack = Attack.None; guardT = 0f;
            ArenaFx.FloatText("DESARMOU!", clubRb.position + Vector3.up * 0.3f, new Color(1f, 0.85f, 0.4f), 1.1f);
            ArenaAudio.Play(Sfx.Block, clubRb.position, 1f, 0.7f);
            ArenaInput.Haptic(0.8f, 0.1f);
            Destroy(clubRb.gameObject, 12f);
        }

        Bone NewBone(RagBone id, RagBone? parent, Vector3 localCenter, float mass)
        {
            var go = new GameObject("Osso_" + id);
            go.transform.SetPositionAndRotation(transform.TransformPoint(localCenter), transform.rotation);
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.linearDamping = 0.05f; rb.angularDamping = 0.5f;
            var b = new Bone { Id = id, Rb = rb, Mass = mass, Parent = parent.HasValue ? bones[parent.Value] : null };
            go.AddComponent<RagdollBone>().Init(this, id);
            bones[id] = b; boneList.Add(b);
            return b;
        }

        void Connect(Bone b, Vector3 localJoint)
        {
            if (b.Parent == null) return;
            var j = b.Rb.gameObject.AddComponent<ConfigurableJoint>();
            j.connectedBody = b.Parent.Rb;
            j.anchor = b.Rb.transform.InverseTransformPoint(transform.TransformPoint(localJoint));
            j.xMotion = j.yMotion = j.zMotion = ConfigurableJointMotion.Locked;
            j.angularXMotion = j.angularYMotion = j.angularZMotion = ConfigurableJointMotion.Limited;
            var (lo, hi, y, z) = Limits(b.Id);
            j.lowAngularXLimit = new SoftJointLimit { limit = lo };
            j.highAngularXLimit = new SoftJointLimit { limit = hi };
            j.angularYLimit = new SoftJointLimit { limit = y };
            j.angularZLimit = new SoftJointLimit { limit = z };
            j.rotationDriveMode = RotationDriveMode.Slerp;
            j.enablePreprocessing = false;
            b.Joint = j;
            var jw = transform.TransformPoint(localJoint);
            b.AnchorLocal = j.anchor;
            b.ParentAnchorLocal = b.Parent.Rb.transform.InverseTransformPoint(jw);
            b.RelRot = Quaternion.Inverse(b.Parent.Rb.transform.rotation) * b.Rb.transform.rotation;
            b.JointLocalBuild = localJoint;
        }

        // Limites das juntas (graus): X baixo/alto, Y, Z.
        static (float, float, float, float) Limits(RagBone b) => b switch
        {
            RagBone.Tronco => (-35f, 35f, 35f, 25f),
            RagBone.Cabeca => (-40f, 40f, 60f, 30f),
            RagBone.BracoD or RagBone.BracoE => (-170f, 170f, 70f, 80f),
            RagBone.AntebracoD or RagBone.AntebracoE => (-145f, 10f, 20f, 10f),
            RagBone.CoxaD or RagBone.CoxaE => (-95f, 60f, 25f, 35f),
            _ => (-5f, 140f, 10f, 5f),   // canelas: dobram para trás
        };

        void AddBox(RagBone id, RagBone? parent, Vector3 center, Vector3 size, float mass, Material m, Vector3 joint)
        {
            center *= k; size *= k; joint *= k; mass *= spec.MassMultiplier;
            var b = NewBone(id, parent, center, mass);
            b.Rb.gameObject.AddComponent<BoxCollider>().size = size;
            b.Col = b.Rb.GetComponent<Collider>();
            ArenaUtil.Prim(PrimitiveType.Cube, b.Rb.transform, Vector3.zero, size, m);
            Connect(b, joint);
        }

        void AddSphere(RagBone id, RagBone parent, Vector3 center, float r, float mass, Vector3 joint)
        {
            center *= k; r *= k; joint *= k; mass *= spec.MassMultiplier;
            var b = NewBone(id, parent, center, mass);
            b.Rb.gameObject.AddComponent<SphereCollider>().radius = r;
            b.Col = b.Rb.GetComponent<Collider>();
            ArenaUtil.Prim(PrimitiveType.Sphere, b.Rb.transform, Vector3.zero, Vector3.one * r * 2f, skin);
            Connect(b, joint);
        }

        // Membro em cápsula do ponto "de" (junta) até "até".
        void AddLimb(RagBone id, RagBone parent, Vector3 from, Vector3 to, float r, float mass, Material m)
        {
            from *= k; to *= k; r *= k; mass *= spec.MassMultiplier;
            var b = NewBone(id, parent, (from + to) * 0.5f, mass);
            var cap = b.Rb.gameObject.AddComponent<CapsuleCollider>();
            cap.direction = 1; cap.radius = r; cap.height = Vector3.Distance(from, to) + r * 2f;
            b.Col = cap;
            ArenaUtil.Prim(PrimitiveType.Capsule, b.Rb.transform, Vector3.zero, new Vector3(r * 2f, cap.height * 0.5f, r * 2f), m);
            Connect(b, from);
        }

        float anchorStrength = 1f;

        void SetAnchorStrength(float strength)
        {
            float k = strength;   // (aqui k = força da âncora, não a escala)
            anchorStrength = strength;
            if (!anchorJoint) return;
            const float w = 14f;
            float m = spec.AnchorMultiplier;
            float spring = totalMass * w * w * k;
            var lin = new JointDrive { positionSpring = spring, positionDamper = 2f * 0.9f * totalMass * w * Mathf.Sqrt(Mathf.Max(k, 0.05f)), maximumForce = totalMass * 80f * k * m };
            anchorJoint.xDrive = anchorJoint.yDrive = anchorJoint.zDrive = lin;
            anchorJoint.slerpDrive = new JointDrive { positionSpring = 900f * k * m * spec.MassMultiplier, positionDamper = 60f * Mathf.Max(k, 0.05f) * m * spec.MassMultiplier, maximumForce = float.MaxValue };
        }

        // ---------- Pose ("animação" que os músculos seguem) ----------
        readonly Dictionary<RagBone, Vector3> pose = new Dictionary<RagBone, Vector3>();

        void BuildPose(float speed01)
        {
            pose.Clear();
            float swing = Mathf.Sin(phase) * 35f * speed01;
            pose[RagBone.CoxaE] = new Vector3(swing, 0, 0);
            pose[RagBone.CoxaD] = new Vector3(-swing, 0, 0);
            pose[RagBone.CanelaE] = new Vector3(10f + Mathf.Max(0f, -Mathf.Sin(phase)) * 45f * speed01, 0, 0);
            pose[RagBone.CanelaD] = new Vector3(10f + Mathf.Max(0f, Mathf.Sin(phase)) * 45f * speed01, 0, 0);
            pose[RagBone.BracoE] = new Vector3(-swing * 0.6f - 10f, 0, -12f);
            pose[RagBone.AntebracoE] = new Vector3(-25f, 0, 0);
            pose[RagBone.Tronco] = new Vector3(-8f, 0, 0);
            pose[RagBone.Cabeca] = new Vector3(8f, 0, 0);
            // Braço da clava: parado à frente; ataque = pose de aviso (de cima ou de um dos lados) e golpe; guarda = clava na frente.
            (System.Numerics.Vector3 arm, System.Numerics.Vector3 fore) p;
            if (guardT > 0f && attack == Attack.None) p = ActiveRagdollPoses.Guard;
            else switch (attack)
            {
                case Attack.Windup: p = ActiveRagdollPoses.Windup(curAttack); break;
                case Attack.Strike: p = ActiveRagdollPoses.Strike(curAttack); break;
                default: p = (new System.Numerics.Vector3(-30f, 0f, 8f), new System.Numerics.Vector3(-50f, 0f, 0f)); break;
            }
            pose[RagBone.BracoD] = p.arm.ToU();
            pose[RagBone.AntebracoD] = p.fore.ToU();
            if (!Armed && attack == Attack.Windup) { pose[RagBone.BracoD] = new Vector3(-20f, 0f, 20f); pose[RagBone.AntebracoD] = new Vector3(-120f, 0f, 0f); }   // punho recolhido
            if (!Armed && attack == Attack.Strike) { pose[RagBone.BracoD] = new Vector3(-85f, 0f, -5f); pose[RagBone.AntebracoD] = new Vector3(-5f, 0f, 0f); }    // soco reto
        }

        void ApplyMuscles()
        {
            foreach (var b in boneList)
            {
                if (!b.Joint || !b.Rb) continue;
                float m = b.Severed ? 0f : muscle;
                if (b.Id == RagBone.BracoD || b.Id == RagBone.AntebracoD) m *= attack == Attack.Strike ? 2.5f : guardT > 0f ? 2f : 1.3f;   // golpe rápido / guarda firme
                b.Joint.slerpDrive = new JointDrive
                {
                    positionSpring = ActiveRagdollRules.MuscleSpring(b.Mass, m),
                    positionDamper = ActiveRagdollRules.MuscleDamper(b.Mass) * Mathf.Max(0.2f, Mathf.Min(1f, m)),
                    maximumForce = float.MaxValue,
                };
                var e = pose.TryGetValue(b.Id, out var v) ? v : Vector3.zero;
                b.Joint.targetRotation = Quaternion.Inverse(Quaternion.Euler(e));
            }
        }

        // ---------- Loop ----------
        void FixedUpdate()
        {
            if (bones.Count == 0) return;
            float dt = Time.fixedDeltaTime;
            hitCd -= dt; flopT -= dt;
            var pl = Player;
            if (mode != Mode.Dead)
            {
                if (!Proxied) State.Tick(dt);   // com representante, quem avança o tempo da vida/status é ele
                UpdatePieces(dt);
                UpdateStabsBleed(dt);
                if (State.Dead) Die();
            }
            float speed01 = 0f;
            switch (mode)
            {
                case Mode.Stand:
                    muscle = flopT > 0f ? ActiveRagdollRules.MuscleHit : ActiveRagdollRules.MuscleNormal;
                    speed01 = pl ? Think(pl, dt) : 0f;
                    // Arrastado para longe do ponto da IA (agarrado e puxado, empurrado forte): perde o pé.
                    // (agarrado ou espetado perde o pé mais fácil: dá para levantar do chão)
                    float dragOff = GrabbedOrStuck ? ActiveRagdollRules.DragOffDistanceHeld : ActiveRagdollRules.DragOffDistance;
                    if ((bones[RagBone.Bacia].Rb.position - anchor.transform.TransformPoint(PelvisOffset)).magnitude > dragOff) KnockDown();
                    break;
                case Mode.Down:
                    muscle = ActiveRagdollRules.MuscleDown;
                    downT -= dt;
                    if (downT <= 0f && canStand && !GrabbedOrStuck && bones[RagBone.Bacia].Rb.linearVelocity.magnitude < 0.6f) StandUp();
                    break;
                case Mode.Dead:
                    muscle = 0f;
                    if (!GrabbedOrStuck) deadT += dt;
                    if (deadT > 10f) { Destroy(gameObject); return; }
                    break;
            }
            if (mode == Mode.Stand && standT > 0f)
            {
                standT -= dt;
                SetAnchorStrength(1f - Mathf.Clamp01(standT / ActiveRagdollRules.StandUpSeconds));
            }
            else if (mode == Mode.Stand)
            {
                // Espetado ou agarrado: amolece a mola que o segura em pé (dá para arrastar e levantar).
                float want = GrabbedOrStuck ? ActiveRagdollRules.StuckStandStrength : 1f;
                if (!Mathf.Approximately(want, anchorStrength)) SetAnchorStrength(want);
            }
            BuildPose(speed01);
            ApplyMuscles();
        }

        // IA (passo A3): vira para o jogador; com ficha de ataque chega perto e ataca com o repertório (de cima, dos lados,
        // passo à frente; desarmado = soco); sem ficha ronda a ~2 m pelos lados. Às vezes ergue a clava para se defender
        // quando a arma do jogador vem rápida.
        float Think(ArenaPlayer pl, float dt)
        {
            var to = pl.FeetPosition - anchor.position; to.y = 0f;
            float dist = to.magnitude;
            var dir = dist > 1e-3f ? to / dist : transform.forward;
            if (dist > 1e-3f) anchor.MoveRotation(Quaternion.Slerp(anchor.rotation, Quaternion.LookRotation(dir), dt * 5f));
            bool stuck = GrabbedOrStuck || !State.CanAct;   // atordoado, cambaleando, congelado, eletrocutado ou preso
            if (!State.CanAct && attack != Attack.None) { attack = Attack.None; ArenaGame.Tokens.Release(TokenId); }
            var tokens = ArenaGame.Tokens;
            bool hasToken = canAttack && !stuck && tokens.TryTake(TokenId);
            if (!hasToken && attack == Attack.None) tokens.Release(TokenId);
            float reach = Armed ? EnemyAttacks.Range(curAttack) : ActiveRagdollRules.PunchReach;
            float speed = 0f;
            Vector3 move = Vector3.zero;
            // Pouca vida: às vezes recua um pouco.
            retreatT -= dt;
            if (retreatT <= 0f && retreatCheckT <= 0f && attack == Attack.None && State.HpFraction < ActiveRagdollRules.RetreatHpFraction)
            {
                retreatCheckT = 3f;
                if (Random.value < ActiveRagdollRules.RetreatChance) retreatT = ActiveRagdollRules.RetreatSeconds;
            }
            retreatCheckT -= dt;
            if (retreatT > 0f && !stuck) { move = -dir * 1.6f; speed = 1.6f; hasToken = false; tokens.Release(TokenId); }
            else if (attack == Attack.None && !stuck && guardT <= 0f)
            {
                if (hasToken && dist > reach - 0.15f) { move = dir * 1.3f; speed = 1.3f; }
                else if (!hasToken)
                {
                    // Ronda: mantém ~2 m e anda de lado (troca de lado de vez em quando).
                    strafeT -= dt;
                    if (strafeT <= 0f) { strafeT = Random.Range(1.5f, 3f); strafeDir = Random.value < 0.5f ? -1 : 1; }
                    var side = Vector3.Cross(Vector3.up, dir) * strafeDir;
                    float radial = dist - ActiveRagdollRules.CircleRadius;
                    move = side * ActiveRagdollRules.CircleSpeed + dir * Mathf.Clamp(radial, -1f, 1f) * 1.2f;
                    speed = move.magnitude;
                }
            }
            // Passo à frente: avança durante o golpe.
            if (attack == Attack.Strike && curAttack == EnemyAttack.Passo && attackT < 0.2f) move = lungeDir * (ActiveRagdollPoses.StepLunge / 0.2f);
            if (move.sqrMagnitude > 1e-6f)
            {
                anchor.MovePosition(ArenaUtil.ClampToArena(anchor.position + move * dt));
                phase += dt * 7f;
            }
            // Guarda: a arma do jogador vem rápida perto do corpo.
            guardT -= dt; guardCd -= dt;
            var w = pl.Weapon;
            if (Armed && attack == Attack.None && guardCd <= 0f && w && w.Held && w.TipSpeed > ActiveRagdollRules.GuardTriggerSpeed
                && Vector3.Distance(w.TipPoint, bones[RagBone.Cabeca].Rb.position) < ActiveRagdollRules.GuardTriggerDistance)
            {
                guardCd = ActiveRagdollRules.GuardCooldown;
                if (Random.value < ActiveRagdollRules.GuardChance(Diff)) guardT = ActiveRagdollRules.GuardSeconds;
            }
            attackCd -= dt;
            if (attack == Attack.None && hasToken && dist <= reach && attackCd <= 0f && guardT <= 0f)
            {
                curAttack = Armed ? EnemyAttacks.Pick(EnemyKind.Goblin, Diff, Random.value) : EnemyAttack.Cima;
                attack = Attack.Windup; attackT = 0f; strikeHit = false;
            }
            if (attack != Attack.None)
            {
                attackT += dt;
                float windup = (Armed ? 1.0f : 0.6f) * DifficultyRules.WindupMultiplier(Diff) * (riposte ? 0.6f : 1f);
                if (attack == Attack.Windup && attackT > windup)
                {
                    attack = Attack.Strike; attackT = 0f; lungeDir = dir; riposte = false; hasClubPrev = false;
                    ArenaAudio.Play(Sfx.Whoosh, Center, 0.7f, Armed ? 0.8f : 1.3f);
                }
                else if (attack == Attack.Strike)
                {
                    if (!strikeHit) CheckShield(pl);
                    if (!strikeHit && attack == Attack.Strike) CheckClubHit(pl);
                    if (attackT > 0.35f) { attack = Attack.Recover; attackT = 0f; }
                }
                else if (attack == Attack.Recover && attackT > 0.7f)
                {
                    attack = Attack.None;
                    attackCd = CombatAI.Pick(CombatAI.AttackDelay(Diff), Random.value) + 0.6f;
                    tokens.Release(TokenId);   // devolve a vez para outro atacar
                }
            }
            return speed > 0.05f ? 1f : 0f;
        }

        // Escudo do jogador no caminho do golpe (varredura entre passos): aparo, bloqueio, contra-ataque ou guarda quebrada.
        void CheckShield(ArenaPlayer pl)
        {
            if (!bones.TryGetValue(RagBone.AntebracoD, out var fr) || fr.Severed) { hasClubPrev = false; return; }
            var a = fr.Rb.position + fr.Rb.rotation * new Vector3(0f, -0.12f * k, 0f);
            var b = Armed ? clubRb.position + clubRb.rotation * weaponTip : a;
            var shield = pl.Shield;
            if (hasClubPrev && shield && shield.Equipped && shield.Intercepts(clubPrevA, clubPrevB, a, b, 0.05f))
            {
                strikeHit = true;
                ResolveGuard(pl, Guard.ResolveShield(shield.GuardSpeed, pl.State.CounterStance > 0f, pl.State.Stamina.Current), shield.Center);
            }
            clubPrevA = a; clubPrevB = b; hasClubPrev = true;
        }

        void ResolveGuard(ArenaPlayer pl, GuardResult r, Vector3 at)
        {
            var ps = pl.State;
            switch (r)
            {
                case GuardResult.Counter:
                    ps.ConsumeCounterStance();
                    Parried(at);
                    ArenaFx.FloatText("CONTRA-ATAQUE!", at + Vector3.up * 0.4f, new Color(1f, 0.83f, 0.42f), 1.4f);
                    Damage(Combat.CounterDamage * (ps.Has(SkillIds.FioAfiado) ? Combat.SharpEdgeBonus : 1f), Center);
                    ArenaInput.Haptic(1f, 0.2f);
                    pl.Vignette(0.5f);
                    break;
                case GuardResult.Parry:
                    Parried(at);
                    break;
                case GuardResult.ShieldBlock:
                case GuardResult.Block:
                    ps.Stamina.TrySpend(r == GuardResult.Block ? Guard.BlockStaminaCost : Guard.ShieldBlockStaminaCost, 1f);
                    ArenaFx.FloatText("BLOQUEIO", at, new Color(0.85f, 0.85f, 0.85f), 0.9f);
                    ArenaAudio.Play(Sfx.Block, at);
                    ArenaInput.Haptic(0.6f, 0.07f, left: r == GuardResult.ShieldBlock);
                    if (r == GuardResult.Block) pl.TakeDamage(Guard.DamageToPlayer(r, State.Def.AttackDamage), Center, ignoreGuardCooldown: true);
                    attack = Attack.Recover; attackT = 0f;
                    // Revide: depois de ser bloqueado, às vezes ataca de novo logo (aviso mais curto).
                    if (Random.value < CombatAI.RiposteChance(Diff)) { riposte = true; attackCd = CombatAI.Pick(CombatAI.RiposteDelay(Diff), Random.value); }
                    break;
                case GuardResult.GuardBroken:
                    ArenaFx.FloatText("GUARDA QUEBRADA", at, new Color(1f, 0.44f, 0.44f), 1f);
                    pl.TakeDamage(State.Def.AttackDamage, Center, ignoreGuardCooldown: true);
                    break;
            }
        }

        // Aparado: perde o golpe e cambaleia (janela para punir).
        void Parried(Vector3 at)
        {
            attack = Attack.Recover; attackT = 0f; flopT = 0.6f;
            State.ApplyStun(ActiveRagdollRules.ParryStunSeconds);
            ArenaFx.FloatText("APARADO", at + Vector3.up * 0.2f, new Color(0.6f, 0.9f, 1f), 0.9f);
            ArenaAudio.Play(Sfx.Parry, at, 1f);
            ArenaInput.Haptic(0.8f, 0.08f);
            ArenaGame.Tokens.Release(TokenId);
        }

        // A clava acerta se a ponta passar perto do corpo do jogador (cabeça até a cintura).
        void CheckClubHit(ArenaPlayer pl)
        {
            if (!bones.TryGetValue(RagBone.AntebracoD, out var fr) || fr.Severed) return;
            // Ponta da clava (armado) ou o punho (desarmado).
            var tip = Armed ? clubRb.position + clubRb.rotation * weaponTip
                            : fr.Rb.position + fr.Rb.rotation * new Vector3(0f, -0.12f * k, 0f);
            var head = pl.HeadPosition; var waist = new Vector3(head.x, pl.FeetPosition.y + 0.7f, head.z);
            var ab = head - waist;
            var c = waist + ab * Mathf.Clamp01(Vector3.Dot(tip - waist, ab) / ab.sqrMagnitude);
            if (Vector3.Distance(tip, c) > (Armed ? 0.28f : 0.22f)) return;
            strikeHit = true;
            pl.TakeDamage(State.Def.AttackDamage * (Armed ? 1f : ActiveRagdollRules.PunchDamageFraction), Center);
            ArenaAudio.Play(Sfx.Impact, tip, 0.8f);
        }

        // ---------- Golpes da arma do jogador ----------
        public void OnBoneCollision(RagBone id, Collision c)
        {
            var pl = Player;
            if (!pl) return;
            var w = pl.Weapon;
            if (!w || !w.Body || c.rigidbody != w.Body) return;
            Bone bone = null;
            if (c.contactCount == 0 || (id != RagBone.Clava && !bones.TryGetValue(id, out bone))) return;
            var contact = c.GetContact(0).point;
            float speed = c.relativeVelocity.magnitude;
            // A clava bateu na arma: aparo (durante o ataque), bloqueio (na guarda) ou desarme (golpe forte nela).
            if (id == RagBone.Clava)
            {
                if (!Armed || speed < 1.5f) return;
                if (attack == Attack.Windup || attack == Attack.Strike)
                {
                    if (pl.State.CounterStance > 0f) ResolveGuard(pl, GuardResult.Counter, contact);
                    else Parried(contact);
                    strikeHit = true;
                }
                else if (guardT > 0f)
                {
                    ArenaFx.FloatText("BLOQUEOU", contact + Vector3.up * 0.2f, new Color(0.8f, 0.8f, 0.8f), 0.8f);
                    ArenaAudio.Play(Sfx.Block, contact, 0.9f);
                    ArenaInput.Haptic(0.5f, 0.06f);
                    if (speed >= ActiveRagdollRules.DisarmSpeed * 1.6f) Disarm(-c.relativeVelocity);   // pancada muito forte na guarda arranca a clava
                }
                else if (ActiveRagdollRules.Disarms(id, speed / spec.DisarmMultiplier)) Disarm(-c.relativeVelocity);
                return;
            }
            // Peça arrancada no chão (esqueleto): golpe quebra a peça — aí ela não volta mais.
            if (bone != null && bone.Severed)
            {
                if (spec.Reassembles && speed >= RagdollSpec.BreakSpeed && bone.Rb.position.y < 0.4f * k) Shatter(bone);
                return;
            }
            if (hitCd > 0f || mode == Mode.Dead && !CanHitCorpse) return;
            var def = w.Def;
            var bladeDir = w.BladeDir;
            // Velocidade da ponta medida ANTES do impacto (depois do contato a colisão já freou a lâmina) — igual ao inimigo antigo.
            var tipVel = w.PhysTipVelocity;
            float tipSpeed = tipVel.magnitude;
            var vel = tipVel.sqrMagnitude > c.relativeVelocity.sqrMagnitude ? tipVel : c.relativeVelocity;
            speed = Mathf.Max(speed, tipSpeed);
            float align = tipSpeed > 0.01f ? Vector3.Dot(bladeDir, tipVel / tipSpeed) : 0f;
            var tipWorld = w.Body.position + w.Body.rotation * new Vector3(0f, 0f, def.Length);
            float fromTip = Vector3.Distance(contact, tipWorld);
            if (PlayerWeapon.DebugStab) Debug.Log($"[GoblinFisico] contato {id} vel={tipSpeed:0.0} alinh={align:0.00} ponta={fromTip:0.00}");
            // Perfurar
            if (StabRules.CanPierce(def.Kind) && ActiveRagdollRules.CanStab(tipSpeed, align, fromTip) && w.Held && !IsStuckBy(w))
            {
                Stab(bone, contact, w, tipSpeed);
                return;
            }
            if (speed < Combat.MinSwingSpeed) return;
            hitCd = 0.25f;
            float edge = Geometry.EdgeAlignment(bladeDir.ToN(), vel.ToN());
            // Desmembrar; golpe forte no braço da clava desarma (B&S: mão atingida solta a arma)
            bool canSever = !spec.SeverNeedsHeavy || def.Weight > 5f;   // orc: só arma pesada arranca membro
            if (canSever && ActiveRagdollRules.Severs(id, speed / spec.SeverMultiplier, edge, def.Blunt)) { Sever(bone, vel); }
            else if (Armed && ActiveRagdollRules.Disarms(id, speed / spec.DisarmMultiplier)) Disarm(vel);
            // Tipo de dano × material (maça no osso, machado/estocada na armadura) — igual aos inimigos antigos.
            var dtype = DamageTypes.Classify(def.Kind, edge, align);
            float dmg = Combat.MeleeDamage(speed, def, Combat.SharpEdge(pl.State.Has(SkillIds.FioAfiado), def), true, def.Blunt ? 1f : edge)
                        * ActiveRagdollRules.DamageMultiplier(id) * DamageTypes.Multiplier(DamageTypes.MaterialOf(Kind), dtype);
            if (dmg > 0f && mode != Mode.Dead) Damage(dmg, contact);
            flopT = ActiveRagdollRules.HitFlopSeconds;
            if (mode == Mode.Stand && ActiveRagdollRules.Knocksdown(id, speed / spec.KnockdownMultiplier, def.Weight > 5f)) KnockDown();
            ArenaAudio.Play(def.Weight > 5f ? Sfx.HitHeavy : Sfx.Hit, contact, 0.4f + speed / 10f);
        }

        bool CanHitCorpse => false;

        void Damage(float dmg, Vector3 at)
        {
            var ev = State.TakeDamage(dmg, 0f, false);
            ArenaFx.FloatText(Mathf.RoundToInt(ev.Amount).ToString(), at + Vector3.up * 0.2f, new Color(1f, 0.85f, 0.4f), 0.8f);
            ArenaFx.Burst(at, new Color(0.7f, 0.12f, 0.16f), 8, 2f);
            if (State.Dead) Die();
        }

        void KnockDown()
        {
            if (mode != Mode.Stand) return;
            mode = Mode.Down; downT = ActiveRagdollRules.DownSeconds; attack = Attack.None; guardT = 0f;
            ArenaGame.Tokens.Release(TokenId);
            SetAnchorStrength(0f);
            ArenaFx.FloatText("CAIU!", Center + Vector3.up * 0.5f, new Color(1f, 0.85f, 0.4f), 0.9f);
        }

        void StandUp()
        {
            var p = bones[RagBone.Bacia].Rb.position;
            anchor.position = ArenaUtil.ClampToArena(new Vector3(p.x, transform.position.y, p.z));
            mode = Mode.Stand; standT = ActiveRagdollRules.StandUpSeconds; attackCd = 1f;
        }

        void Die()
        {
            if (mode == Mode.Dead) return;
            mode = Mode.Dead; attack = Attack.None;
            ArenaGame.Tokens.Release(TokenId);
            if (anchorJoint) Destroy(anchorJoint);
            if (Armed) { Destroy(clubJoint); clubJoint = null; if (clubRb) Destroy(clubRb.gameObject, 10f); }
            ArenaFx.FloatText("MORREU", Center + Vector3.up * 0.6f, new Color(1f, 0.3f, 0.3f), 1.2f);
            ArenaAudio.Play(Sfx.Death, Center, 0.8f);
        }

        // Arranca o membro: quebra a junta com o osso pai (os filhos vão junto).
        void Sever(Bone b, Vector3 dir)
        {
            if (b.Severed || !b.Joint) return;
            Destroy(b.Joint);
            b.Joint = null;
            MarkSevered(b);
            var at = b.Rb.position;
            b.Rb.AddForce(dir.normalized * 3f + Vector3.up * 2f, ForceMode.VelocityChange);
            ArenaFx.Burst(at, new Color(0.7f, 0.12f, 0.16f), 26, 3.5f, 1.4f);
            ArenaAudio.Play(Sfx.Rip, at, 1f);
            ArenaInput.Haptic(1f, 0.12f);
            string name = b.Id == RagBone.Cabeca ? "DECAPITOU!" : ActiveRagdollRules.IsLeg(b.Id) ? "PERNA!" : "BRAÇO!";
            ArenaFx.FloatText(name, at + Vector3.up * 0.4f, new Color(1f, 0.25f, 0.25f), 1.2f);
            if (b.Id == RagBone.Cabeca && spec.HeadKills) { State.TakeDamage(State.Hp + 1f, 0f, false); Die(); return; }
            if (ActiveRagdollRules.IsWeaponArm(b.Id)) { canAttack = false; attack = Attack.None; }
            if (ActiveRagdollRules.IsLeg(b.Id)) { canStand = false; KnockDown(); }
            if (spec.Reassembles) { pieces.Add(new Piece { Bone = b, T = RagdollSpec.ReassembleSeconds }); ArenaAudio.Play(Sfx.Bone, at, 1f); }
            State.TakeDamage(spec.Reassembles ? 6f : 15f, 0f, false);
        }

        // ---------- Esqueleto: peças arrancadas voltam em 4 s, a não ser que sejam quebradas no chão ----------
        sealed class Piece { public Bone Bone; public float T; }
        readonly List<Piece> pieces = new List<Piece>();

        void UpdatePieces(float dt)
        {
            for (int i = pieces.Count - 1; i >= 0; i--)
            {
                var p = pieces[i];
                if (p.Bone == null || !p.Bone.Rb || mode == Mode.Dead) { pieces.RemoveAt(i); continue; }
                p.T -= dt;
                if (p.T > 0f || p.Bone.Parent == null || p.Bone.Parent.Severed || !p.Bone.Parent.Rb) continue;
                Reattach(p.Bone);
                pieces.RemoveAt(i);
            }
        }

        // Encaixa de volta: coloca a peça na pose original em relação ao pai e recria a junta.
        void Reattach(Bone b)
        {
            var parent = b.Parent.Rb;
            var rot = parent.rotation * b.RelRot;
            var jointWorld = parent.position + parent.rotation * b.ParentAnchorLocal;
            var pos = jointWorld - rot * b.AnchorLocal;
            b.Rb.position = pos; b.Rb.rotation = rot;
            b.Rb.transform.SetPositionAndRotation(pos, rot);
            b.Rb.linearVelocity = parent.linearVelocity; b.Rb.angularVelocity = Vector3.zero;
            var j = b.Rb.gameObject.AddComponent<ConfigurableJoint>();
            j.connectedBody = parent;
            j.autoConfigureConnectedAnchor = false;
            j.anchor = b.AnchorLocal;
            j.connectedAnchor = b.ParentAnchorLocal;
            j.xMotion = j.yMotion = j.zMotion = ConfigurableJointMotion.Locked;
            j.angularXMotion = j.angularYMotion = j.angularZMotion = ConfigurableJointMotion.Limited;
            var (lo, hi, y, z) = Limits(b.Id);
            j.lowAngularXLimit = new SoftJointLimit { limit = lo };
            j.highAngularXLimit = new SoftJointLimit { limit = hi };
            j.angularYLimit = new SoftJointLimit { limit = y };
            j.angularZLimit = new SoftJointLimit { limit = z };
            j.rotationDriveMode = RotationDriveMode.Slerp;
            j.enablePreprocessing = false;
            b.Joint = j;
            Unsever(b);
            ArenaFx.FloatText("SE MONTOU", b.Rb.position + Vector3.up * 0.3f, new Color(0.9f, 0.9f, 0.8f), 0.9f);
            ArenaAudio.Play(Sfx.Rattle, b.Rb.position, 1f);
            if (!bones[RagBone.BracoD].Severed && !bones[RagBone.AntebracoD].Severed) canAttack = true;
            if (!bones[RagBone.CoxaE].Severed && !bones[RagBone.CanelaE].Severed && !bones[RagBone.CoxaD].Severed && !bones[RagBone.CanelaD].Severed) canStand = true;
        }

        void Unsever(Bone b)
        {
            b.Severed = false;
            foreach (var o in boneList) if (o.Parent == b && o.Severed && o.Joint) Unsever(o);
        }

        // Peça arrancada quebrada no chão: some (não volta). Cabeça quebrada = morre.
        void Shatter(Bone b)
        {
            var at = b.Rb.position;
            ArenaFx.Burst(at, new Color(0.9f, 0.88f, 0.8f), 20, 3f, 1.2f);
            ArenaFx.FloatText("QUEBROU!", at + Vector3.up * 0.3f, new Color(1f, 0.9f, 0.7f), 1f);
            ArenaAudio.Play(Sfx.Bone, at, 1f, 0.7f);
            ArenaInput.Haptic(0.7f, 0.08f);
            pieces.RemoveAll(p => p.Bone == b);
            // Filhos presos nela também quebram.
            foreach (var o in boneList.ToArray()) if (o.Parent == b && o.Rb) Shatter(o);
            Destroy(b.Rb.gameObject);
            b.Rb = null;
            if (b.Id == RagBone.Cabeca && mode != Mode.Dead) { State.TakeDamage(State.Hp + 1f, 0f, false); Die(); }
            else if (mode != Mode.Dead) State.TakeDamage(10f, 0f, false);
        }

        void MarkSevered(Bone b)
        {
            b.Severed = true;
            foreach (var o in boneList) if (o.Parent == b && !o.Severed) MarkSevered(o);
        }

        // ---------- Perfurar ----------
        sealed class StabLink { public Bone Bone; public ConfigurableJoint Joint; public PlayerWeapon Weapon; public bool Deep; public int Frames; }
        readonly List<StabLink> stabs = new List<StabLink>();

        bool IsStuckBy(PlayerWeapon w) { foreach (var s in stabs) if (s.Weapon == w && s.Joint) return true; return false; }
        bool GrabbedOrStuck => stabs.Count > 0 || grabs > 0;
        int grabs;

        void Stab(Bone b, Vector3 contact, PlayerWeapon w, float speed)
        {
            var vis = w.Visual;
            // A lâmina passa pelo corpo todo (a física dela não bate mais nos ossos deste goblin).
            var body = BodyColliders();
            foreach (var wc in vis.GetComponents<Collider>()) foreach (var bc in body) Physics.IgnoreCollision(wc, bc, true);
            var j = b.Rb.gameObject.AddComponent<ConfigurableJoint>();
            j.connectedBody = w.Body;
            j.autoConfigureConnectedAnchor = false;
            // Ponto do osso preso na lâmina: começa ~6 cm dentro (puxar uns 8 cm solta; tremidas não).
            var onBlade = vis.TransformPoint(new Vector3(0f, 0f, Mathf.Min(w.Def.Length - 0.06f, vis.InverseTransformPoint(contact).z)));
            // Pose física dos corpos (o transform de corpo interpolado fica atrasado durante a física).
            var inv = Quaternion.Inverse(b.Rb.rotation);
            j.anchor = inv * (onBlade - b.Rb.position);
            j.connectedAnchor = Quaternion.Inverse(w.Body.rotation) * (onBlade - w.Body.position);
            j.axis = inv * (w.Body.rotation * Vector3.right);
            j.secondaryAxis = inv * (w.Body.rotation * Vector3.up);
            j.xMotion = j.yMotion = ConfigurableJointMotion.Locked;
            j.zMotion = ConfigurableJointMotion.Free;   // desliza ao longo da lâmina, com freio
            j.angularXMotion = j.angularYMotion = j.angularZMotion = ConfigurableJointMotion.Free;
            j.zDrive = new JointDrive { positionSpring = 0f, positionDamper = ActiveRagdollRules.StabDamper, maximumForce = float.MaxValue };
            j.enablePreprocessing = false;
            j.zDrive = new JointDrive { positionSpring = 0f, positionDamper = ActiveRagdollRules.StabDamperOut, maximumForce = float.MaxValue };
            stabs.Add(new StabLink { Bone = b, Joint = j, Weapon = w });
            if (mode == Mode.Stand) SetAnchorStrength(ActiveRagdollRules.StuckStandStrength);
            ArenaFx.Burst(contact, new Color(0.7f, 0.12f, 0.16f), 14, 3f);
            ArenaAudio.Play(Sfx.Stab, contact);
            ArenaInput.Haptic(1f, 0.1f);
            float dmg = StabRules.StabDamage(speed, w.Def, false, true) * ActiveRagdollRules.DamageMultiplier(b.Id)
                        * DamageTypes.Multiplier(DamageTypes.MaterialOf(Kind), DamageType.Perfuracao);
            if (b.Id == RagBone.Cabeca && spec.HeadKills && DamageTypes.IsFatalStab(EnemyKind.Goblin, true, speed))
            {
                ArenaFx.FloatText("GOLPE FATAL", contact + Vector3.up * 0.3f, new Color(1f, 0.3f, 0.3f), 1.3f);
                State.TakeDamage(State.Hp + 1f, 0f, false);
                Die();
                return;
            }
            ArenaFx.FloatText("CRAVOU!", contact + Vector3.up * 0.3f, new Color(1f, 0.55f, 0.45f), 1f);
            Damage(dmg, contact);
            flopT = 0.5f;
        }

        void UpdateStabsBleed(float dt)
        {
            for (int i = stabs.Count - 1; i >= 0; i--)
            {
                var s = stabs[i];
                var w = s.Weapon;
                bool gone = !s.Joint || !w || !w.Body || !w.Held;
                if (!gone)
                {
                    var vis = w.Visual;
                    // Pose FÍSICA (Rigidbody), não o transform: com interpolação o transform no passo de física está atrasado.
                    var p = s.Bone.Rb.position + s.Bone.Rb.rotation * s.Joint.anchor;
                    float z = (Quaternion.Inverse(w.Body.rotation) * (p - w.Body.position)).z;
                    // Freio de entrada/saída pela velocidade relativa ao longo da lâmina.
                    var axis = w.Body.rotation * Vector3.forward;
                    float vAxial = Vector3.Dot(s.Bone.Rb.GetPointVelocity(p) - w.Body.GetPointVelocity(p), axis);   // > 0: osso indo para a ponta (saindo)
                    float damper = vAxial > 0f ? ActiveRagdollRules.StabDamperOut : ActiveRagdollRules.StabDamperIn;
                    if (Mathf.Abs(s.Joint.zDrive.positionDamper - damper) > 1f)
                        s.Joint.zDrive = new JointDrive { positionSpring = 0f, positionDamper = damper, maximumForce = float.MaxValue };
                    // Só sai se o JOGADOR puxar a espada para trás (a arma recuando ao longo da lâmina); sozinho o corpo
                    // para na ponta. Também para na guarda. Travar = prender no ponto atual (sem tranco).
                    float L = w.Def.Length;
                    bool pulling = Vector3.Dot(w.Body.linearVelocity - w.RigVelocity, axis) < -ActiveRagdollRules.PullOutSpeed;
                    if (z > L + 0.02f && pulling) gone = true;                                   // puxou até a ponta sair
                    float lockZ = -1f;
                    if (z < ActiveRagdollRules.StabGuard) lockZ = ActiveRagdollRules.StabGuard;
                    else if (z > L - 0.03f && !pulling) lockZ = Mathf.Min(z, L - 0.03f);
                    if (lockZ >= 0f)
                    {
                        if (s.Joint.zMotion != ConfigurableJointMotion.Locked)
                        {
                            s.Joint.connectedAnchor = new Vector3(0f, 0f, lockZ);
                            s.Joint.zMotion = ConfigurableJointMotion.Locked;
                        }
                    }
                    else if (s.Joint.zMotion != ConfigurableJointMotion.Free) s.Joint.zMotion = ConfigurableJointMotion.Free;
                    if (!s.Deep && z < w.Def.Length - 0.25f)
                    {
                        s.Deep = true;
                        ArenaFx.FloatText("FUNDO!", p + Vector3.up * 0.3f, new Color(1f, 0.4f, 0.4f), 0.9f);
                        if (mode != Mode.Dead) Damage(12f, p);
                        ArenaInput.Haptic(0.8f, 0.08f);
                    }
                }
                if (gone) { ReleaseStab(s); stabs.RemoveAt(i); continue; }
                if (mode != Mode.Dead)
                {
                    bleedAcc += ActiveRagdollRules.BleedDps * dt;
                    if (bleedAcc >= 4f) { State.TakeDamage(bleedAcc, 0f, false); bleedAcc = 0f; }
                }
            }
        }

        void ReleaseStab(StabLink s)
        {
            if (s.Joint) Destroy(s.Joint);
            if (s.Weapon && s.Weapon.Visual)
            {
                var body = BodyColliders();
                foreach (var wc in s.Weapon.Visual.GetComponents<Collider>()) foreach (var bc in body) if (bc) Physics.IgnoreCollision(wc, bc, false);
            }
        }

        // ---------- Agarrar ----------
        // Prende a mão (corpo cinemático) num ponto da superfície do osso mais perto da palma.
        public static ConfigurableJoint TryGrab(Vector3 palm, Rigidbody hand, out RagdollGoblin owner)
        {
            owner = null;
            Bone best = null; RagdollGoblin bestG = null; float bestD = GrabRules.Reach; Vector3 bestP = default;
            foreach (var g in All)
            {
                if (!g) continue;
                foreach (var b in g.boneList)
                {
                    if (!b.Rb) continue;
                    foreach (var col in b.Rb.GetComponents<Collider>())
                    {
                        var p = col.ClosestPoint(palm);
                        float d = Vector3.Distance(p, palm);
                        if (d < bestD) { bestD = d; best = b; bestG = g; bestP = p; }
                    }
                }
            }
            if (best == null) return null;
            if (!bestG.spec.Grabbable)
            {
                if (Time.time - lastHeavyMsg > 1.5f) { lastHeavyMsg = Time.time; ArenaGame.Instance?.Message(bestG.State.Def.Name + " é pesado demais para agarrar", 1f); }
                return null;
            }
            hand.position = bestP; hand.transform.position = bestP;   // a mão encosta na superfície (por fora)
            var j = best.Rb.gameObject.AddComponent<ConfigurableJoint>();
            j.connectedBody = hand;
            j.autoConfigureConnectedAnchor = false;
            j.anchor = Quaternion.Inverse(best.Rb.rotation) * (bestP - best.Rb.position);
            j.connectedAnchor = Vector3.zero;
            j.xMotion = j.yMotion = j.zMotion = ConfigurableJointMotion.Locked;
            j.angularXMotion = j.angularYMotion = j.angularZMotion = ConfigurableJointMotion.Free;
            j.enablePreprocessing = false;
            bestG.grabs++;
            owner = bestG;
            ArenaFx.FloatText("AGARROU!", bestP + Vector3.up * 0.2f, new Color(1f, 0.85f, 0.6f), 0.8f);
            return j;
        }

        public void OnGrabReleased() => grabs = Mathf.Max(0, grabs - 1);
        static float lastHeavyMsg = -10f;

        // Soltou da mão: arremessado (ou largado no alto) cai de verdade — perde o pé e o corpo inteiro voa com a mão.
        public void OnThrown(Vector3 velocity)
        {
            bool thrown = velocity.magnitude > GrabRules.MinThrowSpeed;
            bool lifted = (bones[RagBone.Bacia].Rb.position - anchor.transform.TransformPoint(PelvisOffset)).magnitude > 0.2f;
            if (!thrown && !lifted) return;
            if (mode == Mode.Stand) KnockDown();
            if (thrown) foreach (var b in boneList) if (b.Rb && !b.Severed) b.Rb.linearVelocity = velocity;
        }

        void OnDestroy()
        {
            All.Remove(this);
            ArenaGame.Tokens.Release(TokenId);
            if (clubRb && Armed) Destroy(clubRb.gameObject);
            foreach (var s in stabs) ReleaseStab(s);
            foreach (var b in boneList) if (b.Rb) Destroy(b.Rb.gameObject);
        }
    }

    // Repassa as colisões de cada osso para o goblin.
    public sealed class RagdollBone : MonoBehaviour
    {
        public RagdollGoblin Owner { get; private set; }
        public RagBone Id { get; private set; }
        public void Init(RagdollGoblin owner, RagBone id) { Owner = owner; Id = id; }
        void OnCollisionEnter(Collision c) { if (Owner) Owner.OnBoneCollision(Id, c); }
    }

    // Treino: mantém N goblins físicos vivos (pausa → "Goblins físicos no Treino": 1, 3 ou 6 — para medir o FPS, passo A2).
    public sealed class RagdollGoblinSpawner : MonoBehaviour
    {
        public Vector3 Point, LookAt;
        readonly List<RagdollGoblin> current = new List<RagdollGoblin>();
        readonly List<float> wait = new List<float>();

        void Update()
        {
            int n = SaveSystem.Options.TrainingGoblins;
            while (current.Count < n) { current.Add(null); wait.Add(0.3f + current.Count * 0.2f); }
            for (int i = current.Count - 1; i >= n; i--)
            {
                if (current[i]) Destroy(current[i].gameObject);
                current.RemoveAt(i); wait.RemoveAt(i);
            }
            for (int i = 0; i < n; i++)
            {
                if (current[i] && !current[i].Dead) { wait[i] = 3f; continue; }
                wait[i] -= Time.deltaTime;
                if (wait[i] > 0f) continue;
                // Em leque a partir do ponto (1 m entre eles).
                var dir = (LookAt - Point); dir.y = 0f; dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.forward;
                var side = Vector3.Cross(Vector3.up, dir);
                float off = (i - (n - 1) * 0.5f) * 1.1f;
                var def = (i % 3) switch { 1 => EnemyDef.Esqueleto, 2 => EnemyDef.Orc, _ => EnemyDef.Goblin };
                current[i] = RagdollGoblin.Spawn(ArenaUtil.ClampToArena(Point + side * off - dir * Mathf.Abs(off) * 0.3f), LookAt, new EnemyState(def));
                wait[i] = 3f;
            }
        }
    }
}
