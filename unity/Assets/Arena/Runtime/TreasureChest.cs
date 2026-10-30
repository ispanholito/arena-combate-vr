using System.Collections.Generic;
using Arena.Core;
using UnityEngine;

namespace Arena
{
    // Baú da dungeon: aparece quando a sala é limpa. Grip perto (VR) ou E (desktop) abre: ouro + às vezes uma arma.
    // No VR também abre com a MÃO: ponha a mão embaixo da frente da tampa e levante (2026-10-30, interação física).
    public sealed class TreasureChest : MonoBehaviour
    {
        public static readonly List<TreasureChest> All = new List<TreasureChest>();
        public const float OpenRange = 1.6f;

        int floor;
        bool opened, mimic;
        Transform lid;
        float openT;

        bool rare;   // baú do desafio / sala secreta: arma Rara ou melhor e o dobro de ouro

        public static TreasureChest Spawn(Vector3 pos, Vector3 facing, int floor, Transform parent, bool mimic = false, bool rare = false)
        {
            var go = new GameObject("Bau");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.LookRotation(facing.Flat().sqrMagnitude > 0.01f ? facing.Flat() : Vector3.forward);
            var c = go.AddComponent<TreasureChest>();
            c.floor = floor;
            c.mimic = mimic;
            c.rare = rare;
            c.Build();
            All.Add(c);
            ArenaFx.Burst(pos + Vector3.up * 0.4f, new Color(1f, 0.85f, 0.3f), 16, 2.5f);
            return c;
        }

        void Build()
        {
            var wood = ArenaUtil.Lit(new Color(0.4f, 0.24f, 0.1f));
            var gold = ArenaUtil.Lit(new Color(0.85f, 0.65f, 0.2f), new Color(0.25f, 0.18f, 0.04f));
            ArenaUtil.Prim(PrimitiveType.Cube, transform, new Vector3(0, 0.22f, 0), new Vector3(0.8f, 0.44f, 0.5f), wood, keepCollider: true);
            ArenaUtil.Prim(PrimitiveType.Cube, transform, new Vector3(0, 0.22f, 0.26f), new Vector3(0.12f, 0.14f, 0.03f), gold);
            lid = new GameObject("Tampa").transform;
            lid.SetParent(transform, false);
            lid.localPosition = new Vector3(0, 0.44f, -0.25f);   // dobradiça atrás
            ArenaUtil.Prim(PrimitiveType.Cube, lid, new Vector3(0, 0.07f, 0.25f), new Vector3(0.82f, 0.14f, 0.52f), wood);
            ArenaUtil.Prim(PrimitiveType.Cube, lid, new Vector3(0, 0.07f, 0.25f), new Vector3(0.84f, 0.04f, 0.1f), gold);
        }

        const float LiftReach = 0.2f, LiftSpeed = 0.7f;   // mão a até 20 cm da frente da tampa subindo a ≥ 0,7 m/s
        readonly Vector3[] prevHand = new Vector3[2];
        bool handInit;

        void Update()
        {
            if (!opened) { HandLift(); return; }
            if (openT >= 1f) return;
            openT = Mathf.Min(1f, openT + Time.deltaTime / 0.4f);
            lid.localRotation = Quaternion.Euler(-110f * Mathf.SmoothStep(0f, 1f, openT), 0, 0);
        }

        void HandLift()
        {
            var game = ArenaGame.Instance;
            var pl = game ? game.Player : null;
            if (!pl || !pl.IsVr || ArenaGame.Blocked || Time.deltaTime <= 0f) { handInit = false; return; }
            var edge = transform.TransformPoint(new Vector3(0f, 0.44f, 0.26f));   // frente da tampa (lado oposto à dobradiça)
            for (int side = 0; side < 2; side++)
            {
                var h = side == 0 ? pl.leftHand : pl.rightHand;
                if (!h) continue;
                var p = h.position;
                float up = handInit ? (p.y - prevHand[side].y) / Time.deltaTime : 0f;
                prevHand[side] = p;
                if (handInit && up >= LiftSpeed && Vector3.Distance(p, edge) <= LiftReach)
                {
                    ArenaInput.Haptic(0.5f, 0.06f, left: side == 0);
                    Open(pl);
                    return;
                }
            }
            handInit = true;
        }

        void OnDestroy() => All.Remove(this);

        public static void ClearAll()
        {
            for (int i = All.Count - 1; i >= 0; i--) if (All[i]) Destroy(All[i].gameObject);
            All.Clear();
        }

        // Era um mímico: o baú vira o monstro (que deixa o loot do baú ao morrer).
        void Reveal(ArenaGame game)
        {
            var pos = transform.position;
            var e = game.Spawn(EnemyDef.Mimico, pos, false);
            e.transform.rotation = transform.rotation;
            e.SpawnGrace = 0.4f;
            ArenaFx.Burst(pos + Vector3.up * 0.5f, new Color(0.55f, 0.1f, 0.2f), 24, 4f);
            ArenaAudio.Play(Sfx.Roar, pos, 0.8f, 1.8f);
            ArenaInput.Haptic(1f, 0.2f);
            game.Message("MÍMICO!", 1.5f);
            Destroy(gameObject);
        }

        // Abre o baú fechado mais próximo ao alcance. Retorna true se abriu.
        public static bool TryOpen(ArenaPlayer player)
        {
            TreasureChest best = null;
            float bestD = OpenRange;
            foreach (var c in All)
            {
                if (!c || c.opened) continue;
                float d = player.IsVr
                    ? Mathf.Min(Vector3.Distance(player.RightHand.position, c.transform.position + Vector3.up * 0.4f), (player.FeetPosition - c.transform.position).Flat().magnitude)
                    : (player.FeetPosition - c.transform.position).Flat().magnitude;
                if (d < bestD) { bestD = d; best = c; }
            }
            if (!best) return false;
            best.Open(player);
            return true;
        }

        void Open(ArenaPlayer player)
        {
            opened = true;
            var game = ArenaGame.Instance;
            if (mimic && game) { Reveal(game); return; }
            var (gold, item) = game ? game.Loot.RollChest(floor, game.CurrentTier) : (10, null);
            if (rare && game) { gold *= 2; item = game.Loot.RollItem(Rarity.Raro, game.CurrentTier); }
            gold = player.State.Blessings.ScaleGold(gold);
            player.State.AddGold(gold);
            bool potion = game && game.Loot.RollChestPotion() && player.State.AddPotion();
            var top = transform.position + Vector3.up * 0.8f;
            ArenaFx.FloatText("+" + gold + " ouro", top + Vector3.up * 0.3f, new Color(1f, 0.85f, 0.3f), 1f);
            ArenaFx.Burst(top, new Color(1f, 0.85f, 0.3f), 24, 3f);
            ArenaAudio.Play(Sfx.Level, top, 0.8f, 1.2f);
            ArenaInput.Haptic(0.6f, 0.1f);
            if (item != null)
            {
                WeaponPickup.Spawn(item, top, transform.forward * 1.2f + Vector3.up * 3f);
                game?.Message("Baú: " + item.DisplayName + " (" + ItemInstance.RarityName(item.Rarity) + ")", 1.8f);
            }
            else game?.Message("Baú: " + gold + " de ouro" + (potion ? " e uma poção" : ""), 1.2f);
            if (potion) ArenaFx.FloatText("+1 poção", top + Vector3.up * 0.6f, new Color(1f, 0.35f, 0.4f), 1f);
            game?.RequestSave();
        }
    }
}
