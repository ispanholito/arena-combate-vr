using Arena.Core;
using UnityEngine;

namespace Arena
{
    // Trocar de arma sem botão (pedido do Pablo, 2026-10-29): com a mão LIVRE (sem controle), leve a mão por cima do
    // ombro, atrás da cabeça, e FECHE — como puxar a outra arma das costas. Útil quando só um controle está na mão
    // (o clique do analógico esquerdo continua trocando quando o controle esquerdo está na mão).
    [RequireComponent(typeof(ArenaPlayer))]
    public sealed class WeaponSwapGesture : MonoBehaviour
    {
        const float Behind = 0.04f, MinSide = 0.02f, MaxSide = 0.45f, Below = 0.45f, Above = 0.2f, Cooldown = 1f;
        ArenaPlayer player;
        HandMagic magic;
        readonly HandShape[] last = new HandShape[2];
        float cd;

        void Awake() { player = GetComponent<ArenaPlayer>(); }

        void Update()
        {
            cd -= Time.deltaTime;
            if (!player.IsVr || ArenaGame.Blocked || player.TreeOpen) return;
            if (!magic) magic = GetComponent<HandMagic>();
            if (!magic) return;
            var head = player.Head;
            for (int side = 0; side < 2; side++)
            {
                bool free = side == 0 ? player.Input.LeftHandFree : player.Input.RightHandFree;
                if (!free || !magic.GetHand(side, out var palm, out _, out var shape)) { last[side] = HandShape.Nenhuma; continue; }
                bool fistOnset = shape == HandShape.Punho && last[side] != HandShape.Punho;
                if (shape != HandShape.Nenhuma) last[side] = shape;
                if (!fistOnset || cd > 0f) continue;
                var rel = palm - head.position;
                float fwd = Vector3.Dot(rel, player.HeadForward);
                float lat = Mathf.Abs(Vector3.Dot(rel, Vector3.Cross(Vector3.up, player.HeadForward)));
                float up = rel.y;
                if (fwd > Behind || lat < MinSide || lat > MaxSide || up < -Below || up > Above) continue;
                var st = player.State;
                if (st.Loadout[1 - st.ActiveSlot] == null) { ArenaGame.Instance?.Message("Sem 2ª arma", 0.8f); cd = Cooldown; continue; }
                cd = Cooldown;
                HandLock.Hold(side, "troca", 0.6f);
                player.SwitchWeapon();
                ArenaAudio.Play(Sfx.Whoosh, palm, 0.7f, 1.3f);
                ArenaInput.Haptic(0.5f, 0.08f, left: side == 0);
                ArenaGame.Instance?.Message("Arma: " + player.Weapon.Def.Name, 1f);
            }
        }
    }
}
