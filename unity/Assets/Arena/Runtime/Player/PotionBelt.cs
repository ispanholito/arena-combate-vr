using System.Collections.Generic;
using Arena.Core;
using UnityEngine;

namespace Arena
{
    // Poção no peito (Pablo, 2026-10-30: "deixar ela no peito"). Os frascos ficam numa faixa no peito, um por poção.
    // Leve a mão até um frasco e PEGUE (grip do controle esquerdo, ou mão livre fechando/pinça), leve à boca e segure
    // um instante = bebe. Soltar longe da boca devolve o frasco ao peito. Desktop: B continua bebendo direto.
    [RequireComponent(typeof(ArenaPlayer))]
    public sealed class PotionBelt : MonoBehaviour
    {
        const float GrabRange = 0.14f, MouthRange = 0.16f, DrinkSeconds = 0.3f;
        const float Spacing = 0.075f;

        ArenaPlayer player;
        Transform belt;
        readonly List<Transform> flasks = new List<Transform>();
        readonly int[] held = { -1, -1 };          // frasco segurado por mão (0 = esquerda, 1 = direita)
        readonly bool[] wasGrab = new bool[2];
        readonly float[] mouthT = new float[2];

        void Awake() => player = GetComponent<ArenaPlayer>();

        void LateUpdate()
        {
            if (!player.IsVr || player.State == null) { if (belt) belt.gameObject.SetActive(false); return; }
            if (!belt) Build();
            belt.gameObject.SetActive(!ArenaGame.InMenu);
            PlaceBelt();
            Sync();
            if (ArenaGame.Blocked || player.TreeOpen) { Release(0); Release(1); return; }
            for (int side = 0; side < 2; side++) UpdateHand(side);
        }

        // Faixa na altura do peito, um pouco à esquerda (a mão esquerda é a que costuma ficar livre), virada com o corpo.
        void PlaceBelt()
        {
            var head = player.Head;
            var fwd = head.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
            fwd.Normalize();
            var right = Vector3.Cross(Vector3.up, fwd);
            belt.position = head.position - Vector3.up * 0.34f + fwd * 0.13f - right * 0.05f;
            belt.rotation = Quaternion.LookRotation(fwd);
        }

        void Build()
        {
            belt = new GameObject("FaixaDePocoes").transform;
            var strap = ArenaUtil.Lit(new Color(0.42f, 0.26f, 0.14f));
            ArenaUtil.Prim(PrimitiveType.Cube, belt, new Vector3(0f, -0.04f, -0.02f), new Vector3(0.3f, 0.035f, 0.015f), strap);
        }

        Transform MakeFlask()
        {
            var f = new GameObject("Pocao").transform;
            f.SetParent(belt, false);
            var glass = ArenaUtil.Lit(new Color(0.95f, 0.15f, 0.25f), new Color(0.5f, 0.03f, 0.08f));
            ArenaUtil.Prim(PrimitiveType.Sphere, f, Vector3.zero, new Vector3(0.05f, 0.055f, 0.05f), glass);
            ArenaUtil.Prim(PrimitiveType.Cylinder, f, new Vector3(0f, 0.035f, 0f), new Vector3(0.022f, 0.015f, 0.022f), ArenaUtil.Lit(new Color(0.85f, 0.85f, 0.9f)));
            ArenaUtil.Prim(PrimitiveType.Cylinder, f, new Vector3(0f, 0.055f, 0f), new Vector3(0.018f, 0.008f, 0.018f), ArenaUtil.Lit(new Color(0.55f, 0.35f, 0.18f)));
            return f;
        }

        // Um frasco por poção; os que estão na mão não voltam para a faixa.
        void Sync()
        {
            int n = Mathf.Max(0, player.State.Potions);
            while (flasks.Count < n) flasks.Add(MakeFlask());
            while (flasks.Count > n)
            {
                int last = flasks.Count - 1;
                for (int s = 0; s < 2; s++) if (held[s] == last) held[s] = -1;
                Destroy(flasks[last].gameObject);
                flasks.RemoveAt(last);
            }
            float x0 = -Spacing * (flasks.Count - 1) * 0.5f;
            for (int i = 0; i < flasks.Count; i++)
            {
                if (held[0] == i || held[1] == i) continue;
                flasks[i].SetParent(belt, false);
                flasks[i].localPosition = new Vector3(x0 + i * Spacing, 0f, 0f);
                flasks[i].localRotation = Quaternion.identity;
            }
        }

        void UpdateHand(int side)
        {
            var hand = side == 0 ? player.leftHand : player.rightHand;
            bool free = side == 0 ? player.Input.LeftHandFree : player.Input.RightHandFree;
            // A mão direita com controle segura a arma: só a esquerda (controle ou livre) e a direita livre pegam poção.
            if (!hand || (side == 1 && !free)) { Release(side); return; }
            bool grab = free ? FreeHandClosed(side) : player.Input.LeftGripHeld;
            bool started = grab && !wasGrab[side];
            wasGrab[side] = grab;

            if (held[side] < 0)
            {
                if (!started) return;
                int best = -1; float bestD = GrabRange;
                for (int i = 0; i < flasks.Count; i++)
                {
                    if (held[1 - side] == i) continue;
                    float d = Vector3.Distance(flasks[i].position, hand.position);
                    if (d < bestD) { bestD = d; best = i; }
                }
                if (best < 0) return;
                held[side] = best; mouthT[side] = 0f;
                flasks[best].SetParent(null, true);
                ArenaInput.Haptic(0.35f, 0.04f, left: side == 0);
                ArenaAudio.Play(Sfx.Click, hand.position, 0.4f, 1.6f);
                return;
            }

            var f = flasks[held[side]];
            f.position = hand.position + hand.forward * 0.04f;
            f.rotation = hand.rotation;
            if (free) HandLock.Hold(side, "pocao", 0.3f);   // segurando o frasco, a mão livre não solta poderes
            if (!grab) { Release(side); return; }
            var head = player.Head;
            var mouth = head.position - head.up * 0.09f + head.forward * 0.05f;
            mouthT[side] = Vector3.Distance(f.position, mouth) < MouthRange ? mouthT[side] + Time.deltaTime : 0f;
            if (mouthT[side] >= DrinkSeconds)
            {
                held[side] = -1;
                player.DrinkPotion();   // a poção some da faixa no próximo Sync (State.Potions diminuiu)
            }
        }

        void Release(int side)
        {
            held[side] = -1; mouthT[side] = 0f;
        }

        bool FreeHandClosed(int side) => HandPoses.Closed(player, side);
    }
}
