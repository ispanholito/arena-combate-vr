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
        float paceSeed => (GetInstanceID() & 1023) * 0.01f;   // vai-e-volta próprio de cada um (EnemyMotion.Pace)
        Vector3 smoothMove;
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
        Vector3 faceLocal = Vector3.forward;
        float footT;   // logo depois de um golpe que NÃO derruba: ser empurrado não faz perder o pé
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
        // IA em grupo (CombatSteering): rondando = sem ficha; esquiva de ameaça; finta.
        bool circling, feint;
        float dodgeT, threatCheckT, kameCheckT;
        Vector3 dodgeDir;
        // Reação pendente (tempo de reação humano): esquiva ou guarda só começam quando o tempo acaba.
        float reactDodgeT = -1f, reactGuardT = -1f;
        Vector3 reactDodgeDir;
        readonly HashSet<Projectile> seenThreats = new HashSet<Projectile>();
        static readonly List<System.Numerics.Vector2> neighbors = new List<System.Numerics.Vector2>();
        bool riposte;
        Vector3 lungeDir;
        int TokenId => GetInstanceID();
        public bool IsDown => mode == Mode.Down;   // caído (derrubado), para diagnóstico/IA
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
            faceLocal = Quaternion.Inverse(bones[RagBone.Cabeca].Rb.rotation) * transform.forward;   // para onde a cara olha (regiões da cabeça)
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
            if (State.Def.Kind == EnemyKind.Esqueleto) DressSkeleton();
            else if (State.Def.Kind == EnemyKind.Goblin) DressModel("Goblin");
            baseY = transform.position.y;
            helmet = Random.value < spec.HelmetChance;
            if (helmet) BuildHelmet();
            if (Random.value < spec.ShieldChance) BuildShield();

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

        // Esqueleto = "goblin em ossos" (pedido do Pablo, 2026-10-28, depois de não gostar do modelo do KayKit): crânio de
        // goblin com focinho, orelhas pontudas de osso e órbitas escuras com brasas; costelas, coluna e bacia; ossos finos
        // com nós nas juntas, mãos e pés. Os colisores continuam os mesmos; só o desenho muda. As peças de cada osso são
        // juntadas numa malha por material (poucas chamadas de desenho no Quest).
        void DressSkeleton()
        {
            var boneMat = ArenaUtil.Lit(new Color(0.87f, 0.84f, 0.72f));
            var darkMat = ArenaUtil.Lit(new Color(0.05f, 0.04f, 0.04f));
            var glowMat = ArenaUtil.Lit(new Color(1f, 0.35f, 0.15f), new Color(1.8f, 0.4f, 0.1f));
            foreach (var b in boneList)
            {
                var t = b.Rb.transform;
                foreach (var r in t.GetComponentsInChildren<Renderer>()) r.enabled = false;
                var holder = new GameObject("Ossos").transform;
                holder.SetParent(t, false);
                void P(PrimitiveType type, Vector3 pos, Vector3 size, Material m, Vector3? euler = null)
                {
                    var g = ArenaUtil.Prim(type, holder, pos, size, m);
                    if (euler.HasValue) g.transform.localRotation = Quaternion.Euler(euler.Value);
                }
                switch (b.Col)
                {
                    case CapsuleCollider c:
                    {
                        float L = c.height - 2f * c.radius, th = c.radius * 0.42f;
                        P(PrimitiveType.Cylinder, Vector3.zero, new Vector3(th * 2f, L * 0.5f, th * 2f), boneMat);
                        P(PrimitiveType.Sphere, Vector3.up * L * 0.5f, Vector3.one * c.radius * 1.15f, boneMat);    // junta de cima
                        P(PrimitiveType.Sphere, Vector3.down * L * 0.5f, Vector3.one * c.radius * 0.95f, boneMat);  // junta de baixo
                        if (b.Id == RagBone.CanelaE || b.Id == RagBone.CanelaD)   // pé
                            P(PrimitiveType.Cube, new Vector3(0f, -L * 0.5f - c.radius * 0.35f, 0.05f * k), new Vector3(0.07f, 0.03f, 0.17f) * k, boneMat);
                        if (b.Id == RagBone.AntebracoE || b.Id == RagBone.AntebracoD)   // mão de dedos compridos
                        {
                            float y = -L * 0.5f - c.radius * 0.8f;
                            P(PrimitiveType.Sphere, new Vector3(0f, y, 0f), new Vector3(0.07f, 0.05f, 0.05f) * k, boneMat);
                            for (int f = -1; f <= 1; f++)
                                P(PrimitiveType.Capsule, new Vector3(f * 0.022f * k, y - 0.05f * k, 0.01f * k), new Vector3(0.016f, 0.04f, 0.016f) * k, boneMat, new Vector3(-15f, 0f, f * 12f));
                        }
                        break;
                    }
                    case BoxCollider box when b.Id == RagBone.Tronco:
                    {
                        var s = box.size;   // ~0.34 × 0.32 × 0.20 (× escala)
                        P(PrimitiveType.Cylinder, new Vector3(0f, 0f, -s.z * 0.35f), new Vector3(0.045f * k, s.y * 0.55f, 0.045f * k), boneMat);   // coluna
                        for (int i = 0; i < 4; i++)
                        {
                            float y = s.y * (0.28f - i * 0.17f), w = s.x * (0.92f - Mathf.Abs(i - 1.2f) * 0.07f);
                            foreach (int sd in new[] { -1, 1 })   // costela: lado indo de trás para frente
                                P(PrimitiveType.Capsule, new Vector3(sd * w * 0.45f, y, 0f), new Vector3(0.028f * k, s.z * 0.42f, 0.028f * k), boneMat, new Vector3(90f, sd * -12f, 0f));
                            P(PrimitiveType.Capsule, new Vector3(0f, y, s.z * 0.42f), new Vector3(0.026f * k, w * 0.3f, 0.026f * k), boneMat, new Vector3(0f, 0f, 90f));   // frente
                        }
                        P(PrimitiveType.Cube, new Vector3(0f, 0f, s.z * 0.46f), new Vector3(0.035f * k, s.y * 0.6f, 0.025f * k), boneMat);   // esterno
                        P(PrimitiveType.Capsule, new Vector3(0f, s.y * 0.48f, s.z * 0.1f), new Vector3(0.03f * k, s.x * 0.48f, 0.03f * k), boneMat, new Vector3(0f, 0f, 90f));   // clavículas
                        break;
                    }
                    case BoxCollider box:   // bacia
                    {
                        var s = box.size;
                        foreach (int sd in new[] { -1, 1 })
                            P(PrimitiveType.Sphere, new Vector3(sd * s.x * 0.24f, 0.01f * k, 0f), new Vector3(s.x * 0.42f, s.y * 0.75f, s.z * 0.45f), boneMat, new Vector3(0f, 0f, sd * -20f));
                        P(PrimitiveType.Cylinder, new Vector3(0f, s.y * 0.55f, -s.z * 0.3f), new Vector3(0.045f * k, s.y * 0.5f, 0.045f * k), boneMat);   // coluna baixa
                        break;
                    }
                    case SphereCollider sc:   // crânio de goblin
                    {
                        float r = sc.radius;
                        P(PrimitiveType.Sphere, new Vector3(0f, 0.05f * r, -0.05f * r), new Vector3(1.9f, 1.75f, 1.95f) * r, boneMat);          // calota
                        P(PrimitiveType.Sphere, new Vector3(0f, -0.3f * r, 0.75f * r), new Vector3(1.05f, 0.75f, 1.0f) * r, boneMat);            // focinho
                        P(PrimitiveType.Cube, new Vector3(0f, 0.32f * r, 0.72f * r), new Vector3(1.5f, 0.22f, 0.4f) * r, boneMat, new Vector3(-12f, 0f, 0f));   // sobrancelha
                        foreach (int sd in new[] { -1, 1 })
                        {
                            P(PrimitiveType.Sphere, new Vector3(sd * 0.4f * r, 0.08f * r, 0.78f * r), Vector3.one * 0.48f * r, darkMat);          // órbita
                            P(PrimitiveType.Sphere, new Vector3(sd * 0.4f * r, 0.08f * r, 0.96f * r), Vector3.one * 0.16f * r, glowMat);          // brasa
                            P(PrimitiveType.Capsule, new Vector3(sd * 1.05f * r, 0.25f * r, -0.1f * r), new Vector3(0.22f, 0.62f, 0.1f) * r, boneMat, new Vector3(0f, sd * 15f, sd * -68f));   // orelha pontuda
                            P(PrimitiveType.Sphere, new Vector3(sd * 0.1f * r, -0.25f * r, 1.22f * r), Vector3.one * 0.13f * r, darkMat);         // narina
                        }
                        P(PrimitiveType.Cube, new Vector3(0f, -0.72f * r, 0.55f * r), new Vector3(0.95f, 0.2f, 0.85f) * r, boneMat, new Vector3(8f, 0f, 0f));   // maxilar
                        for (int i = -2; i <= 2; i++)   // dentes
                            P(PrimitiveType.Cube, new Vector3(i * 0.17f * r, -0.55f * r, 1.02f * r), new Vector3(0.1f, 0.16f, 0.08f) * r, boneMat);
                        break;
                    }
                }
                CombineByMaterial(holder);
            }
        }

        // Modelo gerado por IA e cortado por osso no editor (Editor/AiModelImport): esconde as formas simples e põe o
        // pedaço do modelo em cada osso (arrancar a cabeça leva a cabeça do modelo). Sem os arquivos, fica como estava.
        static readonly Dictionary<string, Material> modelMats = new Dictionary<string, Material>();

        void DressModel(string name)
        {
            if (!modelMats.TryGetValue(name, out var mat))
            {
                var tex = Resources.Load<Texture2D>($"Models/{name}/{name}_cor");
                mat = tex ? new Material(Shader.Find("Standard")) { mainTexture = tex, color = Color.white } : null;
                if (mat) mat.SetFloat("_Glossiness", 0.12f);
                modelMats[name] = mat;
            }
            if (!mat) return;
            foreach (var b in boneList)
            {
                var mesh = Resources.Load<Mesh>($"Models/{name}/{name}_{b.Id}");
                if (!mesh) continue;
                foreach (var r in b.Rb.GetComponentsInChildren<Renderer>()) r.enabled = false;
                var v = new GameObject("Modelo");
                v.transform.SetParent(b.Rb.transform, false);
                v.transform.SetPositionAndRotation(transform.position, transform.rotation);   // malha no espaço do corpo em repouso
                v.transform.localScale = Vector3.one * k;
                v.AddComponent<MeshFilter>().sharedMesh = mesh;
                v.AddComponent<MeshRenderer>().sharedMaterial = mat;
            }
        }

        // Junta as peças filhas numa malha por material (as primitivas vêm legíveis da Unity).
        static void CombineByMaterial(Transform holder)
        {
            var groups = new Dictionary<Material, List<CombineInstance>>();
            var toLocal = holder.worldToLocalMatrix;
            foreach (var mf in holder.GetComponentsInChildren<MeshFilter>())
            {
                var mat = mf.GetComponent<MeshRenderer>().sharedMaterial;
                if (!groups.TryGetValue(mat, out var list)) groups[mat] = list = new List<CombineInstance>();
                list.Add(new CombineInstance { mesh = mf.sharedMesh, transform = toLocal * mf.transform.localToWorldMatrix });
            }
            var kids = new List<GameObject>();
            foreach (Transform c in holder) kids.Add(c.gameObject);
            foreach (var kv in groups)
            {
                var mesh = new Mesh();
                mesh.CombineMeshes(kv.Value.ToArray(), true, true);
                var go = new GameObject("Malha");
                go.transform.SetParent(holder, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = kv.Key;
            }
            foreach (var kid in kids) Destroy(kid);
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
            if (shieldRb) cols.AddRange(shieldRb.GetComponents<Collider>());
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
        // Limites no ângulo da JUNTA, que na Unity tem o sinal oposto ao da pose (Euler local): pose +x = junta −x.
        // Antes estavam invertidos nos membros assimétricos (2026-10-30): o joelho e o cotovelo só dobravam "para o lado
        // errado" (travados retos) e a coxa ia só 60° à frente — o goblin deslizava sem passo e não conseguia ajoelhar.
        static (float, float, float, float) Limits(RagBone b) => b switch
        {
            RagBone.Tronco => (-35f, 35f, 35f, 25f),
            RagBone.Cabeca => (-40f, 40f, 60f, 30f),
            RagBone.BracoD or RagBone.BracoE => (-170f, 170f, 70f, 80f),
            RagBone.AntebracoD or RagBone.AntebracoE => (-10f, 145f, 20f, 10f),
            RagBone.CoxaD or RagBone.CoxaE => (-60f, 110f, 25f, 35f),
            _ => (-140f, 5f, 10f, 5f),   // canelas: dobram para trás
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

        // ---------- Andar (2ª versão, 2026-10-30, Pablo: "o jeito que ele anda não tem nada a ver") ----------
        // Ciclo de passo pela direção REAL do movimento: a coxa vai e volta, o joelho dobra na hora que a perna passa por
        // baixo do corpo (levanta o pé), o tronco inclina com a velocidade e gira contra as pernas, os braços balançam
        // opostos às pernas (o da clava menos) e a bacia sobe e desce a cada passo. De lado = passo lateral.
        float gaitF, gaitS;

        // ---------- Pescoço (2026-10-30: "tá muito travado") ----------
        // A cabeça procura o jogador (ou o ponto da vitrine) dentro do limite do pescoço, com atraso, e mexe um pouco sozinha
        // (olhadas rápidas e respiração). Passando do limite, o tronco ajuda a virar.
        float headYaw, headPitch;
        void UpdateLook(Vector3? target, float dt)
        {
            if (!bones.TryGetValue(RagBone.Tronco, out var tr) || !tr.Rb || !bones.TryGetValue(RagBone.Cabeca, out var hd) || !hd.Rb) return;
            float wantYaw = 0f, wantPitch = 0f;
            if (target.HasValue)
            {
                var local = Quaternion.Inverse(tr.Rb.rotation) * (target.Value - hd.Rb.position);
                wantYaw = Mathf.Atan2(local.x, Mathf.Max(0.05f, local.z)) * Mathf.Rad2Deg;
                wantPitch = Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg;
            }
            float t = Time.time + (GetInstanceID() & 255);
            wantYaw += (Mathf.PerlinNoise(t * 0.35f, 1.3f) - 0.5f) * 24f;     // olhadas
            wantPitch += (Mathf.PerlinNoise(1.7f, t * 0.3f) - 0.5f) * 10f;
            headYaw = Mathf.Lerp(headYaw, Mathf.Clamp(wantYaw, -70f, 70f), dt * 6f);
            headPitch = Mathf.Lerp(headPitch, Mathf.Clamp(wantPitch, -30f, 30f), dt * 6f);
        }

        void BuildPose(float speed01)
        {
            pose.Clear();
            // Velocidade real e direção do passo (no espaço do corpo). GaitRules dá amplitude e ritmo sem o pé escorregar.
            float v = new Vector2(gaitF, gaitS).magnitude * (speed01 > 0f ? 1f : 0f);
            float dirF = v > 0.01f ? gaitF / v : 0f, dirS = v > 0.01f ? gaitS / v : 0f;
            float on = Mathf.Clamp01(v / 0.25f);                       // some suave ao parar
            float run = GaitRules.Run(v);
            float A = GaitRules.ThighAmplitudeDeg(v, k) * on;
            float s = Mathf.Sin(phase), c = Mathf.Cos(phase);
            float back = Mathf.Sign(dirF + 1e-4f);                      // andando de costas: o joelho dobra na outra metade
            // Coxa (+ = para trás): esquerda e direita em oposição. A parte de lado abre a perna que está no ar.
            float swingF = s * A * dirF;
            float sideOpen = A * 0.6f * dirS;
            pose[RagBone.CoxaE] = new Vector3(swingF, 0, Mathf.Max(0f, -c * back) * sideOpen);
            pose[RagBone.CoxaD] = new Vector3(-swingF, 0, Mathf.Max(0f, c * back) * sideOpen);
            // Joelho: perna no ar (coxa indo para frente) dobra muito; na de apoio, pouco (absorve o peso).
            float kSwing = GaitRules.SwingKneeDeg(v) * on, kStance = GaitRules.StanceKneeDeg(v) * on;
            float liftE = Mathf.Max(0f, -c * back), liftD = Mathf.Max(0f, c * back);
            pose[RagBone.CanelaE] = new Vector3(6f + kStance * (1f - liftE) + kSwing * Mathf.Pow(liftE, 0.8f), 0, 0);
            pose[RagBone.CanelaD] = new Vector3(6f + kStance * (1f - liftD) + kSwing * Mathf.Pow(liftD, 0.8f), 0, 0);
            // Tronco: inclinado à frente pela velocidade, gira de leve contra as pernas (ombros acompanham os braços),
            // quase sem balanço lateral; parado respira. Além de 45° de pescoço o tronco ajuda a virar.
            float lean = GaitRules.TorsoLeanDeg(v) * on * Mathf.Max(0f, dirF);
            float breathe = Mathf.Sin(Time.time * 2.1f) * 2f * (1f - on);
            pose[RagBone.Tronco] = new Vector3(-6f - lean + breathe, -s * (5f + 4f * run) * on * dirF + Mathf.Sign(headYaw) * Mathf.Clamp(Mathf.Abs(headYaw) - 45f, 0f, 25f), c * 1.5f * on);
            // Cabeça estável: compensa a inclinação e o giro do tronco e olha (UpdateLook).
            pose[RagBone.Cabeca] = new Vector3(8f + lean * 0.8f + headPitch, Mathf.Clamp(headYaw + s * (5f + 4f * run) * on * dirF, -55f, 55f), 0f);
            // Braço livre: oposto à perna do mesmo lado; cotovelo dobra mais correndo (mão passa do quadril ao peito).
            float elbow = GaitRules.ElbowDeg(v) * on + 10f * (1f - on);
            float swing = swingF;   // usado pelo braço da clava
            pose[RagBone.BracoE] = new Vector3(-swingF * 0.9f - 8f - 6f * run, 0, -10f);
            pose[RagBone.AntebracoE] = new Vector3(-elbow - Mathf.Max(0f, -s * dirF) * 15f * on, 0, 0);
            // Braço da clava: parado à frente (balança um pouco andando); ataque = pose de aviso e golpe; guarda = clava na frente.
            (System.Numerics.Vector3 arm, System.Numerics.Vector3 fore) p;
            if (guardT > 0f && attack == Attack.None) p = ActiveRagdollPoses.Guard;
            else switch (attack)
            {
                case Attack.Windup: p = ActiveRagdollPoses.Windup(curAttack); break;
                case Attack.Strike: p = ActiveRagdollPoses.Strike(curAttack); break;
                default: p = (new System.Numerics.Vector3(-30f + swing * 0.5f, 0f, 8f), new System.Numerics.Vector3(-50f - 30f * run, 0f, 0f)); break;
            }
            pose[RagBone.BracoD] = p.arm.ToU();
            pose[RagBone.AntebracoD] = p.fore.ToU();
            if (!Armed && attack == Attack.Windup) { pose[RagBone.BracoD] = new Vector3(-20f, 0f, 20f); pose[RagBone.AntebracoD] = new Vector3(-120f, 0f, 0f); }   // punho recolhido
            if (!Armed && attack == Attack.Strike) { pose[RagBone.BracoD] = new Vector3(-85f, 0f, -5f); pose[RagBone.AntebracoD] = new Vector3(-5f, 0f, 0f); }    // soco reto
            if (HasShield) { pose[RagBone.BracoE] = new Vector3(-70f, 0f, -15f); pose[RagBone.AntebracoE] = new Vector3(-90f, 0f, 0f); }   // escudo na frente
            if (standT > 0f) GetUpPose(1f - Mathf.Clamp01(standT / getUpTotal));
        }

        // ---------- Levantar (3ª versão, 2026-10-30, roteiro biomecânico mandado pelo Pablo) ----------
        // A bacia segue um CAMINHO de poses-chave (altura + orientação no mundo, pelo ponto da IA, que gira junto) e os
        // músculos fazem a pose de cada etapa. Depende de como caiu:
        //   De barriga: mãos no chão perto do peito → empurra e sobe o peito → joelhos para baixo do quadril (de quatro)
        //               → uma perna à frente (meio-ajoelhado) → empurra o pé e fica de pé, com um ajuste de equilíbrio.
        //   De costas:  dobra os joelhos → ROLA de lado (cabeça, ombros e quadril juntos) → fica de barriga com as mãos
        //               apoiadas → e segue igual (de quatro → meio-ajoelhado → de pé).
        // Assimetria: a perna esquerda vai um pouco antes; a mão esquerda é a de apoio no joelho.
        struct GetUpKey
        {
            public float T, Height;       // T = 0..1 do tempo; Height = altura da bacia (× escala)
            public Quaternion Pelvis;     // orientação da bacia no mundo
            public (RagBone, Vector3)[] Limbs;
        }
        GetUpKey[] getUp;
        float getUpTotal;
        Vector3 getUpXZ;

        static (RagBone, Vector3)[] L(params (RagBone, Vector3)[] l) => l;
        static readonly (RagBone, Vector3)[] PoseLyingBack = L(
            (RagBone.CoxaE, new Vector3(-55f, 0f, 0f)), (RagBone.CanelaE, new Vector3(85f, 0f, 0f)),
            (RagBone.CoxaD, new Vector3(-45f, 0f, 0f)), (RagBone.CanelaD, new Vector3(75f, 0f, 0f)),
            (RagBone.Tronco, new Vector3(-10f, 0f, 0f)), (RagBone.Cabeca, new Vector3(-15f, 0f, 0f)),
            (RagBone.BracoE, new Vector3(-10f, 0f, -20f)), (RagBone.AntebracoE, new Vector3(-30f, 0f, 0f)),
            (RagBone.BracoD, new Vector3(-90f, 0f, 35f)), (RagBone.AntebracoD, new Vector3(-20f, 0f, 0f)));   // braço direito cruza para rolar
        static readonly (RagBone, Vector3)[] PoseProneHands = L(
            (RagBone.CoxaE, new Vector3(0f, 0f, 0f)), (RagBone.CanelaE, new Vector3(10f, 0f, 0f)),
            (RagBone.CoxaD, new Vector3(0f, 0f, 0f)), (RagBone.CanelaD, new Vector3(5f, 0f, 0f)),
            (RagBone.Tronco, new Vector3(0f, 0f, 0f)), (RagBone.Cabeca, new Vector3(25f, 0f, 0f)),
            (RagBone.BracoE, new Vector3(-40f, 0f, -25f)), (RagBone.AntebracoE, new Vector3(-110f, 0f, 0f)),   // mãos no chão perto do peito
            (RagBone.BracoD, new Vector3(-40f, 0f, 25f)), (RagBone.AntebracoD, new Vector3(-110f, 0f, 0f)));
        static readonly (RagBone, Vector3)[] PosePushUp = L(
            (RagBone.CoxaE, new Vector3(-20f, 0f, 0f)), (RagBone.CanelaE, new Vector3(35f, 0f, 0f)),
            (RagBone.CoxaD, new Vector3(-5f, 0f, 0f)), (RagBone.CanelaD, new Vector3(15f, 0f, 0f)),
            (RagBone.Tronco, new Vector3(15f, 0f, 0f)), (RagBone.Cabeca, new Vector3(30f, 0f, 0f)),          // coluna estende, cabeça acompanha
            (RagBone.BracoE, new Vector3(-75f, 0f, -12f)), (RagBone.AntebracoE, new Vector3(-30f, 0f, 0f)),     // cotovelos estendendo
            (RagBone.BracoD, new Vector3(-75f, 0f, 12f)), (RagBone.AntebracoD, new Vector3(-25f, 0f, 0f)));
        static readonly (RagBone, Vector3)[] PoseQuadruped = L(
            (RagBone.CoxaE, new Vector3(-95f, 0f, 0f)), (RagBone.CanelaE, new Vector3(100f, 0f, 0f)),         // joelhos sob o quadril
            (RagBone.CoxaD, new Vector3(-90f, 0f, 0f)), (RagBone.CanelaD, new Vector3(100f, 0f, 0f)),
            (RagBone.Tronco, new Vector3(0f, 0f, 0f)), (RagBone.Cabeca, new Vector3(30f, 0f, 0f)),           // coluna neutra, olha à frente das mãos
            (RagBone.BracoE, new Vector3(-88f, 0f, -8f)), (RagBone.AntebracoE, new Vector3(-8f, 0f, 0f)),      // mãos sob os ombros
            (RagBone.BracoD, new Vector3(-88f, 0f, 8f)), (RagBone.AntebracoD, new Vector3(-8f, 0f, 0f)));
        static readonly (RagBone, Vector3)[] PoseHalfKneel = L(
            (RagBone.CoxaE, new Vector3(-80f, 0f, 0f)), (RagBone.CanelaE, new Vector3(85f, 0f, 0f)),          // pé da frente plantado, joelho a ~90°
            (RagBone.CoxaD, new Vector3(5f, 0f, 0f)), (RagBone.CanelaD, new Vector3(95f, 0f, 0f)),            // joelho de trás no chão
            (RagBone.Tronco, new Vector3(-15f, 0f, 0f)), (RagBone.Cabeca, new Vector3(15f, 0f, 0f)),
            (RagBone.BracoE, new Vector3(-45f, 0f, -15f)), (RagBone.AntebracoE, new Vector3(-55f, 0f, 0f)),    // mão no joelho da frente
            (RagBone.BracoD, new Vector3(-65f, 0f, 12f)), (RagBone.AntebracoD, new Vector3(-20f, 0f, 0f)));    // a outra ainda perto do chão
        static readonly (RagBone, Vector3)[] PoseRising = L(
            (RagBone.CoxaE, new Vector3(-40f, 0f, 0f)), (RagBone.CanelaE, new Vector3(45f, 0f, 0f)),
            (RagBone.CoxaD, new Vector3(-15f, 0f, 0f)), (RagBone.CanelaD, new Vector3(40f, 0f, 0f)),
            (RagBone.Tronco, new Vector3(-20f, 0f, 0f)), (RagBone.Cabeca, new Vector3(15f, 0f, 0f)),
            (RagBone.BracoE, new Vector3(-30f, 0f, -20f)), (RagBone.AntebracoE, new Vector3(-30f, 0f, 0f)),    // braços ajudam no equilíbrio
            (RagBone.BracoD, new Vector3(-40f, 0f, 20f)), (RagBone.AntebracoD, new Vector3(-40f, 0f, 0f)));

        void BuildGetUp()
        {
            var pel = bones[RagBone.Bacia].Rb;
            var spine = pel.rotation * Vector3.up;                     // de onde a cabeça aponta
            var F = new Vector3(spine.x, 0f, spine.z);
            if (F.sqrMagnitude < 0.05f) F = pel.rotation * Vector3.forward; F.y = 0f;
            F = F.sqrMagnitude > 1e-4f ? F.normalized : Vector3.forward;
            bool onBack = Vector3.Dot(pel.rotation * Vector3.forward, Vector3.up) > 0f;   // peito para cima = de costas
            var right = Vector3.Cross(Vector3.up, F);
            var prone = Quaternion.LookRotation(Vector3.down, F);       // peito para baixo, cabeça para F
            var up = Quaternion.LookRotation(F, Vector3.up);
            var keys = new List<GetUpKey>();
            float t0 = 0f;
            if (onBack)
            {
                var back = Quaternion.LookRotation(Vector3.up, F);     // peito para cima
                var side = Quaternion.LookRotation(-right, F);          // de lado (rola para a esquerda)
                keys.Add(new GetUpKey { T = 0.00f, Height = 0.13f, Pelvis = back, Limbs = PoseLyingBack });
                keys.Add(new GetUpKey { T = 0.12f, Height = 0.14f, Pelvis = back, Limbs = PoseLyingBack });   // joelhos dobram, pés vêm
                keys.Add(new GetUpKey { T = 0.24f, Height = 0.17f, Pelvis = side, Limbs = PoseLyingBack });   // rola de lado
                t0 = 0.34f;
            }
            float S(float x) => t0 + (1f - t0) * x;                    // etapas de barriga no tempo que sobra
            keys.Add(new GetUpKey { T = S(0.00f), Height = 0.13f, Pelvis = prone, Limbs = PoseProneHands });
            keys.Add(new GetUpKey { T = S(0.20f), Height = 0.20f, Pelvis = Quaternion.Slerp(prone, up, 0.25f), Limbs = PosePushUp });
            keys.Add(new GetUpKey { T = S(0.42f), Height = 0.30f, Pelvis = Quaternion.Slerp(prone, up, 0.12f), Limbs = PoseQuadruped });
            keys.Add(new GetUpKey { T = S(0.52f), Height = 0.30f, Pelvis = Quaternion.Slerp(prone, up, 0.12f), Limbs = PoseQuadruped });   // segura de quatro
            keys.Add(new GetUpKey { T = S(0.70f), Height = 0.33f, Pelvis = Quaternion.Slerp(prone, up, 0.7f), Limbs = PoseHalfKneel });
            keys.Add(new GetUpKey { T = S(0.86f), Height = 0.46f, Pelvis = Quaternion.Slerp(prone, up, 0.88f), Limbs = PoseRising });
            keys.Add(new GetUpKey { T = S(0.95f), Height = 0.56f, Pelvis = Quaternion.Slerp(up, Quaternion.LookRotation(F + right * 0.1f, Vector3.up), 0.5f), Limbs = null });   // ajuste de equilíbrio
            keys.Add(new GetUpKey { T = 1.00f, Height = 0.55f, Pelvis = up, Limbs = null });
            getUp = keys.ToArray();
            getUpTotal = onBack ? 2.7f : 2.0f;
            getUpXZ = new Vector3(pel.position.x, 0f, pel.position.z);
        }

        // Pose e caminho da bacia no instante p (0..1).
        void SampleGetUp(float p, out float height, out Quaternion pelvis, out (RagBone, Vector3)[] a, out (RagBone, Vector3)[] b, out float w)
        {
            int i = 0;
            while (i < getUp.Length - 2 && p > getUp[i + 1].T) i++;
            var k0 = getUp[i]; var k1 = getUp[i + 1];
            w = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(k0.T, k1.T, p));
            height = Mathf.Lerp(k0.Height, k1.Height, w);
            pelvis = Quaternion.Slerp(k0.Pelvis, k1.Pelvis, w);
            a = k0.Limbs; b = k1.Limbs;
        }

        void GetUpPose(float p)
        {
            if (getUp == null) return;
            SampleGetUp(p, out _, out _, out var a, out var b, out var w);
            var stand = new Dictionary<RagBone, Vector3>(pose);
            foreach (var id in new[] { RagBone.CoxaE, RagBone.CanelaE, RagBone.CoxaD, RagBone.CanelaD, RagBone.Tronco, RagBone.Cabeca, RagBone.BracoE, RagBone.AntebracoE, RagBone.BracoD, RagBone.AntebracoD })
            {
                var va = Find(a, id, stand); var vb = Find(b, id, stand);
                // Assimetria: o lado direito chega um pouco depois.
                float wi = id == RagBone.CoxaD || id == RagBone.CanelaD || id == RagBone.BracoD || id == RagBone.AntebracoD ? Mathf.Clamp01(w * 1.15f - 0.15f) : w;
                pose[id] = Vector3.Lerp(va, vb, wi);
            }
        }

        static Vector3 Find((RagBone, Vector3)[] l, RagBone id, Dictionary<RagBone, Vector3> stand)
        {
            if (l != null) foreach (var (b, v) in l) if (b == id) return v;
            return stand.TryGetValue(id, out var s) ? s : Vector3.zero;
        }

        // Move o ponto da IA para a bacia seguir o caminho (a bacia fica a PelvisOffset do ponto, no espaço dele).
        void DriveGetUp(float p)
        {
            SampleGetUp(p, out var h, out var rot, out _, out _, out _);
            var pelvisPos = new Vector3(getUpXZ.x, groundY + h * k, getUpXZ.z);
            anchor.MoveRotation(rot);
            anchor.MovePosition(pelvisPos - rot * PelvisOffset);
        }

        void ApplyMuscles()
        {
            foreach (var b in boneList)
            {
                if (!b.Joint || !b.Rb) continue;
                float m = b.Severed ? 0f : muscle;
                if (b.Id != RagBone.Bacia && b.Id != RagBone.Tronco) m *= spec.LimbMuscle;   // esqueleto: ossos soltos, chacoalham
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
            hitCd -= dt; flopT -= dt; footT -= dt;
            var pl = Player;
            if (mode != Mode.Dead)
            {
                if (!Proxied) State.Tick(dt);   // com representante, quem avança o tempo da vida/status é ele
                UpdatePieces(dt);

                if (State.Dead) Die();
            }
            UpdateShield();
            UpdateStabsBleed(dt);      // vivo OU morto: senão o corpo morto nunca soltava da espada ("fantasma grudado")
            UpdatePendingCollisions(dt);
            FollowBodyWhileStuck();
            float speed01 = 0f;
            switch (mode)
            {
                case Mode.Stand:
                    muscle = flopT > 0f ? ActiveRagdollRules.MuscleHit : ActiveRagdollRules.MuscleNormal;
                    speed01 = standT > 0f ? 0f : Showcase ? ShowcaseStep(dt) : pl ? Think(pl, dt) : 0f;   // levantando: não anda nem ataca
                    // Arrastado para longe do ponto da IA (agarrado e puxado, empurrado forte): perde o pé.
                    // (agarrado ou espetado perde o pé mais fácil: dá para levantar do chão)
                    float dragOff = GrabbedOrStuck ? ActiveRagdollRules.DragOffDistanceHeld : ActiveRagdollRules.DragOffDistance;
                    // Levantando (standT > 0) a bacia ainda está no chão, longe do ponto da IA: não conta como "arrastado".
                    // Antes contava, e ele caía de novo no mesmo instante em que levantava (nunca mais ficava de pé).
                    // Golpe que não derruba (ex.: espada na cabeça): o tranco não vale como "arrastado" (antes caíam sempre).
                    if (standT <= 0f && (footT <= 0f || GrabbedOrStuck) && (bones[RagBone.Bacia].Rb.position - anchor.transform.TransformPoint(PelvisOffset)).magnitude > dragOff) KnockDown();
                    break;
                case Mode.Down:
                    muscle = ActiveRagdollRules.MuscleDown;
                    downT -= dt;
                    if (downT <= 0f && canStand && !GrabbedOrStuck && bones[RagBone.Bacia].Rb.linearVelocity.magnitude < 0.6f) StandUp();
                    break;
                case Mode.Dead:
                    muscle = 0f;
                    if (!GrabbedOrStuck) deadT += dt;
                    SettleCorpse();
                    if (deadT > 10f) { Destroy(gameObject); return; }
                    break;
            }
            if (mode == Mode.Stand && standT > 0f)
            {
                standT -= dt;
                float p = 1f - Mathf.Clamp01(standT / getUpTotal);
                SetAnchorStrength(Mathf.Lerp(0.6f, 1f, p));   // firme desde o começo: a bacia segue o caminho das etapas
                DriveGetUp(p);
                if (standT <= 0f) { anchor.MoveRotation(Quaternion.LookRotation(getUp[getUp.Length - 1].Pelvis * Vector3.forward)); anchor.MovePosition(new Vector3(getUpXZ.x, groundY, getUpXZ.z)); }
            }
            else if (mode == Mode.Stand)
            {
                // Espetado ou agarrado: amolece a mola que o segura em pé (dá para arrastar e levantar).
                float want = GrabbedOrStuck ? ActiveRagdollRules.StuckStandStrength : 1f;
                if (!Mathf.Approximately(want, anchorStrength)) SetAnchorStrength(want);
            }
            if (mode == Mode.Stand) UpdateLook(Showcase ? ShowcaseLook + Vector3.up * 1.5f : pl ? pl.HeadPosition : (Vector3?)null, dt);
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
            // Ameaças (projétil vindo, Kamehameha carregando na minha direção): às vezes dá um passo para o lado.
            dodgeT -= dt; threatCheckT -= dt; kameCheckT -= dt;
            if (dodgeT <= 0f && reactDodgeT < 0f && threatCheckT <= 0f && !stuck && attack != Attack.Strike) { threatCheckT = 0.1f; CheckThreats(pl); }
            if (reactDodgeT >= 0f) { reactDodgeT -= dt; if (reactDodgeT < 0f && !stuck && attack != Attack.Strike) StartDodge(reactDodgeDir); }
            circling = false;
            if (dodgeT > 0f && !stuck) { move = dodgeDir * CombatSteering.DodgeSpeed; }
            else if (retreatT > 0f && !stuck) { move = -dir * 1.6f; hasToken = false; tokens.Release(TokenId); }
            else if (attack == Attack.None && !stuck && guardT <= 0f)
            {
                if (hasToken && dist > reach - 0.15f)
                {
                    // Chega em curva e corre de longe (EnemyMotion, 2026-10-29: antes vinha reto).
                    move = dir * 1.3f * EnemyMotion.SpeedFactor(dist);   // vem reto (2026-10-30)
                }
                else if (!hasToken && !EnemyMotion.Engaged(dist))
                {
                    move = dir * 1.3f * EnemyMotion.SpeedFactor(dist);   // longe: vem reto; só ronda/cerca quando chega perto
                }
                else if (!hasToken)
                {
                    // Ronda numa VAGA do arco na frente do jogador (CombatSteering.AssignSlots); parado na vaga, balança de lado.
                    circling = true;
                    // A vaga respira: se afasta e se aproxima (Pace) em volta do raio da ronda.
                    float radius = CombatSteering.SlotRadius - EnemyMotion.Pace(Time.time, paceSeed, dist) * 0.8f;
                    var target = pl.FeetPosition + Quaternion.Euler(0f, MySlotAngle(pl), 0f) * pl.HeadForward * radius;
                    var toSlot = target - anchor.position; toSlot.y = 0f;
                    move = Vector3.ClampMagnitude(toSlot * 1.5f, ActiveRagdollRules.CircleSpeed * 1.6f);
                    strafeT -= dt;
                    if (strafeT <= 0f) { strafeT = Random.Range(1.2f, 2.5f); strafeDir = Random.value < 0.5f ? -1 : 1; }
                    if (toSlot.magnitude < 0.5f) move += Vector3.Cross(Vector3.up, dir) * strafeDir * 0.35f;
                }
            }
            // Passo à frente: avança durante o golpe.
            bool lunging = attack == Attack.Strike && curAttack == EnemyAttack.Passo && attackT < 0.2f;
            if (lunging) move = lungeDir * (ActiveRagdollPoses.StepLunge / 0.2f);
            else if (!stuck) move = Steer(move);   // separação dos vizinhos + desvio dos pilares
            // Acelera e freia (não muda de velocidade de uma vez), inclusive na esquiva; só o avanço do golpe é instantâneo.
            if (!lunging) { smoothMove = Vector3.MoveTowards(smoothMove, move, (move.sqrMagnitude > smoothMove.sqrMagnitude ? EnemyMotion.Accel : EnemyMotion.Decel) * dt); move = smoothMove; }
            else smoothMove = move;
            // Correndo de longe: olha para onde corre (antes corria de lado olhando para você). Perto: olha para você e
            // anda de lado no máximo a MaxStrafe (passo lateral; mais rápido que isso parecia deslizar).
            bool running = hasToken && attack == Attack.None && dist > 3f && move.magnitude > 1.2f;
            var face = running ? new Vector3(move.x, 0f, move.z).normalized : dir;
            if (face.sqrMagnitude > 1e-4f) anchor.MoveRotation(Quaternion.Slerp(anchor.rotation, Quaternion.LookRotation(face), dt * (running ? 4f : 5f)));
            if (!running && !lunging)
            {
                var rightAx = anchor.rotation * Vector3.right;
                float lat = Vector3.Dot(move, rightAx);
                if (Mathf.Abs(lat) > MaxStrafe) move -= rightAx * (lat - Mathf.Sign(lat) * MaxStrafe);
            }
            speed = move.magnitude;
            var localMove = Quaternion.Inverse(anchor.rotation) * move;
            gaitF = localMove.z; gaitS = localMove.x;
            if (move.sqrMagnitude > 1e-6f)
            {
                if (State.Elite == EliteKind.Furioso) move *= EliteRules.FuriousSpeed;   // elite furioso anda mais rápido
                var np = ArenaUtil.ClampToArena(anchor.position + move * dt);
                np.y = baseY + GaitRules.PelvisBob(phase, speed, k);
                anchor.MovePosition(np);
                phase += dt * GaitRules.PhaseRate(speed, k);   // um passo a cada StepLength andado (pé não escorrega)
            }
            // Guarda: a arma do jogador vem rápida perto do corpo.
            guardT -= dt; guardCd -= dt;
            var w = pl.Weapon;
            if (Armed && attack == Attack.None && guardCd <= 0f && w && w.Held && w.TipSpeed > ActiveRagdollRules.GuardTriggerSpeed
                && Vector3.Distance(w.TipPoint, bones[RagBone.Cabeca].Rb.position) < ActiveRagdollRules.GuardTriggerDistance)
            {
                guardCd = ActiveRagdollRules.GuardCooldown;
                if (Random.value < ActiveRagdollRules.GuardChance(Diff)) reactGuardT = CombatSteering.ReactionDelay(Diff, Random.value);
            }
            if (reactGuardT >= 0f) { reactGuardT -= dt; if (reactGuardT < 0f && attack == Attack.None) guardT = ActiveRagdollRules.GuardSeconds; }
            attackCd -= dt;
            if (attack == Attack.None && hasToken && dist <= reach && attackCd <= 0f && guardT <= 0f)
            {
                curAttack = Armed ? EnemyAttacks.Pick(EnemyKind.Goblin, Diff, Random.value) : EnemyAttack.Cima;
                attack = Attack.Windup; attackT = 0f; strikeHit = false;
                feint = Armed && !riposte && Random.value < CombatSteering.FeintChance(Diff);
            }
            if (attack != Attack.None)
            {
                attackT += dt;
                float windup = (Armed ? 1.0f : 0.6f) * DifficultyRules.WindupMultiplier(Diff) * EliteRules.WindupMultiplier(State.Elite) * (riposte ? 0.6f : 1f);
                if (attack == Attack.Windup && feint && attackT > windup * CombatSteering.FeintAt)
                {
                    // Finta: ergueu a arma e desistiu, com um passinho para trás (quem aparou no ar fica exposto).
                    feint = false; attack = Attack.Recover; attackT = 0.35f;
                    dodgeT = 0.3f; dodgeDir = -dir;
                }
                else if (attack == Attack.Windup && attackT > windup)
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
            return speed > 0.05f ? Mathf.Clamp01(0.35f + speed / 1.6f) : 0f;
        }

        const float MaxStrafe = 1.1f;   // m/s de lado

        // Vaga deste inimigo no arco: junta quem está rondando (inimigos físicos de pé) e distribui pela ordem em volta do jogador.
        float MySlotAngle(ArenaPlayer pl)
        {
            var fwd = pl.HeadForward; var feet = pl.FeetPosition;
            var angles = new List<float>(); int me = -1;
            foreach (var g in All)
            {
                if (!g || g.mode != Mode.Stand || g.State.Dead || (!g.circling && g != this)) continue;
                var to = g.FeetPosition - feet; to.y = 0f;
                if (g == this) me = angles.Count;
                angles.Add(Vector3.SignedAngle(fwd, to, Vector3.up));
            }
            if (me < 0) return 0f;
            return CombatSteering.AssignSlots(angles.ToArray())[me];
        }

        // Separação dos vizinhos (outros inimigos de pé) + desvio dos pilares, no plano do chão.
        Vector3 Steer(Vector3 move)
        {
            var self = new System.Numerics.Vector2(anchor.position.x, anchor.position.z);
            neighbors.Clear();
            foreach (var g in All) if (g && g != this && g.mode == Mode.Stand && !g.State.Dead) { var p = g.FeetPosition; neighbors.Add(new System.Numerics.Vector2(p.x, p.z)); }
            foreach (var e in ArenaEnemy.All) if (e && !e.Dead && !(e is PhysicalGoblinEnemy)) { var p = e.transform.position; neighbors.Add(new System.Numerics.Vector2(p.x, p.z)); }
            var desired = new System.Numerics.Vector2(move.x, move.z) + CombatSteering.Separation(self, neighbors);
            desired += CombatSteering.Avoid(self, desired, NavObstacles.Circles, 0.22f * k);
            desired = CombatSteering.ClampSpeed(desired, Mathf.Max(CombatSteering.MaxSpeed, move.magnitude));
            return new Vector3(desired.X, 0f, desired.Y);
        }

        // Projétil vindo na minha direção (a bola teleguiada não conta: foi feita para acertar) ou Kamehameha carregando
        // na minha direção: com a chance da dificuldade, um passo rápido para o lado (cancela o aviso de ataque).
        void CheckThreats(ArenaPlayer pl)
        {
            var e = new System.Numerics.Vector2(anchor.position.x, anchor.position.z);
            if (seenThreats.Count > 30) seenThreats.Clear();
            foreach (var p in Projectile.All)
            {
                if (!p || seenThreats.Contains(p) || p.GetComponent<HomingProjectile>()) continue;
                var pp = p.transform.position;
                if (!CombatSteering.ProjectileThreat(e, new System.Numerics.Vector2(pp.x, pp.z), new System.Numerics.Vector2(p.Velocity.x, p.Velocity.z), out var dd)) continue;
                seenThreats.Add(p);
                if (Random.value < CombatSteering.DodgeChance(Diff)) QueueDodge(new Vector3(dd.X, 0f, dd.Y));
                return;
            }
            var hm = HandMagic.Instance;
            if (hm && hm.KameCharging && kameCheckT <= 0f)
            {
                kameCheckT = 0.7f;
                var toMe = anchor.position - pl.FeetPosition; toMe.y = 0f;
                if (toMe.magnitude < 14f && Vector3.Angle(pl.HeadForward, toMe) < 15f && Random.value < CombatSteering.DodgeChance(Diff))
                {
                    var side = Vector3.Cross(Vector3.up, pl.HeadForward);
                    QueueDodge(Vector3.Dot(toMe, side) >= 0f ? side : -side);
                }
            }
        }

        void QueueDodge(Vector3 dir) { reactDodgeDir = dir; reactDodgeT = CombatSteering.ReactionDelay(Diff, Random.value); }

        void StartDodge(Vector3 dir)
        {
            dodgeT = CombatSteering.DodgeSeconds; dodgeDir = dir.normalized;
            if (attack == Attack.Windup) { attack = Attack.None; ArenaGame.Tokens.Release(TokenId); }
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
            var w = pl.WeaponOf(c.rigidbody);   // direita ou esquerda
            if (!w || !w.Body) return;
            Bone bone = null;
            // Clava e escudo não são ossos (antes o escudo caía aqui e nunca rachava — teste de 2026-10-31).
            if (c.contactCount == 0 || (id != RagBone.Clava && id != RagBone.Escudo && !bones.TryGetValue(id, out bone))) return;
            WeaponContact(pl, w, id, bone, c.GetContact(0).point, c.relativeVelocity, slicing: false);
        }

        // Arma em chamas atravessando o corpo (FlameBlade: sem colisão física, detectado por sobreposição da lâmina).
        public void OnWeaponSlice(RagBone id, Vector3 contact)
        {
            var pl = Player;
            if (!pl || id == RagBone.Clava) return;
            var w = pl.Weapon;
            if (!w || !w.Body || !bones.TryGetValue(id, out var bone)) return;
            WeaponContact(pl, w, id, bone, contact, w.PhysTipVelocity, slicing: true);
        }

        // Colisores do corpo (e da clava): FlameBlade ignora a colisão deles com a arma enquanto ela queima.
        public List<Collider> Colliders() => BodyColliders();

        void WeaponContact(ArenaPlayer pl, PlayerWeapon w, RagBone id, Bone bone, Vector3 contact, Vector3 relVel, bool slicing)
        {
            float speed = relVel.magnitude;
            if (id == RagBone.Escudo) { ShieldHit(pl, w, speed, contact); return; }
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
                else if (guardT > 0f && RedAura.Active)
                {
                    // Aura vermelha: a guarda não segura — atordoa e abre a defesa.
                    guardT = 0f;
                    State.ApplyStun(RedAuraRules.GuardBreakStun);
                    ArenaFx.FloatText("DEFESA QUEBRADA!", contact + Vector3.up * 0.25f, new Color(1f, 0.3f, 0.25f), 1f);
                    ArenaFx.Burst(contact, new Color(1f, 0.25f, 0.15f), 14, 3f);
                    ArenaAudio.Play(Sfx.Broken, contact, 0.8f, 1.1f);
                    ArenaInput.Haptic(0.8f, 0.08f);
                }
                else if (guardT > 0f)
                {
                    ArenaFx.FloatText("BLOQUEOU", contact + Vector3.up * 0.2f, new Color(0.8f, 0.8f, 0.8f), 0.8f);
                    ArenaAudio.Play(Sfx.Block, contact, 0.9f);
                    ArenaInput.Haptic(0.5f, 0.06f);
                    if (speed >= ActiveRagdollRules.DisarmSpeed * 1.6f) Disarm(-relVel);   // pancada muito forte na guarda arranca a clava
                }
                else if (ActiveRagdollRules.Disarms(id, speed / spec.DisarmMultiplier)) Disarm(-relVel);
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
            var vel = tipVel.sqrMagnitude > relVel.sqrMagnitude ? tipVel : relVel;
            speed = Mathf.Max(speed, tipSpeed);
            float align = tipSpeed > 0.01f ? Vector3.Dot(bladeDir, tipVel / tipSpeed) : 0f;
            var tipWorld = w.Body.position + w.Body.rotation * new Vector3(0f, 0f, def.Length);
            float fromTip = Vector3.Distance(contact, tipWorld);
            if (PlayerWeapon.DebugStab) Debug.Log($"[GoblinFisico] contato {id} vel={tipSpeed:0.0} alinh={align:0.00} ponta={fromTip:0.00}");
            // Perfurar (a lâmina em chamas passa direto: não crava)
            if (!slicing && StabRules.Enabled && StabRules.CanPierce(def.Kind) && ActiveRagdollRules.CanStab(tipSpeed, align, fromTip) && w.Held && !IsStuckBy(w))
            {
                Stab(bone, contact, w, tipSpeed);
                return;
            }
            if (speed < Combat.MinSwingSpeed) return;
            hitCd = 0.25f;
            float edge = Geometry.EdgeAlignment(bladeDir.ToN(), vel.ToN());
            // Desmembrar; golpe forte no braço da clava desarma (B&S: mão atingida solta a arma)
            bool canSever = !spec.SeverNeedsHeavy || def.Weight > 5f;   // orc: só arma pesada arranca membro
            var dtype = DamageTypes.Classify(def.Kind, edge, align);
            // Cabeça (e o alto do tronco, no pescoço): regra por região — HeadHitRules.
            bool headHit = id == RagBone.Cabeca || (id == RagBone.Tronco && NearNeck(contact));
            var zone = headHit ? HeadZoneAt(contact) : HeadZone.Cranio;
            // Aura vermelha quebra o capacete (o golpe já pega a cabeça sem proteção).
            if (headHit && helmet && zone != HeadZone.Pescoco && RedAura.Active && speed >= HeadHitRules.MinSpeed && mode != Mode.Dead) BreakHelmet(vel);
            var headOut = HeadOutcome.Nada;
            if (headHit)
            {
                bool decap = canSever && spec.HeadKills && DismemberRules.Decapitates(Kind, dtype, true, speed / spec.SeverMultiplier, edge);
                if (!spec.HeadKills) decap = canSever && !def.Blunt && edge >= DismemberRules.DecapMinEdge && speed / spec.SeverMultiplier >= DismemberRules.DecapMinSpeed;
                headOut = HeadHitRules.Resolve(zone, def.Blunt, helmet && zone != HeadZone.Pescoco, Kind == EnemyKind.Orc, spec.Reassembles,
                                               decap, speed, edge, Random.value);
            }
            else if (canSever && ActiveRagdollRules.Severs(id, speed / spec.SeverMultiplier, edge, def.Blunt)) { Sever(bone, vel); }
            if (!headHit && Armed && !bone.Severed && ActiveRagdollRules.Disarms(id, speed / spec.DisarmMultiplier)) Disarm(vel);
            // Tipo de dano × material (maça no osso, machado/estocada na armadura) — igual aos inimigos antigos.
            float zoneMul = headHit ? HeadHitRules.DamageMultiplier(zone, def.Blunt, helmet && zone != HeadZone.Pescoco) : ActiveRagdollRules.DamageMultiplier(id);
            float dmg = Combat.MeleeDamage(speed, def, Combat.SharpEdge(pl.State.Has(SkillIds.FioAfiado), def), true, def.Blunt ? 1f : edge)
                        * zoneMul * DamageTypes.Multiplier(DamageTypes.MaterialOf(Kind), dtype);
            dmg *= FlameBlade.DamageMultiplier;   // arma em chamas: +30% e põe calor
            dmg *= RedAura.DamageMultiplier;   // aura vermelha (as duas mãos)
            FlameBlade.OnHit(State);
            if (def.HeatPerHit > 0f && mode != Mode.Dead) State.Status.AddHeat(def.HeatPerHit);   // tocha
            if (def.Funny) WeaponModels.FunnyHit(def, contact);
            if (dmg > 0f && mode != Mode.Dead) Damage(dmg, contact);
            flopT = ActiveRagdollRules.HitFlopSeconds;
            bool fell = false;
            if (headHit && mode != Mode.Dead)
            {
                if (zone == HeadZone.Rosto && !def.Blunt) ArenaFx.FloatText("CORTOU!", contact + Vector3.up * 0.25f, new Color(1f, 0.4f, 0.35f), 0.8f);
                switch (headOut)
                {
                    case HeadOutcome.Decapita: Sever(bones[RagBone.Cabeca], vel); break;
                    case HeadOutcome.Mata:
                        ArenaFx.FloatText("JUGULAR!", contact + Vector3.up * 0.35f, new Color(1f, 0.2f, 0.2f), 1.2f);
                        ArenaFx.Burst(contact, new Color(0.7f, 0.08f, 0.12f), 22, 3f, 1.2f);
                        ArenaInput.Haptic(1f, 0.12f);
                        State.TakeDamage(State.Hp + 1f, 0f, false); Die();
                        break;
                    case HeadOutcome.Atordoa:
                        State.ApplyStun(HeadHitRules.StunSeconds);
                        attack = Attack.None; guardT = 0f;
                        ArenaFx.FloatText("ATORDOADO!", Center + Vector3.up * 0.55f, new Color(1f, 1f, 0.55f), 1f);
                        break;
                    case HeadOutcome.Derruba: if (mode == Mode.Stand) { KnockDown(); fell = true; } break;
                }
            }
            else if (!headHit)
            {
                // Tronco/bacia: arma pesada ou de pancada derruba (como antes).
                bool bluntHit = def.Blunt || dtype == DamageType.Contusao;   // concussão: arma de pancada, ou bater sem o fio
                if (mode == Mode.Stand && ActiveRagdollRules.Knocksdown(id, speed / spec.KnockdownMultiplier, def.Weight > 5f, bluntHit, false)) { KnockDown(); fell = true; }
            }
            if (!fell) footT = 0.7f;
            ArenaAudio.Play(def.Weight > 5f ? Sfx.HitHeavy : Sfx.Hit, contact, 0.4f + speed / 10f);
        }

        bool CanHitCorpse => false;

        // ---------- Escudo (orc da sala 5; roteiro da demo) ----------
        // Fica na frente do peito, do lado esquerdo, seguindo o corpo (cinemático: a arma do jogador bate e PARA nele, então
        // o golpe não chega no corpo). Cada golpe racha (EnemyShieldState): com a aura vermelha quebra em 3-4.
        EnemyShieldState shieldState;
        Rigidbody shieldRb;
        Transform shieldCracks;
        int shieldStage;
        public bool HasShield => shieldRb && shieldState != null && !shieldState.Broken;
        public void ForceShield() { if (!shieldRb) BuildShield(); }

        void BuildShield()
        {
            shieldState = new EnemyShieldState();
            var go = new GameObject("Escudo");
            go.transform.SetPositionAndRotation(ShieldTargetPos(), ShieldTargetRot());
            shieldRb = go.AddComponent<Rigidbody>();
            shieldRb.isKinematic = true; shieldRb.mass = 3f;
            shieldRb.interpolation = RigidbodyInterpolation.Interpolate;
            var col = go.AddComponent<BoxCollider>();
            col.size = new Vector3(0.44f, 0.56f, 0.06f) * k;
            var plank = ArenaUtil.Lit(new Color(0.5f, 0.33f, 0.17f));
            var iron = ArenaUtil.Lit(new Color(0.4f, 0.42f, 0.48f), new Color(0.04f, 0.04f, 0.05f));
            for (int i = 0; i < 3; i++)   // 3 tábuas
                ArenaUtil.Prim(PrimitiveType.Cube, go.transform, new Vector3((i - 1) * 0.148f, 0f, 0f) * k, new Vector3(0.142f, 0.54f, 0.045f) * k, plank);
            foreach (float y in new[] { -0.2f, 0.2f })
                ArenaUtil.Prim(PrimitiveType.Cube, go.transform, new Vector3(0f, y, 0.03f) * k, new Vector3(0.46f, 0.05f, 0.02f) * k, iron);   // faixas de ferro
            ArenaUtil.Prim(PrimitiveType.Sphere, go.transform, new Vector3(0f, 0f, 0.04f) * k, new Vector3(0.1f, 0.1f, 0.05f) * k, iron);
            foreach (var c in go.GetComponentsInChildren<Collider>()) if (c != col) Destroy(c);
            shieldCracks = new GameObject("Rachaduras").transform;
            shieldCracks.SetParent(go.transform, false);
            go.AddComponent<RagdollBone>().Init(this, RagBone.Escudo);
            foreach (var bc in BodyColliders()) if (bc != col) Physics.IgnoreCollision(col, bc);
        }

        Vector3 ShieldTargetPos()
        {
            var t = bones[RagBone.Tronco].Rb;
            var rot = anchor ? anchor.rotation : transform.rotation;
            return t.position + rot * (new Vector3(-0.08f, -0.02f, 0.3f) * k);
        }
        Quaternion ShieldTargetRot() => anchor ? anchor.rotation : transform.rotation;

        void UpdateShield()
        {
            if (!shieldRb || shieldState == null) return;
            if (mode == Mode.Stand && !shieldState.Broken)
            {
                if (!shieldRb.isKinematic) { shieldRb.isKinematic = true; }   // levantou: pega o escudo de novo
                shieldRb.MovePosition(ShieldTargetPos());
                shieldRb.MoveRotation(ShieldTargetRot());
            }
            else if (shieldRb.isKinematic) shieldRb.isKinematic = false;   // caído ou morto: o escudo cai junto
        }

        void ShieldHit(ArenaPlayer pl, PlayerWeapon w, float speed, Vector3 contact)
        {
            if (!HasShield || hitCd > 0f) return;
            speed = Mathf.Max(speed, w.PhysTipVelocity.magnitude);
            if (speed < EnemyShieldState.MinSpeed) return;
            hitCd = 0.25f;
            bool aura = RedAura.Active;
            bool broke = shieldState.Hit(speed, aura, w.Def.Weight > 5f, w.Def.Blunt);
            ArenaAudio.Play(Sfx.Block, contact, 0.9f, aura ? 0.75f : 1f);
            ArenaFx.Burst(contact, aura ? new Color(1f, 0.3f, 0.2f) : new Color(0.75f, 0.55f, 0.3f), aura ? 14 : 6, 2.5f);
            ArenaInput.Haptic(aura ? 0.8f : 0.5f, 0.06f, left: w.IsLeft);
            State.Posture.Add(aura ? 18f : 6f);
            if (broke) { BreakShield(w.TipVelocity); return; }
            int stage = shieldState.Stage;
            if (stage != shieldStage) { shieldStage = stage; AddCracks(stage); ArenaFx.FloatText(stage >= 3 ? "VAI QUEBRAR!" : "RACHOU!", contact + Vector3.up * 0.25f, new Color(1f, 0.75f, 0.4f), 0.8f); }
            else ArenaFx.FloatText(aura ? "TRINCOU" : "BLOQUEOU", contact + Vector3.up * 0.2f, new Color(0.85f, 0.85f, 0.85f), 0.6f);
        }

        // Rachaduras escuras aparecendo na frente do escudo, mais a cada etapa.
        void AddCracks(int stage)
        {
            var dark = ArenaUtil.Unlit(new Color(0.08f, 0.04f, 0.02f));
            int n = stage * 3;
            while (shieldCracks.childCount < n)
            {
                var c = ArenaUtil.Prim(PrimitiveType.Cube, shieldCracks, new Vector3(Random.Range(-0.17f, 0.17f), Random.Range(-0.22f, 0.22f), 0.035f) * k,
                                       new Vector3(0.012f, Random.Range(0.08f, 0.2f), 0.01f) * k, dark).transform;
                c.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-60f, 60f));
                foreach (var col in c.GetComponents<Collider>()) Destroy(col);
            }
        }

        // Quebrou: as tábuas voam em pedaços e somem.
        void BreakShield(Vector3 hitVel)
        {
            var at = shieldRb.position;
            ArenaFx.FloatText("ESCUDO QUEBROU!", at + Vector3.up * 0.4f, new Color(1f, 0.35f, 0.25f), 1.3f);
            ArenaFx.Burst(at, new Color(0.6f, 0.4f, 0.2f), 30, 4f, 1.3f);
            ArenaAudio.Play(Sfx.Broken, at, 1f, 0.8f);
            ArenaInput.Haptic(1f, 0.15f);
            var plank = ArenaUtil.Lit(new Color(0.5f, 0.33f, 0.17f));
            for (int i = 0; i < 4; i++)
            {
                var piece = ArenaUtil.Prim(PrimitiveType.Cube, null, at + shieldRb.rotation * (new Vector3((i - 1.5f) * 0.11f, Random.Range(-0.15f, 0.15f), 0f) * k),
                                           new Vector3(0.1f, Random.Range(0.2f, 0.35f), 0.04f) * k, plank, keepCollider: true);
                piece.transform.rotation = shieldRb.rotation * Quaternion.Euler(0f, 0f, Random.Range(-40f, 40f));
                var rb = piece.AddComponent<Rigidbody>();
                rb.mass = 0.6f;
                rb.linearVelocity = hitVel.normalized * Random.Range(1.5f, 3.5f) + Random.insideUnitSphere * 1.5f + Vector3.up * 1.5f;
                rb.angularVelocity = Random.insideUnitSphere * 8f;
                foreach (var bc in BodyColliders()) foreach (var pc in piece.GetComponents<Collider>()) Physics.IgnoreCollision(pc, bc);
                Destroy(piece, 5f);
            }
            Destroy(shieldRb.gameObject);
            shieldRb = null;
            // Sem escudo: fica um instante desequilibrado (janela para punir).
            State.ApplyStun(0.8f);
        }

        // Região da cabeça: altura a partir da junta do pescoço e quanto está para a frente da cara.
        Vector3 NeckPoint()
        {
            var h = bones[RagBone.Cabeca];
            return h.Rb.transform.TransformPoint(h.AnchorLocal);
        }
        HeadZone HeadZoneAt(Vector3 contact)
        {
            var h = bones[RagBone.Cabeca];
            var neck = NeckPoint();
            var center = h.Rb.worldCenterOfMass;
            var upv = center - neck;
            float r = Mathf.Max(0.03f, upv.magnitude);
            upv /= r;
            float up01 = Vector3.Dot(contact - neck, upv) / (2f * r);
            var face = Vector3.ProjectOnPlane(h.Rb.transform.rotation * faceLocal, upv).normalized;
            float fwd = Vector3.Dot(contact - center, face) / r;
            return HeadHitRules.Zone(up01, fwd);
        }
        // Golpe no alto do tronco colado no pescoço também é pescoço.
        bool NearNeck(Vector3 contact) => !bones[RagBone.Cabeca].Severed && Vector3.Distance(contact, NeckPoint()) < 0.09f * k;

        void Damage(float dmg, Vector3 at)
        {
            var ev = State.TakeDamage(dmg, 0f, false);
            ArenaFx.FloatText(Mathf.RoundToInt(ev.Amount).ToString(), at + Vector3.up * 0.2f, new Color(1f, 0.85f, 0.4f), 0.8f);
            ArenaFx.Burst(at, new Color(0.7f, 0.12f, 0.16f), 8, 2f);
            if (State.Dead) Die();
        }

        // Capacete: protege a cabeça (não arranca, metade do dano) e um golpe forte nele derruba.
        bool helmet;
        // Design estilizado e colorido (2026-10-30, Pablo: o elmo cinza "ficou uma bosta"), um por tipo, legível de longe:
        // goblin = panela de bronze com faixa vermelha, protetor de nariz e pino; esqueleto = ferro com crista vermelha;
        // orc = ferro escuro com faixa dourada e dois chifres.
        void BuildHelmet()
        {
            var head = bones[RagBone.Cabeca].Rb.transform;
            var b = new Bounds(head.position, Vector3.zero);
            foreach (var r in head.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
            // As orelhas do goblin alargam o x: usa a menor medida do crânio.
            float s = Mathf.Clamp(Mathf.Min(b.size.x, b.size.z) * 1.2f, 0.18f * k, 0.34f * k);
            var root = new GameObject("Elmo").transform;
            root.SetParent(head, false);
            root.position = b.center + Vector3.up * b.extents.y * 0.32f;
            root.rotation = Quaternion.LookRotation(transform.forward);
            root.localScale = Vector3.one / Mathf.Max(0.01f, head.lossyScale.x);
            void Part(PrimitiveType t, Vector3 pos, Vector3 size, Material m, Vector3 euler = default)
            {
                var tr = ArenaUtil.Prim(t, root, pos * s, size * s, m).transform;
                tr.localRotation = Quaternion.Euler(euler);
            }
            var kind = State.Def.Kind;
            if (kind == EnemyKind.Orc)
            {
                var iron = ArenaUtil.Lit(new Color(0.32f, 0.34f, 0.42f), new Color(0.03f, 0.03f, 0.05f));
                var gold = ArenaUtil.Lit(new Color(1f, 0.78f, 0.25f), new Color(0.25f, 0.16f, 0.02f));
                var horn = ArenaUtil.Lit(new Color(0.96f, 0.9f, 0.74f));
                Part(PrimitiveType.Sphere, Vector3.zero, new Vector3(1f, 0.72f, 1f), iron);
                Part(PrimitiveType.Cylinder, new Vector3(0f, -0.08f, 0f), new Vector3(1.06f, 0.05f, 1.06f), gold);
                for (int side = -1; side <= 1; side += 2)
                {
                    Part(PrimitiveType.Cylinder, new Vector3(0.5f * side, 0.18f, 0f), new Vector3(0.14f, 0.22f, 0.14f), horn, new Vector3(0f, 0f, -55f * side));
                    Part(PrimitiveType.Sphere, new Vector3(0.68f * side, 0.36f, 0f), new Vector3(0.11f, 0.11f, 0.11f), horn);
                }
            }
            else if (kind == EnemyKind.Esqueleto)
            {
                var iron = ArenaUtil.Lit(new Color(0.55f, 0.6f, 0.68f), new Color(0.04f, 0.05f, 0.06f));
                var plume = ArenaUtil.Lit(new Color(0.92f, 0.18f, 0.22f), new Color(0.2f, 0.02f, 0.03f));
                Part(PrimitiveType.Sphere, Vector3.zero, new Vector3(1f, 0.7f, 1f), iron);
                Part(PrimitiveType.Cylinder, new Vector3(0f, -0.08f, 0f), new Vector3(1.05f, 0.04f, 1.05f), iron);
                Part(PrimitiveType.Cube, new Vector3(0f, 0.38f, -0.05f), new Vector3(0.1f, 0.3f, 0.85f), plume);
                Part(PrimitiveType.Cube, new Vector3(0f, -0.2f, 0.5f), new Vector3(0.07f, 0.4f, 0.06f), iron);
            }
            else
            {
                var bronze = ArenaUtil.Lit(new Color(0.88f, 0.6f, 0.24f), new Color(0.14f, 0.07f, 0.01f));
                var band = ArenaUtil.Lit(new Color(0.8f, 0.16f, 0.12f));
                Part(PrimitiveType.Sphere, Vector3.zero, new Vector3(1f, 0.7f, 1f), bronze);
                Part(PrimitiveType.Cylinder, new Vector3(0f, -0.07f, 0f), new Vector3(1.05f, 0.05f, 1.05f), band);
                Part(PrimitiveType.Cube, new Vector3(0f, -0.2f, 0.5f), new Vector3(0.08f, 0.42f, 0.06f), bronze);
                Part(PrimitiveType.Cylinder, new Vector3(0f, 0.4f, 0f), new Vector3(0.07f, 0.1f, 0.07f), bronze);
                Part(PrimitiveType.Sphere, new Vector3(0f, 0.52f, 0f), new Vector3(0.13f, 0.13f, 0.13f), band);
                for (int i = 0; i < 6; i++)
                {
                    float a = i * Mathf.PI / 3f;
                    Part(PrimitiveType.Sphere, new Vector3(Mathf.Sin(a) * 0.52f, -0.07f, Mathf.Cos(a) * 0.52f), Vector3.one * 0.07f, bronze);
                }
            }
        }

        // Aura vermelha: o elmo racha e as peças voam (física), a cabeça fica sem proteção.
        void BreakHelmet(Vector3 hitVel)
        {
            helmet = false;
            var root = bones[RagBone.Cabeca].Rb.transform.Find("Elmo");
            if (!root) return;
            var at = root.position;
            ArenaFx.FloatText("CAPACETE QUEBROU!", at + Vector3.up * 0.35f, new Color(1f, 0.35f, 0.25f), 1.2f);
            ArenaFx.Burst(at, new Color(1f, 0.3f, 0.2f), 20, 3.5f, 1f);
            ArenaAudio.Play(Sfx.Broken, at, 1f, 1.2f);
            ArenaInput.Haptic(0.9f, 0.1f);
            var cols = BodyColliders();
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>())
            {
                var piece = r.gameObject;
                piece.transform.SetParent(null, true);
                var bc = piece.AddComponent<BoxCollider>();
                foreach (var c in cols) Physics.IgnoreCollision(bc, c);
                var rb = piece.AddComponent<Rigidbody>();
                rb.mass = 0.3f;
                rb.linearVelocity = hitVel.normalized * Random.Range(1.5f, 3f) + Random.insideUnitSphere * 1.5f + Vector3.up * 2f;
                rb.angularVelocity = Random.insideUnitSphere * 10f;
                Destroy(piece, 4f);
            }
            Destroy(root.gameObject);
        }

        void KnockDown()
        {
            if (mode != Mode.Stand) return;
            mode = Mode.Down; downT = ActiveRagdollRules.DownSeconds; attack = Attack.None; guardT = 0f; standT = 0f;
            ArenaGame.Tokens.Release(TokenId);
            SetAnchorStrength(0f);
            ArenaFx.FloatText("CAIU!", Center + Vector3.up * 0.5f, new Color(1f, 0.85f, 0.4f), 0.9f);
        }

        float groundY, baseY;   // baseY = chão onde nasceu (o ponto da IA desce enquanto levanta; não pode acumular)

        // ---------- Vitrine (Sala de Criação): sem IA; anda com a velocidade dada e olha para um ponto ----------
        public bool Showcase;
        public Vector3 ShowcaseVelocity, ShowcaseLook;
        public bool Standing => mode == Mode.Stand && standT <= 0f;
        public void ForceKnockDown() => KnockDown();
        public void ForceHelmet() { if (!helmet) { helmet = true; BuildHelmet(); } }

        float ShowcaseStep(float dt)
        {
            var face = ShowcaseLook - anchor.position; face.y = 0f;
            if (face.sqrMagnitude > 1e-4f) anchor.MoveRotation(Quaternion.Slerp(anchor.rotation, Quaternion.LookRotation(face.normalized), dt * 5f));
            smoothMove = Vector3.MoveTowards(smoothMove, ShowcaseVelocity, EnemyMotion.Accel * dt);
            var move = smoothMove;
            float speed = move.magnitude;
            var lm = Quaternion.Inverse(anchor.rotation) * move;
            gaitF = lm.z; gaitS = lm.x;
            if (speed > 1e-3f)
            {
                var np = ArenaUtil.ClampToArena(anchor.position + move * dt);
                np.y = baseY + GaitRules.PelvisBob(phase, speed, k);
                anchor.MovePosition(np);
                phase += dt * GaitRules.PhaseRate(speed, k);
            }
            return speed > 0.05f ? Mathf.Clamp01(0.35f + speed / 1.6f) : 0f;
        }

        void StandUp()
        {
            groundY = baseY;
            BuildGetUp();
            // Começa onde a bacia está (sem puxão): o ponto da IA vai para a primeira pose-chave.
            var k0 = getUp[0];
            anchor.rotation = k0.Pelvis;
            anchor.position = new Vector3(getUpXZ.x, groundY + k0.Height * k, getUpXZ.z) - k0.Pelvis * PelvisOffset;
            mode = Mode.Stand; standT = getUpTotal; attackCd = 1f;
        }

        // Corpo morto (Pablo, 2026-10-30: "alguns bugam, saem voando e travam"): ossos sem força nenhuma ficam leves
        // diante da arma do jogador (presa à mão por mola forte) e das colisões encavaladas — o motor de física os
        // empurrava para fora em alta velocidade e eles tremiam. Agora: saída de colisão lenta, mais iterações do solver,
        // limite de velocidade e, parado, o corpo assenta.
        public const float CorpseMaxSpeed = 10f, CorpseMaxSpin = 18f;
        void PrepareCorpse()
        {
            foreach (var b in boneList)
            {
                if (!b.Rb) continue;
                b.Rb.maxDepenetrationVelocity = 1f;
                b.Rb.solverIterations = 12; b.Rb.solverVelocityIterations = 4;
                b.Rb.angularDamping = Mathf.Max(b.Rb.angularDamping, 1.5f);
                b.Rb.sleepThreshold = 0.05f;
            }
        }
        void SettleCorpse()
        {
            bool still = deadT > 1.2f;
            foreach (var b in boneList)
            {
                var rb = b.Rb;
                if (!rb || rb.isKinematic) continue;
                if (rb.linearVelocity.sqrMagnitude > CorpseMaxSpeed * CorpseMaxSpeed) rb.linearVelocity = rb.linearVelocity.normalized * CorpseMaxSpeed;
                if (rb.angularVelocity.sqrMagnitude > CorpseMaxSpin * CorpseMaxSpin) rb.angularVelocity = rb.angularVelocity.normalized * CorpseMaxSpin;
                if (still && rb.linearVelocity.sqrMagnitude < 0.25f) rb.linearDamping = 2f;   // quase parado: assenta (sem tremer)
                else rb.linearDamping = 0.05f;
            }
        }

        void Die()
        {
            if (mode == Mode.Dead) return;
            mode = Mode.Dead; attack = Attack.None;
            PrepareCorpse();
            // Morreu espetado: o corpo fica mais leve para a espada conseguir segurar pendurado (igual ao inimigo antigo).
            if (stabs.Count > 0) foreach (var b in boneList) if (b.Rb) b.Rb.mass *= StabRules.PinnedCorpseMassScale;
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
            // Perna arrancada mata (Pablo, 2026-10-30: caído sem perna ficava vivo e paralisado no chão). O esqueleto não:
            // ela volta a se encaixar em 4 s e ele levanta.
            if (ActiveRagdollRules.IsLeg(b.Id) && !spec.Reassembles) { State.TakeDamage(State.Hp + 1f, 0f, false); Die(); return; }
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
        sealed class StabLink { public Bone Bone; public ConfigurableJoint Joint; public PlayerWeapon Weapon; public bool Deep; public int Frames; public float Age; }
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
            // A junta fica na ESPADA (não no osso): assim o eixo de deslize é sempre a lâmina. Montada no osso, o eixo girava
            // junto com o corpo e ele "escorregava" por uma linha fora da lâmina (o fantasma grudado).
            var j = vis.gameObject.AddComponent<ConfigurableJoint>();
            j.connectedBody = b.Rb;
            j.autoConfigureConnectedAnchor = false;
            // Ponto do osso preso na lâmina: começa 15 cm dentro (a junta nova se acomoda uns centímetros no 1º passo).
            float startZ = Mathf.Min(w.Def.Length - ActiveRagdollRules.StabStartDepth, (Quaternion.Inverse(w.Body.rotation) * (contact - w.Body.position)).z);
            var onBlade = w.Body.position + w.Body.rotation * new Vector3(0f, 0f, startZ);
            // Pose física dos corpos (o transform de corpo interpolado fica atrasado durante a física).
            j.anchor = new Vector3(0f, 0f, startZ);                                   // na lâmina (espaço da espada)
            j.connectedAnchor = Quaternion.Inverse(b.Rb.rotation) * (onBlade - b.Rb.position);   // no osso
            j.axis = Vector3.right; j.secondaryAxis = Vector3.up;                    // z da junta = eixo da lâmina
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
            anyPulling = false;
            for (int i = stabs.Count - 1; i >= 0; i--)
            {
                var s = stabs[i];
                var w = s.Weapon;
                bool gone = !s.Joint || !w || !w.Body || !w.Held || s.Bone == null || !s.Bone.Rb;
                if (!gone)
                {
                    var vis = w.Visual;
                    // Pose FÍSICA (Rigidbody), não o transform: com interpolação o transform no passo de física está atrasado.
                    var p = s.Bone.Rb.position + s.Bone.Rb.rotation * s.Joint.connectedAnchor;
                    float z = (Quaternion.Inverse(w.Body.rotation) * (p - w.Body.position)).z;
                    // Freio de entrada/saída pela velocidade relativa ao longo da lâmina.
                    var axis = w.Body.rotation * Vector3.forward;
                    float vAxial = Vector3.Dot(s.Bone.Rb.GetPointVelocity(p) - w.Body.GetPointVelocity(p), axis);   // > 0: osso indo para a ponta (saindo)
                    // Jogador puxando a espada para trás: sai fácil (freio baixo). Corpo escorregando sozinho: freio alto.
                    bool pulling = Vector3.Dot(w.Body.linearVelocity - w.RigVelocity, axis) < -ActiveRagdollRules.PullOutSpeed;
                    anyPulling |= pulling;
                    float damper = pulling ? ActiveRagdollRules.StabDamperPull : vAxial > 0f ? ActiveRagdollRules.StabDamperOut : ActiveRagdollRules.StabDamperIn;
                    if (Mathf.Abs(s.Joint.zDrive.positionDamper - damper) > 1f)
                        s.Joint.zDrive = new JointDrive { positionSpring = 0f, positionDamper = damper, maximumForce = float.MaxValue };
                    // A ponta passou para fora do osso = soltou. (Antes havia uma "trava na ponta" que só liberava se o jogador
                    // puxasse rápido — puxando devagar o goblin ficava grudado na ponta, como um fantasma.) Para o corpo não
                    // escorregar sozinho, o ponto da IA segue o corpo enquanto está espetado (FollowBodyWhileStuck).
                    float L = w.Def.Length;
                    s.Age += dt;
                    if (z > L + 0.01f && s.Age > ActiveRagdollRules.StabSettleSeconds) { gone = true; if (PlayerWeapon.DebugStab) Debug.Log($"[GoblinFisico] ponta saiu z={z:0.000} L={L}"); }
                    float lockZ = z < ActiveRagdollRules.StabGuard ? ActiveRagdollRules.StabGuard : -1f;   // para na guarda
                    if (lockZ >= 0f)
                    {
                        if (s.Joint.zMotion != ConfigurableJointMotion.Locked)
                        {
                            s.Joint.anchor = new Vector3(0f, 0f, lockZ);
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
                if (gone && PlayerWeapon.DebugStab) Debug.Log($"[GoblinFisico] soltou: joint={(bool)s.Joint} held={(w && w.Held)} rb={(s.Bone != null && s.Bone.Rb)}");
                if (gone) { ReleaseStab(s); stabs.RemoveAt(i); continue; }
                if (mode != Mode.Dead)
                {
                    bleedAcc += ActiveRagdollRules.BleedDps * dt;
                    if (bleedAcc >= 4f) { State.TakeDamage(bleedAcc, 0f, false); bleedAcc = 0f; }
                }
            }
        }

        // Solta a junta na hora, mas só volta a colidir arma × corpo 0,5 s depois — a lâmina ainda está dentro dele e
        // religar a colisão na hora dava um tranco que "bugava tudo".
        void ReleaseStab(StabLink s)
        {
            if (s.Joint) Destroy(s.Joint);
            if (s.Weapon && s.Weapon.Visual) pendingCollisions.Add((s.Weapon, 0.5f));
        }

        bool anyPulling;
        readonly List<(PlayerWeapon w, float t)> pendingCollisions = new List<(PlayerWeapon, float)>();

        void UpdatePendingCollisions(float dt)
        {
            for (int i = pendingCollisions.Count - 1; i >= 0; i--)
            {
                var (w, t) = pendingCollisions[i];
                t -= dt;
                if (t > 0f && w) { pendingCollisions[i] = (w, t); continue; }
                pendingCollisions.RemoveAt(i);
                if (!w || !w.Visual || IsStuckBy(w)) continue;   // espetou de novo nesse meio tempo: continua atravessando
                var body = BodyColliders();
                foreach (var wc in w.Visual.GetComponents<Collider>()) foreach (var bc in body) if (bc) Physics.IgnoreCollision(wc, bc, false);
            }
        }

        // Espetado: o ponto da IA acompanha a bacia (a mola de ficar em pé não empurra o corpo ao longo da lâmina).
        void FollowBodyWhileStuck()
        {
            if (stabs.Count == 0 || mode != Mode.Stand || !anchor || anyPulling) return;   // puxando: os pés resistem
            var p = bones[RagBone.Bacia].Rb.position - anchor.rotation * PelvisOffset;
            anchor.MovePosition(new Vector3(p.x, anchor.position.y, p.z));
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
            if (shieldRb) Destroy(shieldRb.gameObject);
            foreach (var s in stabs) if (s.Joint) Destroy(s.Joint);   // o corpo some junto: não precisa religar colisão
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
        // Criados pelo ArenaGame.Spawn (com o representante invisível): morrem para qualquer coisa — magia, raio, soco,
        // espinhos — e não só para a arma. Morreu: o corpo fica 3 s e outro nasce no lugar.
        readonly List<ArenaEnemy> current = new List<ArenaEnemy>();
        readonly List<RagdollGoblin> bodies = new List<RagdollGoblin>();
        readonly List<float> wait = new List<float>();

        void Update()
        {
            int n = SaveSystem.Options.TrainingGoblins;
            var game = ArenaGame.Instance;
            if (!game) return;
            while (current.Count < n) { current.Add(null); bodies.Add(null); wait.Add(0.3f + current.Count * 0.2f); }
            for (int i = current.Count - 1; i >= n; i--)
            {
                if (current[i]) Destroy(current[i].gameObject);
                if (bodies[i]) Destroy(bodies[i].gameObject);
                current.RemoveAt(i); bodies.RemoveAt(i); wait.RemoveAt(i);
            }
            for (int i = 0; i < n; i++)
            {
                if (current[i] && !current[i].Dead)
                {
                    if (current[i] is PhysicalGoblinEnemy pg && pg.Body) bodies[i] = pg.Body;
                    wait[i] = 3f; continue;
                }
                wait[i] -= Time.deltaTime;
                if (wait[i] > 0f) continue;
                if (bodies[i]) Destroy(bodies[i].gameObject);   // tira o corpo antigo antes de nascer outro
                if (current[i]) Destroy(current[i].gameObject);
                // Em leque a partir do ponto (1 m entre eles).
                var dir = (LookAt - Point); dir.y = 0f; dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.forward;
                var side = Vector3.Cross(Vector3.up, dir);
                float off = (i - (n - 1) * 0.5f) * 1.1f;
                var def = (i % 3) switch { 1 => EnemyDef.Esqueleto, 2 => EnemyDef.Orc, _ => EnemyDef.Goblin };
                current[i] = game.Spawn(def, Point + side * off - dir * Mathf.Abs(off) * 0.3f, false, forcePhysical: true);
                bodies[i] = null;
                wait[i] = 3f;
            }
        }
    }
}
