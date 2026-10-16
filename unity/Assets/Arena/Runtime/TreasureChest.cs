using System.Collections.Generic;
using Arena.Core;
using UnityEngine;

namespace Arena
{
    // Baú da dungeon: aparece quando a sala é limpa. Grip perto (VR) ou E (desktop) abre: ouro + às vezes uma arma.
    public sealed class TreasureChest : MonoBehaviour
    {
        public static readonly List<TreasureChest> All = new List<TreasureChest>();
        public const float OpenRange = 1.6f;

        int floor;
        bool opened;
        Transform lid;
        float openT;

        public static TreasureChest Spawn(Vector3 pos, Vector3 facing, int floor, Transform parent)
        {
            var go = new GameObject("Bau");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.LookRotation(facing.Flat().sqrMagnitude > 0.01f ? facing.Flat() : Vector3.forward);
            var c = go.AddComponent<TreasureChest>();
            c.floor = floor;
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

        void Update()
        {
            if (!opened || openT >= 1f) return;
            openT = Mathf.Min(1f, openT + Time.deltaTime / 0.4f);
            lid.localRotation = Quaternion.Euler(-110f * Mathf.SmoothStep(0f, 1f, openT), 0, 0);
        }

        void OnDestroy() => All.Remove(this);

        public static void ClearAll()
        {
            for (int i = All.Count - 1; i >= 0; i--) if (All[i]) Destroy(All[i].gameObject);
            All.Clear();
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
            var (gold, item) = game ? game.Loot.RollChest(floor) : (10, null);
            player.State.AddGold(gold);
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
            else game?.Message("Baú: " + gold + " de ouro", 1.2f);
            game?.RequestSave();
        }
    }
}
