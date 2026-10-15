using Arena.Core;
using UnityEngine;

namespace Arena
{
    // Escudo da Lâmina na mão esquerda (controle esquerdo).
    // Largar o controle esquerdo e mostrar a mão = escudo vai para as costas e a mão fica livre para os sinais.
    // Desktop: F alterna o escudo, botão direito levanta (apertar logo antes do golpe = aparo).
    [RequireComponent(typeof(ArenaPlayer))]
    public sealed class PlayerShield : MonoBehaviour
    {
        public const float Radius = 0.3f;

        [Tooltip("Ajuste no Quest: rotação do escudo em relação ao controle esquerdo (graus).")]
        public Vector3 VrRotationOffset = new Vector3(0f, 0f, 0f);
        [Tooltip("Ajuste no Quest: deslocamento do centro do escudo em relação ao controle (m).")]
        public Vector3 VrPositionOffset = new Vector3(0f, 0f, 0.06f);

        public bool Available => player.State.Class.Id == ClassId.Lamina;
        public bool Equipped { get; private set; }
        public Vector3 Center => visual.position;
        public float Speed { get; private set; }
        public bool DesktopRaised { get; private set; }
        public float DesktopRaisedFor { get; private set; }

        ArenaPlayer player;
        LeftHandPowers hand;
        Transform visual;
        bool desktopEquipped = true;
        Vector3 prevCenter;
        readonly Vector3[] now = new Vector3[4], prev = new Vector3[4];
        bool hasPrev;

        void Start()
        {
            player = GetComponent<ArenaPlayer>();
            hand = GetComponent<LeftHandPowers>();
            visual = new GameObject("Escudo").transform;
            var wood = ArenaUtil.Lit(new Color(0.36f, 0.24f, 0.14f));
            var iron = ArenaUtil.Lit(new Color(0.5f, 0.52f, 0.56f), new Color(0.05f, 0.05f, 0.06f));
            var face = ArenaUtil.Prim(PrimitiveType.Cylinder, visual, Vector3.zero, new Vector3(Radius * 2f, 0.015f, Radius * 2f), wood).transform;
            face.localRotation = Quaternion.Euler(90, 0, 0);       // face do escudo olha para +Z (para frente do controle)
            var boss = ArenaUtil.Prim(PrimitiveType.Sphere, visual, new Vector3(0, 0, 0.02f), new Vector3(0.12f, 0.12f, 0.06f), iron).transform;
            var rim = ArenaUtil.Prim(PrimitiveType.Cylinder, visual, new Vector3(0, 0, -0.005f), new Vector3(Radius * 2.08f, 0.01f, Radius * 2.08f), iron).transform;
            rim.localRotation = Quaternion.Euler(90, 0, 0);
        }

        void Update()
        {
            bool vr = player.IsVr;
            if (!vr && player.Input.ShieldToggle && Available)
            {
                desktopEquipped = !desktopEquipped;
                ArenaGame.Instance?.Message(desktopEquipped ? "Escudo na mão" : "Escudo nas costas: mão livre", 0.9f);
            }

            bool leftControllerInHand = OVRInput.GetControllerPositionTracked(OVRInput.Controller.LTouch) && !(hand && hand.HandTracked);
            Equipped = Available && (vr ? leftControllerInHand : desktopEquipped) && !player.TreeOpen;
            visual.gameObject.SetActive(Equipped);
            if (!Equipped) { hasPrev = false; DesktopRaised = false; return; }

            if (vr)
            {
                var lh = player.leftHand;
                var rot = lh.rotation * Quaternion.Euler(VrRotationOffset);
                visual.SetPositionAndRotation(lh.position + rot * VrPositionOffset, rot);
            }
            else
            {
                bool raise = player.Input.DesktopParry;
                if (raise && !DesktopRaised) DesktopRaisedFor = 0f;
                DesktopRaised = raise;
                DesktopRaisedFor += Time.deltaTime;
                var cam = player.Head;
                var lp = raise ? new Vector3(-0.12f, -0.12f, 0.42f) : new Vector3(-0.32f, -0.42f, 0.32f);
                var lr = raise ? Quaternion.identity : Quaternion.Euler(20f, -50f, 0f);
                float k = 1f - Mathf.Exp(-25f * Time.deltaTime);
                visual.position = Vector3.Lerp(visual.position, cam.TransformPoint(lp), k);
                visual.rotation = Quaternion.Slerp(visual.rotation, cam.rotation * lr, k);
            }

            float dt = Time.deltaTime;
            if (dt > 0f && hasPrev) Speed = (visual.position - prevCenter).magnitude / dt;
            prevCenter = visual.position;

            // Dois diâmetros cruzados representam o disco do escudo para a colisão com a clava.
            for (int i = 0; i < 4; i++) prev[i] = now[i];
            now[0] = visual.TransformPoint(new Vector3(-Radius, 0, 0)); now[1] = visual.TransformPoint(new Vector3(Radius, 0, 0));
            now[2] = visual.TransformPoint(new Vector3(0, -Radius, 0)); now[3] = visual.TransformPoint(new Vector3(0, Radius, 0));
            if (!hasPrev) { for (int i = 0; i < 4; i++) prev[i] = now[i]; hasPrev = true; }
        }

        // A clava (de clubPrev para clubNow) encostou no escudo neste frame?
        public bool Intercepts(Vector3 clubPrevA, Vector3 clubPrevB, Vector3 clubNowA, Vector3 clubNowB, float tolerance)
        {
            if (!Equipped || !hasPrev || (!player.IsVr && !DesktopRaised)) return false;   // desktop: só levantado protege
            float r = 0.06f + tolerance;
            return Geometry.SweptSegmentsHit(prev[0].ToN(), prev[1].ToN(), now[0].ToN(), now[1].ToN(), clubPrevA.ToN(), clubPrevB.ToN(), clubNowA.ToN(), clubNowB.ToN(), r)
                || Geometry.SweptSegmentsHit(prev[2].ToN(), prev[3].ToN(), now[2].ToN(), now[3].ToN(), clubPrevA.ToN(), clubPrevB.ToN(), clubNowA.ToN(), clubNowB.ToN(), r);
        }

        // Velocidade usada para decidir aparo. Desktop: levantar o escudo até 0,25 s antes do impacto conta como aparo.
        public float GuardSpeed => player.IsVr ? Speed : (DesktopRaised && DesktopRaisedFor < 0.25f ? Guard.ShieldParrySpeed + 1f : 0f);

        // Desktop: com o escudo levantado e olhando para o inimigo, a clava sempre encontra o escudo.
        public bool DesktopCovers(Vector3 enemyPos) =>
            Equipped && !player.IsVr && DesktopRaised
            && Vector3.Dot(player.HeadForward, (enemyPos - player.FeetPosition).Flat().normalized) > 0.5f;

        void OnDestroy() { if (visual) Destroy(visual.gameObject); }
    }
}
