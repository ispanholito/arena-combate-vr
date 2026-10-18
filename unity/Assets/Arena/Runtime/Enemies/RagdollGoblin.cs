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
        }

        readonly Dictionary<RagBone, Bone> bones = new Dictionary<RagBone, Bone>();
        readonly List<Bone> boneList = new List<Bone>();
        public EnemyState State { get; private set; }
        Rigidbody anchor;
        ConfigurableJoint anchorJoint;
        float totalMass;
        static readonly Vector3 PelvisOffset = new Vector3(0f, 0.55f, 0f);   // bacia em relação aos pés (ponto da IA)

        enum Mode { Stand, Down, Dead }
        Mode mode = Mode.Stand;
        float muscle = 1f, flopT, downT, standT, deadT, hitCd, phase, attackT = -1f, attackCd = 1.5f, bleedAcc;
        enum Attack { None, Windup, Strike, Recover }
        Attack attack;
        bool strikeHit, canAttack = true, canStand = true;
        Material skin, cloth, club;

        public bool Dead => mode == Mode.Dead;
        public Vector3 Center => bones[RagBone.Tronco].Rb.position;

        ArenaPlayer Player => ArenaGame.Instance ? ArenaGame.Instance.Player : null;

        // ---------- Criação ----------
        public static RagdollGoblin Spawn(Vector3 feet, Vector3 lookAt)
        {
            var go = new GameObject("Goblin Fisico");
            go.transform.position = feet;
            var dir = (lookAt - feet); dir.y = 0f;
            go.transform.rotation = Quaternion.LookRotation(dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.forward);
            var g = go.AddComponent<RagdollGoblin>();
            g.Build();
            return g;
        }

        public static void ClearAll()
        {
            foreach (var g in All.ToArray()) if (g) Destroy(g.gameObject);
            All.Clear();
        }

        void Build()
        {
            State = new EnemyState(EnemyDef.Goblin);
            skin = ArenaUtil.Lit(new Color(0.37f, 0.54f, 0.23f));
            cloth = ArenaUtil.Lit(new Color(0.35f, 0.25f, 0.16f));
            club = ArenaUtil.Lit(new Color(0.42f, 0.27f, 0.15f));
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
            // Clava presa na mão direita (faz parte do antebraço).
            var fr = bones[RagBone.AntebracoD];
            var clubCol = fr.Rb.gameObject.AddComponent<CapsuleCollider>();
            clubCol.direction = 2; clubCol.radius = 0.06f; clubCol.height = 0.5f;
            clubCol.center = fr.Rb.transform.InverseTransformPoint(transform.TransformPoint(new Vector3(0.23f, 0.46f, 0.22f)));
            var clubVis = ArenaUtil.Prim(PrimitiveType.Capsule, fr.Rb.transform, clubCol.center, new Vector3(0.12f, 0.25f, 0.12f), club).transform;
            clubVis.localRotation = Quaternion.Euler(90, 0, 0);
            fr.Rb.mass += 1.3f; fr.Mass += 1.3f;
            // Rosto: olhos e orelhas presos à cabeça.
            var head = bones[RagBone.Cabeca].Rb.transform;
            var eye = ArenaUtil.Lit(new Color(1f, 0.88f, 0.29f), new Color(0.6f, 0.5f, 0.1f));
            foreach (int s in new[] { -1, 1 })
            {
                ArenaUtil.Prim(PrimitiveType.Sphere, head, new Vector3(s * 0.075f, 0.03f, 0.16f), Vector3.one * 0.06f, eye);
                ArenaUtil.Prim(PrimitiveType.Capsule, head, new Vector3(s * 0.22f, 0.05f, 0), new Vector3(0.06f, 0.12f, 0.06f), skin)
                    .transform.localRotation = Quaternion.Euler(0, 0, -s * 65f);
            }
            // Ossos do mesmo corpo não colidem entre si (evita tremedeira); o resto do mundo sim.
            var cols = new List<Collider>();
            foreach (var b in boneList) cols.AddRange(b.Rb.GetComponents<Collider>());
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
            var b = NewBone(id, parent, center, mass);
            b.Rb.gameObject.AddComponent<BoxCollider>().size = size;
            b.Col = b.Rb.GetComponent<Collider>();
            ArenaUtil.Prim(PrimitiveType.Cube, b.Rb.transform, Vector3.zero, size, m);
            Connect(b, joint);
        }

        void AddSphere(RagBone id, RagBone parent, Vector3 center, float r, float mass, Vector3 joint)
        {
            var b = NewBone(id, parent, center, mass);
            b.Rb.gameObject.AddComponent<SphereCollider>().radius = r;
            b.Col = b.Rb.GetComponent<Collider>();
            ArenaUtil.Prim(PrimitiveType.Sphere, b.Rb.transform, Vector3.zero, Vector3.one * r * 2f, skin);
            Connect(b, joint);
        }

        // Membro em cápsula do ponto "de" (junta) até "até".
        void AddLimb(RagBone id, RagBone parent, Vector3 from, Vector3 to, float r, float mass, Material m)
        {
            var b = NewBone(id, parent, (from + to) * 0.5f, mass);
            var cap = b.Rb.gameObject.AddComponent<CapsuleCollider>();
            cap.direction = 1; cap.radius = r; cap.height = Vector3.Distance(from, to) + r * 2f;
            b.Col = cap;
            ArenaUtil.Prim(PrimitiveType.Capsule, b.Rb.transform, Vector3.zero, new Vector3(r * 2f, cap.height * 0.5f, r * 2f), m);
            Connect(b, from);
        }

        float anchorStrength = 1f;

        void SetAnchorStrength(float k)
        {
            anchorStrength = k;
            if (!anchorJoint) return;
            const float w = 14f;
            float spring = totalMass * w * w * k;
            var lin = new JointDrive { positionSpring = spring, positionDamper = 2f * 0.9f * totalMass * w * Mathf.Sqrt(Mathf.Max(k, 0.05f)), maximumForce = totalMass * 80f * k };
            anchorJoint.xDrive = anchorJoint.yDrive = anchorJoint.zDrive = lin;
            anchorJoint.slerpDrive = new JointDrive { positionSpring = 900f * k, positionDamper = 60f * Mathf.Max(k, 0.05f), maximumForce = float.MaxValue };
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
            // Braço da clava: parado à frente; ataque = ergue por cima e desce.
            switch (attack)
            {
                case Attack.Windup: pose[RagBone.BracoD] = new Vector3(150f, -15f, 0); pose[RagBone.AntebracoD] = new Vector3(-70f, 0, 0); break;
                case Attack.Strike: pose[RagBone.BracoD] = new Vector3(-75f, 10f, 0); pose[RagBone.AntebracoD] = new Vector3(-5f, 0, 0); break;
                default: pose[RagBone.BracoD] = new Vector3(-30f, 0, 8f); pose[RagBone.AntebracoD] = new Vector3(-50f, 0, 0); break;
            }
        }

        void ApplyMuscles()
        {
            foreach (var b in boneList)
            {
                if (!b.Joint) continue;
                float m = b.Severed ? 0f : muscle;
                if (b.Id == RagBone.BracoD || b.Id == RagBone.AntebracoD) m *= attack == Attack.Strike ? 2.5f : 1.3f;   // golpe rápido
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
                State.Tick(dt);
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

        // IA simples: vira para o jogador, anda até ~1,3 m e ataca com a clava (erguer 1 s, descer, recuperar).
        float Think(ArenaPlayer pl, float dt)
        {
            var to = pl.FeetPosition - anchor.position; to.y = 0f;
            float dist = to.magnitude;
            if (dist > 1e-3f)
            {
                var want = Quaternion.LookRotation(to / dist);
                anchor.MoveRotation(Quaternion.Slerp(anchor.rotation, want, dt * 5f));
            }
            float speed = 0f;
            bool stuck = GrabbedOrStuck;
            if (attack == Attack.None && dist > 1.35f && !stuck)
            {
                speed = 1.3f;
                var next = anchor.position + to / Mathf.Max(dist, 1e-3f) * speed * dt;
                anchor.MovePosition(ArenaUtil.ClampToArena(next));
                phase += dt * 7f;
            }
            attackCd -= dt;
            if (attack == Attack.None && dist <= 1.6f && attackCd <= 0f && canAttack && !stuck) { attack = Attack.Windup; attackT = 0f; strikeHit = false; }
            if (attack != Attack.None)
            {
                attackT += dt;
                if (attack == Attack.Windup && attackT > 1.0f) { attack = Attack.Strike; attackT = 0f; ArenaAudio.Play(Sfx.Whoosh, Center, 0.7f, 0.8f); }
                else if (attack == Attack.Strike)
                {
                    if (!strikeHit) CheckClubHit(pl);
                    if (attackT > 0.35f) { attack = Attack.Recover; attackT = 0f; }
                }
                else if (attack == Attack.Recover && attackT > 0.7f) { attack = Attack.None; attackCd = 1.4f; }
            }
            return speed > 0f ? 1f : 0f;
        }

        // A clava acerta se a ponta passar perto do corpo do jogador (cabeça até a cintura).
        void CheckClubHit(ArenaPlayer pl)
        {
            if (!bones.TryGetValue(RagBone.AntebracoD, out var fr) || fr.Severed) return;
            var tip = fr.Rb.transform.TransformPoint(new Vector3(0, -0.12f, 0.45f));
            var head = pl.HeadPosition; var waist = new Vector3(head.x, pl.FeetPosition.y + 0.7f, head.z);
            var ab = head - waist;
            var c = waist + ab * Mathf.Clamp01(Vector3.Dot(tip - waist, ab) / ab.sqrMagnitude);
            if (Vector3.Distance(tip, c) > 0.28f) return;
            strikeHit = true;
            pl.TakeDamage(EnemyDef.Goblin.AttackDamage, Center);
            ArenaAudio.Play(Sfx.Impact, tip, 0.8f);
        }

        // ---------- Golpes da arma do jogador ----------
        public void OnBoneCollision(RagBone id, Collision c)
        {
            var pl = Player;
            if (!pl) return;
            var w = pl.Weapon;
            if (!w || !w.Body || c.rigidbody != w.Body) return;
            if (!bones.TryGetValue(id, out var bone) || c.contactCount == 0) return;
            var contact = c.GetContact(0).point;
            float speed = c.relativeVelocity.magnitude;
            // Aparo físico: a clava bateu na arma durante o ataque.
            if (id == RagBone.AntebracoD && (attack == Attack.Windup || attack == Attack.Strike) && speed > 1.5f)
            {
                attack = Attack.Recover; attackT = 0f; flopT = 0.6f;
                ArenaFx.FloatText("APARADO", contact + Vector3.up * 0.2f, new Color(0.6f, 0.9f, 1f), 0.9f);
                ArenaAudio.Play(Sfx.Parry, contact, 1f);
                ArenaInput.Haptic(0.8f, 0.08f);
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
            // Desmembrar
            if (ActiveRagdollRules.Severs(id, speed, edge, def.Blunt)) { Sever(bone, vel); }
            float dmg = Combat.MeleeDamage(speed, def, Combat.SharpEdge(pl.State.Has(SkillIds.FioAfiado), def), true, def.Blunt ? 1f : edge)
                        * ActiveRagdollRules.DamageMultiplier(id);
            if (dmg > 0f && mode != Mode.Dead) Damage(dmg, contact);
            flopT = ActiveRagdollRules.HitFlopSeconds;
            if (mode == Mode.Stand && ActiveRagdollRules.Knocksdown(id, speed, def.Weight > 5f)) KnockDown();
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
            mode = Mode.Down; downT = ActiveRagdollRules.DownSeconds; attack = Attack.None;
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
            if (anchorJoint) Destroy(anchorJoint);
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
            if (b.Id == RagBone.Cabeca) { State.TakeDamage(State.Hp + 1f, 0f, false); Die(); return; }
            if (ActiveRagdollRules.IsWeaponArm(b.Id)) { canAttack = false; attack = Attack.None; }
            if (ActiveRagdollRules.IsLeg(b.Id)) { canStand = false; KnockDown(); }
            State.TakeDamage(15f, 0f, false);
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
            foreach (var wc in vis.GetComponents<Collider>())
                foreach (var o in boneList) foreach (var bc in o.Rb.GetComponents<Collider>()) Physics.IgnoreCollision(wc, bc, true);
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
            float dmg = StabRules.StabDamage(speed, w.Def, false, true) * ActiveRagdollRules.DamageMultiplier(b.Id);
            if (b.Id == RagBone.Cabeca && DamageTypes.IsFatalStab(EnemyKind.Goblin, true, speed))
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
                foreach (var wc in s.Weapon.Visual.GetComponents<Collider>())
                    foreach (var o in boneList) if (o.Rb) foreach (var bc in o.Rb.GetComponents<Collider>()) Physics.IgnoreCollision(wc, bc, false);
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

    // Treino: mantém um goblin físico vivo ao lado do boneco.
    public sealed class RagdollGoblinSpawner : MonoBehaviour
    {
        public Vector3 Point, LookAt;
        RagdollGoblin current;
        float wait = 0.5f;

        void Update()
        {
            if (current && !current.Dead) { wait = 3f; return; }
            wait -= Time.deltaTime;
            if (wait > 0f) return;
            current = RagdollGoblin.Spawn(Point, LookAt);
            wait = 3f;
        }
    }
}
