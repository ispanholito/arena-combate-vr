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
            switch (def.Kind)
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

        static void Handle(Transform t, Vector3 center, float halfLength, float diameter, Material m)
        {
            var h = ArenaUtil.Prim(PrimitiveType.Cylinder, t, center, new Vector3(diameter, halfLength, diameter), m).transform;
            h.localRotation = Quaternion.Euler(90, 0, 0);
        }

        // Colisores físicos da arma na mão.
        public static Box[] Colliders(WeaponDef def) => def.Kind switch
        {
            WeaponKind.Espada => new[] { new Box(new Vector3(0, 0, 0.6f), new Vector3(0.05f, 0.03f, 0.95f)), new Box(new Vector3(0, 0, 0.11f), new Vector3(0.22f, 0.03f, 0.04f)) },
            WeaponKind.Machado => new[] { new Box(new Vector3(0, 0, 0.5f), new Vector3(0.05f, 0.05f, 0.9f)), new Box(new Vector3(0, -0.08f, 0.92f), new Vector3(0.06f, 0.48f, 0.26f)) },
            WeaponKind.Maca => new[] { new Box(new Vector3(0, 0, 0.32f), new Vector3(0.05f, 0.05f, 0.7f)), new Box(new Vector3(0, 0, 0.74f), new Vector3(0.24f, 0.24f, 0.24f)) },
            WeaponKind.Lanca => new[] { new Box(new Vector3(0, 0, 0.9f), new Vector3(0.045f, 0.045f, 1.95f)) },
            WeaponKind.Adaga => new[] { new Box(new Vector3(0, 0, 0.26f), new Vector3(0.04f, 0.025f, 0.38f)) },
            _ => new[] { new Box(new Vector3(0, 0, 0.55f), new Vector3(0.06f, 0.06f, 1.2f)) },
        };

        // Centro de massa: equilíbrio da arma (machado e maça pendem para a cabeça).
        public static Vector3 CenterOfMass(WeaponDef def) => def.Kind switch
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
