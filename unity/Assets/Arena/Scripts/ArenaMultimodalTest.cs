using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

// Teste do modo "multimodal": controle na mão direita + mão esquerda rastreada.
// Mostra um painel na frente do jogador com o estado de cada entrada e a pose detectada.
// Poses da mão esquerda: Apontar (laser), Punho (soco no chão), Aberta (corte).
public class ArenaMultimodalTest : MonoBehaviour
{
    public enum HandPoseKind { Nenhuma, Aberta, Punho, Apontar, Outra }

    public HandPoseKind CurrentPose { get; private set; }
    public Vector3 LeftWristVelocity { get; private set; }

    TextMesh label;
    LineRenderer laser;
    Transform trackingSpace;
    Transform swordTip;
    XRHandSubsystem hands;
    static readonly List<XRHandSubsystem> buffer = new List<XRHandSubsystem>();
    readonly List<Transform> jointDots = new List<Transform>();
    Vector3 prevWrist;
    bool hadWrist;
    float poseTime;
    string lastEvent = "-";
    float lastEventTime;

    void Start()
    {
        var rig = FindFirstObjectByType<OVRCameraRig>();
        Transform head = Camera.main != null ? Camera.main.transform : transform;
        if (rig != null)
        {
            trackingSpace = rig.trackingSpace;
            head = rig.centerEyeAnchor;
            // Espada simples no controle direito
            var sword = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(sword.GetComponent<Collider>());
            sword.name = "EspadaTeste";
            sword.transform.SetParent(rig.rightHandAnchor, false);
            sword.transform.localScale = new Vector3(0.04f, 0.01f, 0.9f);
            sword.transform.localPosition = new Vector3(0, 0, 0.5f);
            swordTip = new GameObject("Ponta").transform;
            swordTip.SetParent(sword.transform, false);
            swordTip.localPosition = new Vector3(0, 0, 0.5f);
        }

        // Painel de diagnóstico preso à cabeça
        var panel = new GameObject("Diagnostico");
        panel.transform.SetParent(head, false);
        panel.transform.localPosition = new Vector3(0f, -0.1f, 1.2f);
        label = panel.AddComponent<TextMesh>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        panel.GetComponent<MeshRenderer>().sharedMaterial = label.font.material;
        label.fontSize = 64;
        label.characterSize = 0.006f;
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.color = Color.white;

        // Laser
        laser = new GameObject("Laser").AddComponent<LineRenderer>();
        laser.positionCount = 2;
        laser.startWidth = laser.endWidth = 0.012f;
        laser.material = new Material(Shader.Find("Sprites/Default"));
        laser.startColor = laser.endColor = new Color(1f, 0.2f, 0.2f);
        laser.enabled = false;

        // Bolinhas nas juntas da mão esquerda
        for (int i = 0; i < XRHandJointID.EndMarker.ToIndex(); i++)
        {
            var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(dot.GetComponent<Collider>());
            dot.transform.localScale = Vector3.one * 0.012f;
            dot.SetActive(false);
            jointDots.Add(dot.transform);
        }
    }

    void Update()
    {
        if (hands == null || !hands.running)
        {
            SubsystemManager.GetSubsystems(buffer);
            hands = null;
            foreach (var s in buffer) if (s.running) { hands = s; break; }
        }

        bool rightCtrl = OVRInput.IsControllerConnected(OVRInput.Controller.RTouch);
        bool leftCtrl = OVRInput.IsControllerConnected(OVRInput.Controller.LTouch);
        bool leftHand = hands != null && hands.leftHand.isTracked;
        bool rightHand = hands != null && hands.rightHand.isTracked;

        // Pose e velocidade da mão esquerda
        var newPose = HandPoseKind.Nenhuma;
        if (leftHand)
        {
            var h = hands.leftHand;
            newPose = Detect(h);
            if (TryWorld(h, XRHandJointID.Wrist, out var wrist))
            {
                if (hadWrist && Time.deltaTime > 0f) LeftWristVelocity = (wrist - prevWrist) / Time.deltaTime;
                prevWrist = wrist;
                hadWrist = true;
            }
            DrawJoints(h);
        }
        else
        {
            hadWrist = false;
            foreach (var d in jointDots) d.gameObject.SetActive(false);
        }
        if (newPose != CurrentPose) { CurrentPose = newPose; poseTime = 0f; } else poseTime += Time.deltaTime;

        // Ações de teste
        laser.enabled = false;
        if (CurrentPose == HandPoseKind.Apontar && poseTime > 0.12f
            && TryWorld(hands.leftHand, XRHandJointID.IndexTip, out var tip)
            && TryWorld(hands.leftHand, XRHandJointID.IndexMetacarpal, out var baseJ))
        {
            laser.enabled = true;
            laser.SetPosition(0, tip);
            laser.SetPosition(1, tip + (tip - baseJ).normalized * 10f);
        }
        if (CurrentPose == HandPoseKind.Punho && LeftWristVelocity.y < -3f && prevWrist.y < 1.05f) Event("SOCO NO CHÃO");
        if (CurrentPose == HandPoseKind.Aberta && LeftWristVelocity.magnitude > 3.2f && poseTime > 0.1f) Event("CORTE DA PALMA");
        if (OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.RTouch)) { Event("BOTÃO A"); OVRInput.SetControllerVibration(0.5f, 0.6f, OVRInput.Controller.RTouch); Invoke(nameof(StopVibration), 0.15f); }
        if (OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.RTouch)) Event("BOTÃO B");

        float trig = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, OVRInput.Controller.RTouch);
        bool multimodal = rightCtrl && leftHand;
        label.text =
            $"<b>TESTE MULTIMODAL</b>\n" +
            $"Controle direito: {Yes(rightCtrl)}   Controle esquerdo: {Yes(leftCtrl)}\n" +
            $"Mão esquerda rastreada: {Yes(leftHand)}   Mão direita: {Yes(rightHand)}\n" +
            $"Pose mão esquerda: {CurrentPose}   Velocidade: {LeftWristVelocity.magnitude:0.0} m/s\n" +
            $"Gatilho direito: {trig:0.00}   Subsistema de mãos: {(hands != null ? "ativo" : "NÃO ENCONTRADO")}\n" +
            $"Último evento: {(Time.time - lastEventTime < 1.5f ? lastEvent : "-")}\n" +
            $"MULTIMODAL: {(multimodal ? "<color=#7CFC00>FUNCIONANDO</color>" : "<color=#FF6060>NÃO</color>")}";
    }

    void StopVibration() => OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.RTouch);

    void Event(string e)
    {
        if (Time.time - lastEventTime < 0.6f && e == lastEvent) return;
        lastEvent = e;
        lastEventTime = Time.time;
    }

    static string Yes(bool b) => b ? "<color=#7CFC00>SIM</color>" : "<color=#FF6060>NÃO</color>";

    bool TryWorld(XRHand hand, XRHandJointID id, out Vector3 world)
    {
        world = default;
        if (!hand.GetJoint(id).TryGetPose(out Pose p)) return false;
        world = trackingSpace != null ? trackingSpace.TransformPoint(p.position) : p.position;
        return true;
    }

    // Dedo esticado = ponta bem mais longe do pulso que a falange proximal
    bool? Extended(XRHand hand, XRHandJointID proximal, XRHandJointID tipId)
    {
        if (!TryWorld(hand, XRHandJointID.Wrist, out var w) || !TryWorld(hand, proximal, out var p) || !TryWorld(hand, tipId, out var t)) return null;
        return Vector3.Distance(t, w) > Vector3.Distance(p, w) + 0.045f;
    }

    HandPoseKind Detect(XRHand h)
    {
        var i = Extended(h, XRHandJointID.IndexProximal, XRHandJointID.IndexTip);
        var m = Extended(h, XRHandJointID.MiddleProximal, XRHandJointID.MiddleTip);
        var r = Extended(h, XRHandJointID.RingProximal, XRHandJointID.RingTip);
        var l = Extended(h, XRHandJointID.LittleProximal, XRHandJointID.LittleTip);
        if (i == null || m == null || r == null || l == null) return HandPoseKind.Nenhuma;
        if (i.Value && m.Value && r.Value && l.Value) return HandPoseKind.Aberta;
        if (!i.Value && !m.Value && !r.Value && !l.Value) return HandPoseKind.Punho;
        if (i.Value && !m.Value && !r.Value) return HandPoseKind.Apontar;
        return HandPoseKind.Outra;
    }

    void DrawJoints(XRHand h)
    {
        for (int k = 0; k < jointDots.Count; k++)
        {
            var id = XRHandJointIDUtility.FromIndex(k);
            bool ok = TryWorld(h, id, out var pos);
            jointDots[k].gameObject.SetActive(ok);
            if (ok) jointDots[k].position = pos;
        }
    }
}
