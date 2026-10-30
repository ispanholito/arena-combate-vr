using Arena.Core;
using UnityEngine;

namespace Arena
{
    // Fase 4: segurar a arma com as duas mãos.
    // VR com controle esquerdo: aproxime a mão do cabo e segure o grip esquerdo (o escudo vai para as costas).
    // VR com a mão livre: feche o punho encostado no cabo. Desktop: V liga/desliga.
    [RequireComponent(typeof(ArenaPlayer))]
    public sealed class TwoHandGrip : MonoBehaviour
    {
        ArenaPlayer player;
        LeftHandPowers hand;
        bool wasGripping, desktopOn;

        void Start()
        {
            player = GetComponent<ArenaPlayer>();
            hand = GetComponent<LeftHandPowers>();
        }

        void Update()
        {
            var w = player.Weapon;
            if (!w) return;
            if (ArenaGame.Blocked || player.TreeOpen || !w.Held) { w.ReleaseSecondHand(); wasGripping = false; return; }

            bool gripping; Vector3 handPos;
            if (!player.IsVr)
            {
                if (player.Input.DesktopTwoHand) desktopOn = !desktopOn;
                gripping = desktopOn;
                handPos = w.TwoHanded ? w.DesktopSecondHandPoint() : w.DesktopSecondHandGrabPoint();
            }
            else if (player.Input.LeftControllerConnected)
            {
                // Mão esquerda segurando escudo/arma: não pega a direita com as duas mãos.
                if (player.State.LeftItem != null) { w.ReleaseSecondHand(); wasGripping = false; return; }
                gripping = OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, OVRInput.Controller.LTouch) > 0.55f;
                handPos = player.leftHand.position;
            }
            else
            {
                handPos = Vector3.zero;
                gripping = hand && hand.CurrentShape == HandShape.Punho && hand.TryGetPalm(out var palm);
                if (gripping) hand.TryGetPalm(out handPos);
            }

            if (!gripping) w.ReleaseSecondHand();
            else if (!w.TwoHanded)
            {
                // Só pega no momento em que fecha a mão perto do cabo (no desktop, sempre que ligado).
                if ((!wasGripping || !player.IsVr) && !w.TryGrabSecondHand(handPos) && !player.IsVr)
                {
                    desktopOn = false;
                    ArenaGame.Instance?.Message("Esta arma não tem onde segurar com a outra mão aqui", 0.8f);
                }
                else if (w.TwoHanded) ArenaGame.Instance?.Message("Duas mãos", 0.6f);
            }
            else w.SetSecondHandTarget(handPos);
            wasGripping = gripping;
        }
    }
}
