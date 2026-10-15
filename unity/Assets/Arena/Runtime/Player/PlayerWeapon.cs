using Arena.Core;
using UnityEngine;

namespace Arena
{
    // Arma na mão direita: segue a mão com atraso pelo peso, mede a velocidade da ponta e acerta inimigos por varredura.
    // Também pode ser arremessada (grip) e chamada de volta (gatilho).
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

        // Inclinação da lâmina em relação ao controle (graus). Ajustar no Quest: negativo = lâmina mais para cima.
        public float VrGripPitch = -40f;

        enum Mode { Held, Thrown, Recalling }
        Mode mode = Mode.Held;

        ArenaPlayer player;
        Transform visual;
        Vector3 basePrev, tipPrev, throwVel;
        float spin, desktopSwingT = -1f, desktopPointT;
        bool hasPrev;
        Transform staffCrystal;

        public Transform Visual => visual;
        public Vector3 CrystalPoint => staffCrystal ? staffCrystal.position : TipPoint;

        public void Init(ArenaPlayer p) { player = p; }

        public void Equip(WeaponDef def)
        {
            Def = def;
            var pos = visual ? visual.position : transform.position;
            var rot = visual ? visual.rotation : transform.rotation;
            if (visual) Destroy(visual.gameObject);
            visual = BuildVisual(def).transform;
            visual.SetPositionAndRotation(pos, rot);
            mode = Mode.Held;
            hasPrev = false;
        }

        GameObject BuildVisual(WeaponDef def)
        {
            var root = new GameObject(def.Name);
            var t = root.transform;
            staffCrystal = null;
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
                    break;
                }
                case WeaponKind.Machado:
                {
                    var h = ArenaUtil.Prim(PrimitiveType.Cylinder, t, new Vector3(0, 0, 0.45f), new Vector3(0.05f, 0.575f, 0.05f), ArenaUtil.Lit(new Color(0.35f, 0.23f, 0.12f))).transform;
                    h.localRotation = Quaternion.Euler(90, 0, 0);
                    var steel = ArenaUtil.Lit(new Color(0.6f, 0.63f, 0.67f), new Color(0.06f, 0.06f, 0.07f));
                    ArenaUtil.Prim(PrimitiveType.Cube, t, new Vector3(0, -0.13f, 0.92f), new Vector3(0.06f, 0.38f, 0.26f), steel);
                    ArenaUtil.Prim(PrimitiveType.Cube, t, new Vector3(0, 0.06f, 0.92f), new Vector3(0.05f, 0.1f, 0.1f), steel);
                    break;
                }
                default:
                {
                    var h = ArenaUtil.Prim(PrimitiveType.Cylinder, t, new Vector3(0, 0, 0.45f), new Vector3(0.05f, 0.65f, 0.05f), ArenaUtil.Lit(new Color(0.29f, 0.2f, 0.14f))).transform;
                    h.localRotation = Quaternion.Euler(90, 0, 0);
                    staffCrystal = ArenaUtil.Prim(PrimitiveType.Sphere, t, new Vector3(0, 0, 1.14f), Vector3.one * 0.12f, ArenaUtil.Lit(new Color(0.5f, 0.7f, 1f), new Color(0.3f, 0.5f, 1f))).transform;
                    break;
                }
            }
            return root;
        }

        // ---------- Arremessar / chamar de volta ----------
        public void Throw(Vector3 velocity)
        {
            if (!Held) return;
            mode = Mode.Thrown;
            throwVel = velocity.magnitude < 3f ? player.HeadForward * 9f + Vector3.up * 2f : velocity * 1.6f;
            spin = 900f;
        }

        public void Recall()
        {
            if (Held) return;
            mode = Mode.Recalling;
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

        // ---------- Loop (chamado pelo ArenaPlayer depois do movimento) ----------
        public void Tick(float dt, bool vr, bool desktopParry)
        {
            if (Def == null || dt <= 0f) return;
            switch (mode)
            {
                case Mode.Held:
                {
                    Vector3 tp; Quaternion tr;
                    if (vr) { tp = player.RightHand.position; tr = player.RightHand.rotation * Quaternion.Euler(VrGripPitch, 0, 0); }
                    else DesktopPose(dt, out tp, out tr, desktopParry);
                    float k = 1f - Mathf.Exp(-Def.FollowRate(player.State.Strength) * dt);
                    if (!vr) k = Mathf.Max(k, 1f - Mathf.Exp(-25f * dt));
                    visual.position = Vector3.Lerp(visual.position, tp, k);
                    visual.rotation = Quaternion.Slerp(visual.rotation, tr, k);
                    break;
                }
                case Mode.Thrown:
                    throwVel.y -= 9f * dt;
                    visual.position += throwVel * dt;
                    visual.Rotate(Vector3.right, spin * dt, Space.Self);
                    if (visual.position.y < 0.05f) { visual.position = new Vector3(visual.position.x, 0.05f, visual.position.z); throwVel = Vector3.zero; spin = 0f; }
                    visual.position = ArenaUtil.ClampToArena(visual.position);
                    break;
                case Mode.Recalling:
                {
                    var target = player.RightHand.position;
                    var to = target - visual.position;
                    float speed = 14f;
                    visual.position += to.normalized * Mathf.Min(to.magnitude, speed * dt);
                    visual.Rotate(Vector3.right, 720f * dt, Space.Self);
                    if (to.magnitude < 0.25f) { mode = Mode.Held; ArenaInput.Haptic(0.6f, 0.08f); }
                    break;
                }
            }

            basePrev = BasePoint; tipPrev = TipPoint;
            BasePoint = visual.TransformPoint(new Vector3(0, 0, 0.12f));
            TipPoint = visual.TransformPoint(new Vector3(0, 0, Def.Length));
            if (!hasPrev) { basePrev = BasePoint; tipPrev = TipPoint; hasPrev = true; }
            TipVelocity = (TipPoint - tipPrev) / dt;
            TipSpeed = TipVelocity.magnitude;

            if (TipSpeed > Combat.MinSwingSpeed) CheckHits();
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

        void OnDestroy() { if (visual) Destroy(visual.gameObject); }
    }
}
