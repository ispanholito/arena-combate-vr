using Arena.Core;
using UnityEngine;

namespace Arena
{
    // Orc: mesma IA do goblin (golpe anunciado, aparável), mas grande, lento e com machado de duas mãos.
    // Não cambaleia com golpes comuns (só com a postura quebrada) e bloqueia pouco.
    public class OrcEnemy : GoblinEnemy
    {
        protected override void BuildVisual()
        {
            bloodColor = new Color(0.5f, 0.1f, 0.1f);
            bodyTop = 1.3f * k;
            skinMat = ArenaUtil.Lit(new Color(0.36f, 0.45f, 0.3f));
            var leather = ArenaUtil.Lit(new Color(0.25f, 0.17f, 0.11f));
            var iron = ArenaUtil.Lit(new Color(0.45f, 0.46f, 0.5f), new Color(0.04f, 0.04f, 0.05f));
            // Tronco largo, cinto, ombreira e cabeça com presas.
            ArenaUtil.Prim(PrimitiveType.Cylinder, model, new Vector3(0, 0.6f, 0), new Vector3(0.62f, 0.4f, 0.5f), leather);
            ArenaUtil.Prim(PrimitiveType.Cube, model, new Vector3(0, 0.62f, 0), new Vector3(0.64f, 0.08f, 0.52f), iron);
            ArenaUtil.Prim(PrimitiveType.Sphere, model, new Vector3(0, 0.95f, 0), new Vector3(0.58f, 0.4f, 0.48f), skinMat);
            ArenaUtil.Prim(PrimitiveType.Sphere, model, new Vector3(0.26f, 0.98f, 0), Vector3.one * 0.22f, iron);
            ArenaUtil.Prim(PrimitiveType.Sphere, model, new Vector3(0, 1.18f, 0.04f), new Vector3(0.34f, 0.32f, 0.34f), skinMat);
            var tusk = ArenaUtil.Lit(new Color(0.92f, 0.9f, 0.8f));
            var eye = ArenaUtil.Lit(new Color(1f, 0.3f, 0.1f), new Color(0.6f, 0.1f, 0f));
            foreach (int s in new[] { -1, 1 })
            {
                ArenaUtil.Prim(PrimitiveType.Cube, model, new Vector3(s * 0.07f, 1.1f, 0.18f), new Vector3(0.03f, 0.08f, 0.03f), tusk);
                ArenaUtil.Prim(PrimitiveType.Sphere, model, new Vector3(s * 0.07f, 1.22f, 0.16f), Vector3.one * 0.05f, eye);
            }
            // Machado grande: o braço é o `arm` da IA (arma ao longo de +Y).
            arm = new GameObject("Braco").transform;
            arm.SetParent(model, false);
            arm.localPosition = new Vector3(0.32f, 0.95f, 0f);
            clubColor = new Color(0.32f, 0.22f, 0.14f);
            clubMat = ArenaUtil.Lit(clubColor);
            ArenaUtil.Prim(PrimitiveType.Cylinder, arm, new Vector3(0, 0.45f, 0), new Vector3(0.07f, 0.45f, 0.07f), clubMat);
            ArenaUtil.Prim(PrimitiveType.Cube, arm, new Vector3(0, 0.82f, 0.13f), new Vector3(0.05f, 0.3f, 0.28f), iron);
        }
    }
}
