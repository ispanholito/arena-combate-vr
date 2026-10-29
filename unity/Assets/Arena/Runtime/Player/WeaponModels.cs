using Arena.Core;
using UnityEngine;

namespace Arena
{
    // Modelos (primitivas) das 6 armas, usados na mão (PlayerWeapon) e no loot do chão (WeaponPickup).
    // Eixo: Z = cabo → ponta, empunhadura em Z=0. Armas raras ganham uma gema brilhante na cor da raridade.
    public static class WeaponModels
    {
        public readonly struct Box
        {
            public readonly Vector3 Center, Size;
            public Box(Vector3 c, Vector3 s) { Center = c; Size = s; }
        }

        public static Color RarityColor(Rarity r)
        {
            var (cr, cg, cb) = ItemInstance.RarityColor(r);
            return new Color(cr, cg, cb);
        }

        // Cria as partes visuais em `t`. Devolve o cristal do cajado (ponto de onde saem as magias) ou null.
        public static Transform Build(Transform t, WeaponDef def, Rarity rarity)
        {
            Transform crystal = null;
            var wood = ArenaUtil.Lit(new Color(0.33f, 0.21f, 0.11f));
            var steel = ArenaUtil.Lit(new Color(0.7f, 0.72f, 0.76f), new Color(0.06f, 0.06f, 0.07f));
            Vector3 gemAt;
            if (def.Look != WeaponLook.Padrao) { gemAt = BuildLook(t, def, rarity, wood, steel, ref crystal); }
            else switch (def.Kind)
            {
                case WeaponKind.Espada:
                {
                    var metal = ArenaUtil.Lit(new Color(0.78f, 0.8f, 0.84f), new Color(0.08f, 0.08f, 0.1f));
                    ArenaUtil.Prim(PrimitiveType.Cube, t, new Vector3(0, 0, 0.55f), new Vector3(0.05f, 0.012f, 0.85f), metal);
                    ArenaUtil.Prim(PrimitiveType.Cube, t, new Vector3(0, 0, 1.0f), new Vector3(0.035f, 0.01f, 0.1f), metal);
                    ArenaUtil.Prim(PrimitiveType.Cube, t, new Vector3(0, 0, 0.11f), new Vector3(0.22f, 0.03f, 0.04f), ArenaUtil.Lit(new Color(0.54f, 0.42f, 0.16f)));
                    Handle(t, Vector3.zero, 0.1f, 0.036f, ArenaUtil.Lit(new Color(0.23f, 0.14f, 0.08f)));
                    gemAt = new Vector3(0, 0, 0.11f);
                    break;
                }
                case WeaponKind.Machado:
                {
                    Handle(t, new Vector3(0, 0, 0.45f), 0.575f, 0.05f, ArenaUtil.Lit(new Color(0.35f, 0.23f, 0.12f)));
                    var s = ArenaUtil.Lit(new Color(0.6f, 0.63f, 0.67f), new Color(0.06f, 0.06f, 0.07f));
                    ArenaUtil.Prim(PrimitiveType.Cube, t, new Vector3(0, -0.13f, 0.92f), new Vector3(0.06f, 0.38f, 0.26f), s);
                    ArenaUtil.Prim(PrimitiveType.Cube, t, new Vector3(0, 0.06f, 0.92f), new Vector3(0.05f, 0.1f, 0.1f), s);
                    gemAt = new Vector3(0, 0.06f, 0.92f);
                    break;
                }
                case WeaponKind.Maca:
                {
                    Handle(t, new Vector3(0, 0, 0.3f), 0.35f, 0.045f, wood);
                    ArenaUtil.Prim(PrimitiveType.Sphere, t, new Vector3(0, 0, 0.74f), Vector3.one * 0.2f, steel);
                    // Cravos
                    foreach (var d in new[] { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward })
                        ArenaUtil.Prim(PrimitiveType.Cube, t, new Vector3(0, 0, 0.74f) + d * 0.11f, Vector3.one * 0.05f, steel)
                            .transform.localRotation = Quaternion.Euler(45, 45, 0);
                    gemAt = new Vector3(0, 0, -0.04f);
                    break;
                }
                case WeaponKind.Lanca:
                {
                    Handle(t, new Vector3(0, 0, 0.7f), 0.85f, 0.04f, wood);
                    var tip = ArenaUtil.Prim(PrimitiveType.Cube, t, new Vector3(0, 0, 1.77f), new Vector3(0.07f, 0.012f, 0.26f), steel).transform;
                    ArenaUtil.Prim(PrimitiveType.Cube, t, new Vector3(0, 0, 1.6f), new Vector3(0.05f, 0.05f, 0.06f), steel);
                    tip.localRotation = Quaternion.identity;
                    gemAt = new Vector3(0, 0, 1.6f);
                    break;
                }
                case WeaponKind.Adaga:
                {
                    var metal = ArenaUtil.Lit(new Color(0.82f, 0.84f, 0.88f), new Color(0.08f, 0.08f, 0.1f));
                    ArenaUtil.Prim(PrimitiveType.Cube, t, new Vector3(0, 0, 0.28f), new Vector3(0.035f, 0.01f, 0.32f), metal);
                    ArenaUtil.Prim(PrimitiveType.Cube, t, new Vector3(0, 0, 0.09f), new Vector3(0.1f, 0.025f, 0.03f), ArenaUtil.Lit(new Color(0.3f, 0.3f, 0.32f)));
                    Handle(t, Vector3.zero, 0.07f, 0.03f, ArenaUtil.Lit(new Color(0.15f, 0.1f, 0.08f)));
                    gemAt = new Vector3(0, 0, -0.07f);
                    break;
                }
                default:
                {
                    Handle(t, new Vector3(0, 0, 0.45f), 0.65f, 0.05f, ArenaUtil.Lit(new Color(0.29f, 0.2f, 0.14f)));
                    var crystalColor = rarity == Rarity.Comum ? new Color(0.5f, 0.7f, 1f) : RarityColor(rarity);
                    crystal = ArenaUtil.Prim(PrimitiveType.Sphere, t, new Vector3(0, 0, 1.14f), Vector3.one * 0.12f,
                                             ArenaUtil.Lit(crystalColor, crystalColor * 0.7f)).transform;
                    gemAt = new Vector3(0, 0, -0.18f);
                    break;
                }
            }
            if (rarity != Rarity.Comum)
            {
                var c = RarityColor(rarity);
                float s = 0.035f + 0.008f * (int)rarity;
                ArenaUtil.Prim(PrimitiveType.Sphere, t, gemAt + Vector3.up * 0.02f, Vector3.one * s, ArenaUtil.Lit(c, c * 1.2f));
            }
            return crystal;
        }

        // Armas novas (Look): modelo próprio, mesmo funcionamento do tipo.
        static Vector3 BuildLook(Transform t, WeaponDef def, Rarity rarity, Material wood, Material steel, ref Transform crystal)
        {
            switch (def.Look)
            {
                case WeaponLook.Katana:
                {
                    var metal = ArenaUtil.Lit(new Color(0.85f, 0.87f, 0.9f), new Color(0.1f, 0.1f, 0.12f));
                    // Lâmina em 3 trechos levemente curvos.
                    for (int i = 0; i < 3; i++)
                    {
                        var seg = ArenaUtil.Prim(PrimitiveType.Cube, t, new Vector3(0, 0.012f * i * i, 0.29f + i * 0.27f), new Vector3(0.032f, 0.01f, 0.29f), metal).transform;
                        seg.localRotation = Quaternion.Euler(-2.5f * i, 0, 0);
                    }
                    var tsuba = ArenaUtil.Prim(PrimitiveType.Cylinder, t, new Vector3(0, 0, 0.13f), new Vector3(0.09f, 0.008f, 0.07f), ArenaUtil.Lit(new Color(0.15f, 0.13f, 0.12f))).transform;
                    tsuba.localRotation = Quaternion.Euler(90, 0, 0);
                    Handle(t, new Vector3(0, 0, 0.0f), 0.12f, 0.034f, ArenaUtil.Lit(new Color(0.12f, 0.1f, 0.25f)));
                    return new Vector3(0, 0, 0.13f);
                }
                case WeaponLook.Rapieira:
                {
                    var metal = ArenaUtil.Lit(new Color(0.85f, 0.85f, 0.88f), new Color(0.08f, 0.08f, 0.1f));
                    ArenaUtil.Prim(PrimitiveType.Cube, t, new Vector3(0, 0, 0.6f), new Vector3(0.016f, 0.01f, 0.9f), metal);
                    var gold = ArenaUtil.Lit(new Color(0.8f, 0.62f, 0.2f), new Color(0.2f, 0.15f, 0.03f));
                    ArenaUtil.Prim(PrimitiveType.Sphere, t, new Vector3(0, 0, 0.12f), new Vector3(0.13f, 0.13f, 0.05f), gold);
                    ArenaUtil.Prim(PrimitiveType.Cube, t, new Vector3(0, 0, 0.13f), new Vector3(0.18f, 0.015f, 0.015f), gold);
                    Handle(t, Vector3.zero, 0.1f, 0.03f, ArenaUtil.Lit(new Color(0.3f, 0.12f, 0.1f)));
                    return new Vector3(0, 0, -0.1f);
                }
                case WeaponLook.Martelo:
                {
                    Handle(t, new Vector3(0, 0, 0.5f), 0.55f, 0.05f, wood);
                    var dark = ArenaUtil.Lit(new Color(0.42f, 0.44f, 0.48f), new Color(0.04f, 0.04f, 0.05f));
                    ArenaUtil.Prim(PrimitiveType.Cube, t, new Vector3(0, 0, 0.98f), new Vector3(0.36f, 0.18f, 0.2f), dark);
                    ArenaUtil.Prim(PrimitiveType.Cube, t, new Vector3(0.19f, 0, 0.98f), new Vector3(0.04f, 0.22f, 0.24f), steel);
                    ArenaUtil.Prim(PrimitiveType.Cube, t, new Vector3(-0.19f, 0, 0.98f), new Vector3(0.04f, 0.22f, 0.24f), steel);
                    return new Vector3(0, 0.1f, 0.98f);
                }
                case WeaponLook.Machadinha:
                {
                    Handle(t, new Vector3(0, 0, 0.25f), 0.3f, 0.035f, wood);
                    ArenaUtil.Prim(PrimitiveType.Cube, t, new Vector3(0, -0.08f, 0.47f), new Vector3(0.04f, 0.17f, 0.13f), steel);
                    return new Vector3(0, 0.03f, 0.47f);
                }
                case WeaponLook.Bacalhau:
                {
                    var fish = ArenaUtil.Lit(new Color(0.78f, 0.7f, 0.5f));
                    ArenaUtil.Prim(PrimitiveType.Sphere, t, new Vector3(0, 0, 0.45f), new Vector3(0.07f, 0.17f, 0.62f), fish);
                    // Rabo (onde se segura), olho e boca.
                    var tailA = ArenaUtil.Prim(PrimitiveType.Cube, t, new Vector3(0, 0.04f, 0.03f), new Vector3(0.02f, 0.1f, 0.06f), fish).transform;
                    tailA.localRotation = Quaternion.Euler(-35, 0, 0);
                    var tailB = ArenaUtil.Prim(PrimitiveType.Cube, t, new Vector3(0, -0.04f, 0.03f), new Vector3(0.02f, 0.1f, 0.06f), fish).transform;
                    tailB.localRotation = Quaternion.Euler(35, 0, 0);
                    var black = ArenaUtil.Lit(Color.black);
                    ArenaUtil.Prim(PrimitiveType.Sphere, t, new Vector3(0.035f, 0.04f, 0.68f), Vector3.one * 0.025f, black);
                    ArenaUtil.Prim(PrimitiveType.Sphere, t, new Vector3(-0.035f, 0.04f, 0.68f), Vector3.one * 0.025f, black);
                    ArenaUtil.Prim(PrimitiveType.Cube, t, new Vector3(0, -0.03f, 0.75f), new Vector3(0.04f, 0.01f, 0.03f), black);
                    return new Vector3(0, 0.09f, 0.4f);
                }
                case WeaponLook.Frigideira:
                {
                    var iron = ArenaUtil.Lit(new Color(0.16f, 0.16f, 0.17f), new Color(0.02f, 0.02f, 0.02f));
                    Handle(t, new Vector3(0, 0, 0.12f), 0.15f, 0.035f, iron);
                    // Fundo da panela de pé (plano yz): golpe de lado bate de chapa — BONK.
                    var pan = ArenaUtil.Prim(PrimitiveType.Cylinder, t, new Vector3(0, 0, 0.45f), new Vector3(0.32f, 0.015f, 0.32f), iron).transform;
                    pan.localRotation = Quaternion.Euler(0, 0, 90);
                    var rim = ArenaUtil.Prim(PrimitiveType.Cylinder, t, new Vector3(0.02f, 0, 0.45f), new Vector3(0.33f, 0.025f, 0.33f), iron).transform;
                    rim.localRotation = Quaternion.Euler(0, 0, 90);
                    return new Vector3(0, 0.03f, 0.05f);
                }
                case WeaponLook.Tocha:
                {
                    Handle(t, new Vector3(0, 0, 0.3f), 0.34f, 0.04f, wood);
                    ArenaUtil.Prim(PrimitiveType.Cylinder, t, new Vector3(0, 0, 0.64f), new Vector3(0.07f, 0.05f, 0.07f), ArenaUtil.Lit(new Color(0.2f, 0.14f, 0.1f))).transform.localRotation = Quaternion.Euler(90, 0, 0);
                    var flame = new Color(1f, 0.55f, 0.12f);
                    ArenaUtil.Prim(PrimitiveType.Sphere, t, new Vector3(0, 0, 0.72f), new Vector3(0.1f, 0.1f, 0.14f), ArenaUtil.Unlit(flame));
                    ArenaUtil.Prim(PrimitiveType.Sphere, t, new Vector3(0, 0, 0.74f), new Vector3(0.06f, 0.06f, 0.09f), ArenaUtil.Unlit(new Color(1f, 0.9f, 0.5f)));
                    var l = new GameObject("Chama").AddComponent<Light>();
                    l.transform.SetParent(t, false);
                    l.transform.localPosition = new Vector3(0, 0, 0.75f);
                    l.type = LightType.Point; l.color = flame; l.range = 3.5f; l.intensity = 1.6f;
                    return new Vector3(0, 0.03f, 0.55f);
                }
                case WeaponLook.Pistola:
                {
                    var brass = ArenaUtil.Lit(new Color(0.75f, 0.58f, 0.25f), new Color(0.15f, 0.1f, 0.02f));
                    var iron = ArenaUtil.Lit(new Color(0.25f, 0.25f, 0.27f), new Color(0.03f, 0.03f, 0.03f));
                    var g = ArenaUtil.Prim(PrimitiveType.Cube, t, new Vector3(0, -0.06f, -0.02f), new Vector3(0.035f, 0.12f, 0.05f), wood).transform;
                    g.localRotation = Quaternion.Euler(18, 0, 0);
                    ArenaUtil.Prim(PrimitiveType.Cube, t, new Vector3(0, 0.015f, 0.06f), new Vector3(0.04f, 0.035f, 0.16f), wood);
                    Handle(t, new Vector3(0, 0.03f, 0.17f), 0.13f, 0.028f, iron);
                    Handle(t, new Vector3(0, 0.03f, 0.3f), 0.012f, 0.04f, brass);
                    ArenaUtil.Prim(PrimitiveType.Cube, t, new Vector3(0, 0.055f, 0.0f), new Vector3(0.02f, 0.03f, 0.04f), iron);   // cão
                    ArenaUtil.Prim(PrimitiveType.Cube, t, new Vector3(0, -0.015f, 0.03f), new Vector3(0.01f, 0.03f, 0.01f), brass);  // gatilho
                    return new Vector3(0, -0.11f, -0.04f);
                }
                case WeaponLook.PistolaRepeticao:
                {
                    var iron = ArenaUtil.Lit(new Color(0.22f, 0.22f, 0.25f), new Color(0.03f, 0.03f, 0.03f));
                    var brass = ArenaUtil.Lit(new Color(0.75f, 0.58f, 0.25f), new Color(0.15f, 0.1f, 0.02f));
                    var g = ArenaUtil.Prim(PrimitiveType.Cube, t, new Vector3(0, -0.06f, -0.02f), new Vector3(0.035f, 0.12f, 0.05f), wood).transform;
                    g.localRotation = Quaternion.Euler(18, 0, 0);
                    Handle(t, new Vector3(0, 0.035f, 0.2f), 0.12f, 0.026f, iron);                       // cano
                    ArenaUtil.Prim(PrimitiveType.Cylinder, t, new Vector3(0, 0.035f, 0.06f), new Vector3(0.06f, 0.035f, 0.06f), brass).transform.localRotation = Quaternion.Euler(90, 0, 0);   // tambor
                    ArenaUtil.Prim(PrimitiveType.Cube, t, new Vector3(0, 0.0f, 0.04f), new Vector3(0.03f, 0.03f, 0.12f), iron);
                    ArenaUtil.Prim(PrimitiveType.Cube, t, new Vector3(0, 0.06f, -0.01f), new Vector3(0.02f, 0.025f, 0.03f), iron);   // cão
                    ArenaUtil.Prim(PrimitiveType.Cube, t, new Vector3(0, -0.015f, 0.03f), new Vector3(0.01f, 0.03f, 0.01f), brass);  // gatilho
                    return new Vector3(0, -0.11f, -0.04f);
                }
                case WeaponLook.Bacamarte:
                {
                    var brass = ArenaUtil.Lit(new Color(0.78f, 0.6f, 0.25f), new Color(0.15f, 0.1f, 0.02f));
                    ArenaUtil.Prim(PrimitiveType.Cube, t, new Vector3(0, -0.04f, -0.14f), new Vector3(0.045f, 0.09f, 0.28f), wood);   // coronha
                    ArenaUtil.Prim(PrimitiveType.Cube, t, new Vector3(0, 0.0f, 0.12f), new Vector3(0.045f, 0.045f, 0.3f), wood);
                    Handle(t, new Vector3(0, 0.03f, 0.36f), 0.26f, 0.04f, brass);
                    Handle(t, new Vector3(0, 0.03f, 0.63f), 0.04f, 0.085f, brass);   // boca de sino
                    return new Vector3(0, 0.03f, -0.02f);
                }
                case WeaponLook.Vassoura:
                {
                    Handle(t, new Vector3(0, 0, 0.6f), 0.7f, 0.035f, wood);
                    ArenaUtil.Prim(PrimitiveType.Cube, t, new Vector3(0, 0, 1.36f), new Vector3(0.2f, 0.06f, 0.3f), ArenaUtil.Lit(new Color(0.85f, 0.72f, 0.35f)));
                    ArenaUtil.Prim(PrimitiveType.Cylinder, t, new Vector3(0, 0, 1.2f), new Vector3(0.07f, 0.03f, 0.07f), ArenaUtil.Lit(new Color(0.4f, 0.15f, 0.5f))).transform.localRotation = Quaternion.Euler(90, 0, 0);
                    // O nó brilhante faz o papel do cristal do cajado (as magias saem dele).
                    var c = rarity == Rarity.Comum ? new Color(0.6f, 1f, 0.5f) : RarityColor(rarity);
                    crystal = ArenaUtil.Prim(PrimitiveType.Sphere, t, new Vector3(0, 0.05f, 1.2f), Vector3.one * 0.07f, ArenaUtil.Lit(c, c * 0.8f)).transform;
                    return new Vector3(0, 0, -0.08f);
                }
            }
            return Vector3.zero;
        }

        // Arma engraçada acertou: texto de efeito (e a frigideira faz BONK).
        static readonly string[] FishWords = { "PEIXADA!", "SLAP!", "CHEIRO FORTE!" };
        static readonly string[] BroomWords = { "VASSOURADA!", "XÔ!", "LIMPEZA!" };
        public static void FunnyHit(WeaponDef w, Vector3 at)
        {
            string text = w.Look switch
            {
                WeaponLook.Bacalhau => FishWords[Random.Range(0, FishWords.Length)],
                WeaponLook.Frigideira => "BONK!",
                WeaponLook.Vassoura => BroomWords[Random.Range(0, BroomWords.Length)],
                _ => null,
            };
            if (text == null) return;
            ArenaFx.FloatText(text, at + Vector3.up * 0.5f, new Color(1f, 0.95f, 0.5f), 1.2f);
            if (w.Look == WeaponLook.Frigideira) ArenaAudio.Play(Sfx.Block, at, 1f, 1.6f);
        }

        static void Handle(Transform t, Vector3 center, float halfLength, float diameter, Material m)
        {
            var h = ArenaUtil.Prim(PrimitiveType.Cylinder, t, center, new Vector3(diameter, halfLength, diameter), m).transform;
            h.localRotation = Quaternion.Euler(90, 0, 0);
        }

        // Colisores físicos da arma na mão.
        public static Box[] Colliders(WeaponDef def) => def.Look switch
        {
            WeaponLook.Katana => new[] { new Box(new Vector3(0, 0.01f, 0.56f), new Vector3(0.04f, 0.05f, 0.86f)), new Box(new Vector3(0, 0, 0.13f), new Vector3(0.09f, 0.07f, 0.02f)) },
            WeaponLook.Rapieira => new[] { new Box(new Vector3(0, 0, 0.6f), new Vector3(0.025f, 0.02f, 0.92f)), new Box(new Vector3(0, 0, 0.12f), new Vector3(0.14f, 0.13f, 0.06f)) },
            WeaponLook.Martelo => new[] { new Box(new Vector3(0, 0, 0.5f), new Vector3(0.05f, 0.05f, 1.0f)), new Box(new Vector3(0, 0, 0.98f), new Vector3(0.42f, 0.2f, 0.24f)) },
            WeaponLook.Machadinha => new[] { new Box(new Vector3(0, 0, 0.26f), new Vector3(0.04f, 0.04f, 0.56f)), new Box(new Vector3(0, -0.08f, 0.47f), new Vector3(0.05f, 0.18f, 0.14f)) },
            WeaponLook.Bacalhau => new[] { new Box(new Vector3(0, 0, 0.45f), new Vector3(0.07f, 0.17f, 0.62f)) },
            WeaponLook.Frigideira => new[] { new Box(new Vector3(0, 0, 0.13f), new Vector3(0.04f, 0.04f, 0.3f)), new Box(new Vector3(0, 0, 0.45f), new Vector3(0.05f, 0.33f, 0.33f)) },
            WeaponLook.Tocha => new[] { new Box(new Vector3(0, 0, 0.36f), new Vector3(0.06f, 0.06f, 0.8f)) },
            WeaponLook.Vassoura => new[] { new Box(new Vector3(0, 0, 0.65f), new Vector3(0.05f, 0.05f, 1.35f)), new Box(new Vector3(0, 0, 1.36f), new Vector3(0.22f, 0.08f, 0.3f)) },
            WeaponLook.Pistola => new[] { new Box(new Vector3(0, 0.0f, 0.14f), new Vector3(0.04f, 0.08f, 0.34f)) },
            WeaponLook.PistolaRepeticao => new[] { new Box(new Vector3(0, 0.0f, 0.14f), new Vector3(0.06f, 0.09f, 0.36f)) },
            WeaponLook.Bacamarte => new[] { new Box(new Vector3(0, 0, 0.24f), new Vector3(0.06f, 0.1f, 0.84f)) },
            _ => KindColliders(def),
        };

        static Box[] KindColliders(WeaponDef def) => def.Kind switch
        {
            WeaponKind.Espada => new[] { new Box(new Vector3(0, 0, 0.6f), new Vector3(0.05f, 0.03f, 0.95f)), new Box(new Vector3(0, 0, 0.11f), new Vector3(0.22f, 0.03f, 0.04f)) },
            WeaponKind.Machado => new[] { new Box(new Vector3(0, 0, 0.5f), new Vector3(0.05f, 0.05f, 0.9f)), new Box(new Vector3(0, -0.08f, 0.92f), new Vector3(0.06f, 0.48f, 0.26f)) },
            WeaponKind.Maca => new[] { new Box(new Vector3(0, 0, 0.32f), new Vector3(0.05f, 0.05f, 0.7f)), new Box(new Vector3(0, 0, 0.74f), new Vector3(0.24f, 0.24f, 0.24f)) },
            WeaponKind.Lanca => new[] { new Box(new Vector3(0, 0, 0.9f), new Vector3(0.045f, 0.045f, 1.95f)) },
            WeaponKind.Adaga => new[] { new Box(new Vector3(0, 0, 0.26f), new Vector3(0.04f, 0.025f, 0.38f)) },
            _ => new[] { new Box(new Vector3(0, 0, 0.55f), new Vector3(0.06f, 0.06f, 1.2f)) },
        };

        // Centro de massa: equilíbrio da arma (machado e maça pendem para a cabeça).
        public static Vector3 CenterOfMass(WeaponDef def) => def.Look switch
        {
            WeaponLook.Katana => new Vector3(0, 0, 0.16f),
            WeaponLook.Rapieira => new Vector3(0, 0, 0.1f),
            WeaponLook.Martelo => new Vector3(0, 0, 0.82f),
            WeaponLook.Machadinha => new Vector3(0, -0.03f, 0.36f),
            WeaponLook.Bacalhau => new Vector3(0, 0, 0.42f),
            WeaponLook.Frigideira => new Vector3(0, 0, 0.38f),
            WeaponLook.Tocha => new Vector3(0, 0, 0.42f),
            WeaponLook.Vassoura => new Vector3(0, 0, 0.8f),
            WeaponLook.Pistola => new Vector3(0, 0, 0.08f),
            WeaponLook.PistolaRepeticao => new Vector3(0, 0, 0.07f),
            WeaponLook.Bacamarte => new Vector3(0, 0, 0.15f),
            _ => KindCenterOfMass(def),
        };

        static Vector3 KindCenterOfMass(WeaponDef def) => def.Kind switch
        {
            WeaponKind.Espada => new Vector3(0, 0, 0.15f),
            WeaponKind.Machado => new Vector3(0, -0.05f, 0.75f),
            WeaponKind.Maca => new Vector3(0, 0, 0.6f),
            WeaponKind.Lanca => new Vector3(0, 0, 0.55f),
            WeaponKind.Adaga => new Vector3(0, 0, 0.08f),
            _ => new Vector3(0, 0, 0.4f),
        };
    }
}
