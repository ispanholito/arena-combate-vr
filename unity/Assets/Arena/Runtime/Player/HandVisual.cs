using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace Arena
{
    // Mostra as mãos no jogo SEMPRE, nas duas mãos (Pablo, 2026-10-31): uma esfera por articulação e um "osso"
    // (cilindro) entre elas. Mão livre = pose do rastreamento (XR Hands). Com o controle na mão = mão montada em cima do
    // controle, com os dedos seguindo os sensores (grip fecha médio/anelar/mindinho, gatilho o indicador, polegar
    // encostado nos botões). Segurando uma arma, quem aparece é a luva no cabo (PlayerWeapon) — sem mão dupla.
    // Simples de propósito (primitivas); trocar por um modelo de mão com pele depois.
    [RequireComponent(typeof(ArenaPlayer))]
    public sealed class HandVisual : MonoBehaviour
    {
        ArenaPlayer player;
        XRHandSubsystem hands;
        static readonly List<XRHandSubsystem> buffer = new List<XRHandSubsystem>();
        HandRig left, right;

        // Cadeias de ossos: do pulso até a ponta de cada dedo, mais a linha dos nós dos dedos (forma a palma).
        static readonly XRHandJointID[][] Chains =
        {
            new[] { XRHandJointID.Wrist, XRHandJointID.ThumbMetacarpal, XRHandJointID.ThumbProximal, XRHandJointID.ThumbDistal, XRHandJointID.ThumbTip },
            new[] { XRHandJointID.Wrist, XRHandJointID.IndexMetacarpal, XRHandJointID.IndexProximal, XRHandJointID.IndexIntermediate, XRHandJointID.IndexDistal, XRHandJointID.IndexTip },
            new[] { XRHandJointID.Wrist, XRHandJointID.MiddleMetacarpal, XRHandJointID.MiddleProximal, XRHandJointID.MiddleIntermediate, XRHandJointID.MiddleDistal, XRHandJointID.MiddleTip },
            new[] { XRHandJointID.Wrist, XRHandJointID.RingMetacarpal, XRHandJointID.RingProximal, XRHandJointID.RingIntermediate, XRHandJointID.RingDistal, XRHandJointID.RingTip },
            new[] { XRHandJointID.Wrist, XRHandJointID.LittleMetacarpal, XRHandJointID.LittleProximal, XRHandJointID.LittleIntermediate, XRHandJointID.LittleDistal, XRHandJointID.LittleTip },
            new[] { XRHandJointID.ThumbProximal, XRHandJointID.IndexProximal, XRHandJointID.MiddleProximal, XRHandJointID.RingProximal, XRHandJointID.LittleProximal },
        };

        sealed class HandRig
        {
            public Transform Root;
            public readonly Dictionary<XRHandJointID, Transform> Joints = new Dictionary<XRHandJointID, Transform>();
            public readonly List<(XRHandJointID a, XRHandJointID b, Transform bone)> Bones = new List<(XRHandJointID, XRHandJointID, Transform)>();
            public readonly Dictionary<XRHandJointID, Vector3> Pos = new Dictionary<XRHandJointID, Vector3>();
        }

        void Awake() => player = GetComponent<ArenaPlayer>();

        // Cor das mãos (Muralha Impenetrável = vermelho); null volta à pele.
        static Material skinMat;
        static readonly Color SkinColor = new Color(0.86f, 0.66f, 0.52f);
        public static void Tint(Color? c) { if (skinMat) skinMat.color = c ?? SkinColor; }

        void Start()
        {
            var skin = ArenaUtil.Lit(SkinColor);
            skinMat = skin;
            var knuckle = ArenaUtil.Lit(new Color(0.78f, 0.58f, 0.46f));
            left = Build("MaoRastreadaEsquerda", skin, knuckle);
            right = Build("MaoRastreadaDireita", skin, knuckle);
            Application.onBeforeRender += OnBeforeRender;
        }

        void OnDestroy()
        {
            Application.onBeforeRender -= OnBeforeRender;
            if (left?.Root) Destroy(left.Root.gameObject);
            if (right?.Root) Destroy(right.Root.gameObject);
        }

        HandRig Build(string name, Material skin, Material knuckle)
        {
            var h = new HandRig { Root = new GameObject(name).transform };
            for (int i = XRHandJointID.BeginMarker.ToIndex(); i < XRHandJointID.EndMarker.ToIndex(); i++)
            {
                var id = XRHandJointIDUtility.FromIndex(i);
                float r = id == XRHandJointID.Palm ? 0.001f : IsTip(id) ? 0.008f : 0.011f;
                var j = ArenaUtil.Prim(PrimitiveType.Sphere, h.Root, Vector3.zero, Vector3.one * r * 2f, IsTip(id) ? skin : knuckle).transform;
                h.Joints[id] = j;
            }
            var seen = new HashSet<(XRHandJointID, XRHandJointID)>();
            foreach (var chain in Chains)
                for (int k = 0; k + 1 < chain.Length; k++)
                {
                    if (!seen.Add((chain[k], chain[k + 1]))) continue;
                    var bone = ArenaUtil.Prim(PrimitiveType.Cylinder, h.Root, Vector3.zero, Vector3.one * 0.01f, skin).transform;
                    h.Bones.Add((chain[k], chain[k + 1], bone));
                }
            foreach (var c in h.Root.GetComponentsInChildren<Collider>()) Destroy(c);
            h.Root.gameObject.SetActive(false);
            return h;
        }

        static bool IsTip(XRHandJointID id) => id == XRHandJointID.ThumbTip || id == XRHandJointID.IndexTip || id == XRHandJointID.MiddleTip
                                                || id == XRHandJointID.RingTip || id == XRHandJointID.LittleTip;

        void LateUpdate() => Refresh();
        void OnBeforeRender() => Refresh();   // pose mais recente logo antes de desenhar (menos atraso)

        void Refresh()
        {
            if (!player || left == null) return;
            if (!player.IsVr) { left.Root.gameObject.SetActive(false); right.Root.gameObject.SetActive(false); return; }
            if (hands == null || !hands.running)
            {
                SubsystemManager.GetSubsystems(buffer);
                hands = null;
                foreach (var s in buffer) if (s.running) { hands = s; break; }
            }
            for (int side = 0; side < 2; side++)
            {
                var rig = side == 0 ? left : right;
                bool free = side == 0 ? player.Input.LeftHandFree : player.Input.RightHandFree;
                if (free)
                {
                    bool tracked = hands != null && (side == 0 ? hands.leftHand : hands.rightHand).isTracked;
                    Pose(rig, tracked ? (side == 0 ? hands.leftHand : hands.rightHand) : default, tracked, side);
                    continue;
                }
                var w = side == 1 ? player.Weapon : player.WeaponL;
                var t = side == 0 ? player.leftHand : player.rightHand;
                // Segurando arma: a mão é desenhada grudada no cabo (fechada), não onde está o controle.
                if (w && w.Held && w.TryGetGluedHand(out var gp, out var gr)) ControllerHand(rig, side, gp, gr, true);
                else ControllerHand(rig, side, t.position, t.rotation, false);
            }
        }

        // ---------- Mão em cima do controle ----------
        // Pose montada no espaço do controle (x para fora da mão direita; espelhado na esquerda). Os dedos saem do lado de
        // fora do cabo e enrolam em volta dele (giro no eixo vertical do controle) conforme o grip/gatilho.
        static readonly XRHandJointID[][] FingerJoints =
        {
            new[] { XRHandJointID.IndexMetacarpal, XRHandJointID.IndexProximal, XRHandJointID.IndexIntermediate, XRHandJointID.IndexDistal, XRHandJointID.IndexTip },
            new[] { XRHandJointID.MiddleMetacarpal, XRHandJointID.MiddleProximal, XRHandJointID.MiddleIntermediate, XRHandJointID.MiddleDistal, XRHandJointID.MiddleTip },
            new[] { XRHandJointID.RingMetacarpal, XRHandJointID.RingProximal, XRHandJointID.RingIntermediate, XRHandJointID.RingDistal, XRHandJointID.RingTip },
            new[] { XRHandJointID.LittleMetacarpal, XRHandJointID.LittleProximal, XRHandJointID.LittleIntermediate, XRHandJointID.LittleDistal, XRHandJointID.LittleTip },
        };
        static readonly float[] FingerY = { -0.005f, -0.025f, -0.043f, -0.06f };
        static readonly float[] FingerScale = { 1f, 1.05f, 0.97f, 0.8f };
        readonly Dictionary<XRHandJointID, Vector3> synth = new Dictionary<XRHandJointID, Vector3>();

        readonly List<XRHandJointID> synthKeys = new List<XRHandJointID>();

        void ControllerHand(HandRig h, int side, Vector3 origin, Quaternion rot, bool onWeapon)
        {
            var c = side == 0 ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch;
            float mx = side == 0 ? -1f : 1f;   // espelho da mão esquerda
            float grip = Mathf.Max(onWeapon ? 0.9f : 0.12f, OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, c));
            float trig = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, c);
            bool indexNear = OVRInput.Get(OVRInput.NearTouch.PrimaryIndexTrigger, c) || trig > 0.05f;
            float index = indexNear ? Mathf.Max(0.35f, trig) : 0.08f;   // dedo fora do gatilho = apontando
            bool thumbDown = OVRInput.Get(OVRInput.NearTouch.PrimaryThumbButtons, c) || OVRInput.Get(OVRInput.Touch.PrimaryThumbstick, c)
                             || OVRInput.Get(OVRInput.Touch.One, c) || OVRInput.Get(OVRInput.Touch.Two, c);
            synth.Clear();
            Vector3 P(float x, float y, float z) => origin + rot * new Vector3(x * mx, y, z);
            var wrist = new Vector3(0.012f, -0.035f, -0.095f);
            synth[XRHandJointID.Wrist] = P(wrist.x, wrist.y, wrist.z);
            synth[XRHandJointID.Palm] = P(0.018f, -0.03f, -0.045f);
            float[] seg = { 0.042f, 0.026f, 0.021f };
            float[] bend = { 55f, 65f, 45f };
            for (int f = 0; f < 4; f++)
            {
                float curl = f == 0 ? index : grip;
                var ids = FingerJoints[f];
                var b = new Vector3(0.026f, FingerY[f], -0.012f);
                synth[ids[0]] = P((wrist.x + b.x) * 0.5f, (wrist.y + b.y) * 0.5f, (wrist.z + b.z) * 0.5f);
                synth[ids[1]] = P(b.x, b.y, b.z);
                var dir = new Vector3(-0.12f, -0.08f, 1f).normalized;   // dedo esticado: para frente
                float ang = 0f;
                var p = b;
                for (int k = 0; k < 3; k++)
                {
                    ang += bend[k] * curl;
                    var d = Quaternion.AngleAxis(-ang, Vector3.up) * dir;   // enrola em volta do cabo (para dentro, -x)
                    p += d * seg[k] * FingerScale[f];
                    synth[ids[k + 2]] = P(p.x, p.y, p.z);
                }
            }
            // Polegar: em cima do controle (encostado nos botões) ou levantado.
            synth[XRHandJointID.ThumbMetacarpal] = P(-0.005f, -0.02f, -0.07f);
            synth[XRHandJointID.ThumbProximal] = P(-0.012f, -0.0f, -0.045f);
            synth[XRHandJointID.ThumbDistal] = thumbDown ? P(-0.008f, 0.018f, -0.018f) : P(-0.012f, 0.03f, -0.025f);
            synth[XRHandJointID.ThumbTip] = thumbDown ? P(-0.002f, 0.022f, 0.004f) : P(-0.01f, 0.05f, -0.008f);
            // Segurando o cabo da espada cravada: a mão fica grudada nele enquanto puxa.
            if (StuckSword.TryGetHeldHilt(side, out var hilt))
            {
                var off = hilt - synth[XRHandJointID.Palm];
                synthKeys.Clear(); synthKeys.AddRange(synth.Keys);
                foreach (var key in synthKeys) synth[key] += off;
            }
            PoseFrom(h, synth);
        }

        void PoseFrom(HandRig h, Dictionary<XRHandJointID, Vector3> pos)
        {
            if (!h.Root.gameObject.activeSelf) h.Root.gameObject.SetActive(true);
            h.Pos.Clear();
            foreach (var kv in h.Joints)
            {
                if (!pos.TryGetValue(kv.Key, out var w)) { kv.Value.gameObject.SetActive(false); continue; }
                kv.Value.gameObject.SetActive(true);
                kv.Value.position = w;
                h.Pos[kv.Key] = w;
            }
            DrawBones(h);
        }

        void Pose(HandRig h, XRHand hand, bool show, int side)
        {
            if (!show) { if (h.Root.gameObject.activeSelf) h.Root.gameObject.SetActive(false); return; }
            if (!h.Root.gameObject.activeSelf) h.Root.gameObject.SetActive(true);
            var space = player.trackingSpace;
            // Agarrando o goblin físico: a mão é desenhada presa no ponto agarrado (por fora do corpo), não onde a mão real está.
            var offset = Vector3.zero;
            var grabber = EnemyGrabber.Instance;
            if (grabber && grabber.TryGetGrabPoint(side, out var gp) && hand.GetJoint(XRHandJointID.Palm).TryGetPose(out UnityEngine.Pose palmPose))
                offset = gp - space.TransformPoint(palmPose.position);
            else if (StuckSword.TryGetHeldHilt(side, out var hilt) && hand.GetJoint(XRHandJointID.Palm).TryGetPose(out UnityEngine.Pose palm2))
                offset = hilt - space.TransformPoint(palm2.position);   // mão livre segurando o cabo da espada cravada
            foreach (var kv in h.Joints)
            {
                if (!hand.GetJoint(kv.Key).TryGetPose(out UnityEngine.Pose p)) { kv.Value.gameObject.SetActive(false); h.Pos.Remove(kv.Key); continue; }
                var w = space.TransformPoint(p.position) + offset;
                kv.Value.gameObject.SetActive(true);
                kv.Value.position = w;
                h.Pos[kv.Key] = w;
            }
            DrawBones(h);
        }

        void DrawBones(HandRig h)
        {
            foreach (var (a, b, bone) in h.Bones)
            {
                if (!h.Pos.TryGetValue(a, out var pa) || !h.Pos.TryGetValue(b, out var pb)) { bone.gameObject.SetActive(false); continue; }
                var d = pb - pa;
                float len = d.magnitude;
                if (len < 1e-4f) { bone.gameObject.SetActive(false); continue; }
                bone.gameObject.SetActive(true);
                bone.position = (pa + pb) * 0.5f;
                bone.rotation = Quaternion.FromToRotation(Vector3.up, d / len);
                // Ossos da palma (metacarpos e linha dos nós) um pouco mais grossos.
                float thick = a == XRHandJointID.Wrist || b == XRHandJointID.IndexProximal && a == XRHandJointID.ThumbProximal ? 0.018f : 0.014f;
                bone.localScale = new Vector3(thick, len * 0.5f, thick);
            }
        }
    }
}
