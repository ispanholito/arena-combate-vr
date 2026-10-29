using Arena.Core;
using UnityEngine;

namespace Arena
{
    // Correr balançando os braços (Core/ArmSwing.cs): só com as DUAS mãos livres e a opção ligada na pausa.
    // Anda na direção do olhar; enquanto há balanço, trava os poderes das duas mãos (corte, soco, raio, magias, teia).
    [RequireComponent(typeof(ArenaPlayer))]
    public sealed class ArmSwingRun : MonoBehaviour
    {
        ArenaPlayer player;
        HandMagic magic;
        readonly ArmSwingDetector detector = new ArmSwingDetector();
        float stepDist, openL, openR;
        bool announced;

        public bool Running => detector.Running;

        void Awake() => player = GetComponent<ArenaPlayer>();

        void Update()
        {
            float dt = Time.deltaTime;
            bool allowed = player.IsVr && SaveSystem.Options.armSwingRun && !ArenaGame.Blocked && !player.TreeOpen && !player.Hanging
                           && player.Input.LeftHandFree && player.Input.RightHandFree;
            if (!magic) magic = GetComponent<HandMagic>();
            if (!allowed || !magic || !magic.GetHand(0, out var lp, out var lv, out var ls) || !magic.GetHand(1, out var rp, out var rv, out var rs))
            {
                detector.Reset();
                return;
            }
            // As duas mãos ABERTAS (2ª versão): com punho ou dedos apontando não corre.
            if (ls == HandShape.Aberta) openL = 0.25f; else openL -= dt;
            if (rs == HandShape.Aberta) openR = 0.25f; else openR -= dt;   // tolera um quadro de rastreamento ruim
            bool open = openL > 0f && openR > 0f;
            var fwd = player.HeadForward;
            var arc = (fwd + Vector3.up).normalized;   // arco da corrida: frente-e-cima / trás-e-baixo
            float chest = player.HeadPosition.y - 0.2f;
            if (!open) detector.Update(0f, 0f, false, dt);
            else detector.Update(Vector3.Dot(lv, arc), Vector3.Dot(rv, arc), lp.y < chest && rp.y < chest, dt);
            // Balançando (mesmo antes de virar corrida): as mãos não soltam poderes.
            if (detector.Swinging || detector.Running) HandLock.HoldBoth("correr", 0.4f);
            if (detector.Speed <= 0.01f) { announced = false; return; }
            if (!announced) { announced = true; TutorialEvents.Raise("correr"); }
            float speed = detector.Speed * player.State.Blessings.MoveMultiplier;
            player.MoveRig(fwd * speed * dt);
            stepDist += speed * dt;
            if (stepDist > 0.9f) { stepDist = 0f; ArenaAudio.Play(Sfx.Step, player.FeetPosition, 0.4f, Random.Range(0.9f, 1.15f)); }
            if (SaveSystem.Options.moveVignette) player.Vignette(GameOptions.MoveVignetteLevel * Mathf.Min(1f, speed / 3f));
        }
    }
}
