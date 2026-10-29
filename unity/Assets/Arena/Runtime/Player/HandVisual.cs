using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace Arena
{
    // Mostra as mãos rastreadas (XR Hands) no jogo: uma esfera por articulação e um "osso" (cilindro) entre elas.
    // Cada mão aparece quando está livre (sem o controle nela).
    // Com o controle na mão a mão rastreada some (a arma tem a luva e o escudo fica na mão do controle).
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
            bool showLeft = hands != null && hands.leftHand.isTracked && player.Input.LeftHandFree;
            bool showRight = hands != null && hands.rightHand.isTracked && player.Input.RightHandFree;
            Pose(left, showLeft ? hands.leftHand : default, showLeft, 0);
            Pose(right, showRight ? hands.rightHand : default, showRight, 1);
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
            foreach (var kv in h.Joints)
            {
                if (!hand.GetJoint(kv.Key).TryGetPose(out UnityEngine.Pose p)) { kv.Value.gameObject.SetActive(false); h.Pos.Remove(kv.Key); continue; }
                var w = space.TransformPoint(p.position) + offset;
                kv.Value.gameObject.SetActive(true);
                kv.Value.position = w;
                h.Pos[kv.Key] = w;
            }
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
