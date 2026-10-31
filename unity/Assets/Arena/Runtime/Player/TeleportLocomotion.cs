using Arena.Core;
using UnityEngine;

namespace Arena
{
    // Teletransporte opcional (pausa → Locomoção; conforto para quem enjoa — a loja da Meta pede essa opção).
    // Empurre o analógico de andar para frente: sai um arco do controle daquele analógico até o chão; solte = vai.
    // Com o teletransporte ligado, o analógico não anda mais suave (giro, esquiva e o resto continuam iguais).
    [RequireComponent(typeof(ArenaPlayer))]
    public sealed class TeleportLocomotion : MonoBehaviour
    {
        const float AimOn = 0.6f, AimOff = 0.3f, Speed = 8f, Gravity = 9.81f, MaxRange = 8f, Cooldown = 0.35f;
        const int Steps = 40;
        ArenaPlayer player;
        LineRenderer arc;
        Transform marker;
        bool aiming, valid;
        Vector3 target;
        float cd;
        static readonly Color Ok = new Color(0.35f, 0.9f, 1f), Bad = new Color(1f, 0.35f, 0.3f);

        public static bool Enabled(ArenaPlayer p) => p && p.IsVr && SaveSystem.Options.teleport;

        void Awake() => player = GetComponent<ArenaPlayer>();

        void Update()
        {
            cd -= Time.deltaTime;
            if (!Enabled(player) || ArenaGame.Blocked || player.TreeOpen || player.Hanging || player.Bound) { Hide(); aiming = false; return; }
            var stick = player.Input.Move;
            if (!aiming && stick.y > AimOn && cd <= 0f) aiming = true;
            if (!aiming) { Hide(); return; }
            // Arco sai do controle do analógico de andar (esquerdo se estiver na mão; senão o direito).
            var hand = player.Input.LeftControllerConnected ? player.leftHand : player.rightHand;
            if (!hand) { Hide(); return; }
            Trace(hand.position, hand.forward);
            if (stick.magnitude < AimOff)
            {
                aiming = false; Hide();
                if (valid)
                {
                    cd = Cooldown;
                    player.TeleportTo(target, target + player.HeadForward);
                    player.Vignette(0.9f);
                    ArenaAudio.Play2D(Sfx.Dash, 0.4f, 1.3f);
                }
            }
        }

        void Trace(Vector3 from, Vector3 dir)
        {
            EnsureVisuals();
            var v = dir.normalized * Speed;
            var p = from;
            var feetY = player.FeetPosition.y;
            valid = false;
            int n = 0;
            arc.positionCount = Steps;
            for (; n < Steps; n++)
            {
                arc.SetPosition(n, p);
                var next = p + v * 0.05f;
                v += Vector3.down * Gravity * 0.05f;
                // Parede no caminho: para ali (inválido).
                if (Physics.Linecast(p, next, out var hit, ~0, QueryTriggerInteraction.Ignore) && !hit.collider.GetComponentInParent<ArenaPlayer>()
                    && !hit.collider.GetComponentInParent<PlayerWeapon>() && hit.normal.y < 0.7f)
                { next = hit.point; n++; arc.SetPosition(Mathf.Min(n, Steps - 1), next); break; }
                if (next.y <= feetY)
                {
                    float t = (p.y - feetY) / Mathf.Max(1e-4f, p.y - next.y);
                    next = Vector3.Lerp(p, next, t);
                    n++; arc.SetPosition(Mathf.Min(n, Steps - 1), next);
                    var flat = next - player.FeetPosition; flat.y = 0f;
                    valid = flat.magnitude <= MaxRange && !ArenaUtil.IsOutside(next, 0.3f);
                    target = new Vector3(next.x, feetY, next.z);
                    break;
                }
                p = next;
            }
            arc.positionCount = Mathf.Min(n + 1, Steps);
            var c = valid ? Ok : Bad;
            arc.startColor = c; arc.endColor = c;
            marker.gameObject.SetActive(valid);
            if (valid) marker.position = target + Vector3.up * 0.03f;
        }

        void EnsureVisuals()
        {
            if (arc) { arc.enabled = true; return; }
            var go = new GameObject("ArcoTeletransporte");
            arc = go.AddComponent<LineRenderer>();
            arc.material = ArenaUtil.Unlit(Ok);
            arc.widthMultiplier = 0.02f;
            arc.useWorldSpace = true;
            marker = ArenaUtil.Prim(PrimitiveType.Cylinder, null, Vector3.zero, new Vector3(0.6f, 0.01f, 0.6f), ArenaUtil.Unlit(Ok)).transform;
        }

        void Hide()
        {
            if (arc) arc.enabled = false;
            if (marker) marker.gameObject.SetActive(false);
        }
    }
}
