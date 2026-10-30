using System.Collections.Generic;
using Arena.Core;
using UnityEngine;

namespace Arena
{
    // Estátua dos deuses (inspiração Soul Knight): mostra uma bênção e o preço; grip/E perto paga o ouro e recebe.
    // Dungeon: 1 por andar, uma vez. Arena: uma perto da parede, sorteia outra depois de cada compra (mais cara).
    public sealed class BlessingStatue : MonoBehaviour
    {
        public static readonly List<BlessingStatue> All = new List<BlessingStatue>();
        const float UseRange = 1.7f;
        BlessingId offer;
        int price;
        bool repeat, used;
        TextMesh label;
        Transform orb;
        static readonly System.Random rng = new System.Random();

        public static int Price(int tierOrFloor, int timesBought) => 40 + 25 * tierOrFloor + 30 * timesBought;

        public static BlessingStatue Spawn(Vector3 pos, Transform parent, int price, bool repeat)
        {
            if (!Demo.Blessings) return null;
            var go = new GameObject("Estatua");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(pos.x, 0f, pos.z);
            var s = go.AddComponent<BlessingStatue>();
            s.price = price; s.repeat = repeat;
            s.Build();
            All.Add(s);
            return s;
        }

        void Build()
        {
            var stone = ArenaUtil.Lit(new Color(0.55f, 0.55f, 0.6f));
            ArenaUtil.Prim(PrimitiveType.Cube, transform, new Vector3(0f, 0.3f, 0f), new Vector3(0.9f, 0.6f, 0.9f), stone, keepCollider: true);
            ArenaUtil.Prim(PrimitiveType.Capsule, transform, new Vector3(0f, 1.35f, 0f), new Vector3(0.45f, 0.75f, 0.4f), stone);
            ArenaUtil.Prim(PrimitiveType.Sphere, transform, new Vector3(0f, 2.25f, 0f), Vector3.one * 0.32f, stone);
            foreach (int s in new[] { -1, 1 })   // braços erguidos segurando o orbe
                ArenaUtil.Prim(PrimitiveType.Cube, transform, new Vector3(s * 0.25f, 2.3f, 0f), new Vector3(0.09f, 0.5f, 0.09f), stone).transform.localRotation = Quaternion.Euler(0, 0, s * 25f);
            label = ArenaFx.MakeText(transform, "", 0.008f, new Color(1f, 0.9f, 0.6f));
            label.transform.localPosition = new Vector3(0f, 3.4f, 0f);
            Roll();
        }

        void Roll()
        {
            var choices = ArenaGame.Instance ? ArenaGame.Instance.Player.State.Blessings.RollChoices(rng, 1) : null;
            if (choices == null || choices.Count == 0) { used = true; label.text = "ESTÁTUA DOS DEUSES\n<size=40>(você já tem todas)</size>"; return; }
            offer = choices[0];
            var info = RunBlessings.Info(offer);
            var c = new Color(info.R, info.G, info.B);
            if (orb) Destroy(orb.gameObject);
            orb = ArenaUtil.Prim(PrimitiveType.Sphere, transform, new Vector3(0f, 2.75f, 0f), Vector3.one * 0.3f, ArenaUtil.Lit(c, c * 1.5f)).transform;
            label.text = "ESTÁTUA DOS DEUSES\n<size=44>" + info.Name + "</size>\n<size=36>" + info.Desc + "\n" + price + " ouro · grip / E</size>";
            label.color = c;
        }

        void Update()
        {
            var cam = Camera.main;
            if (cam && label) { var d = label.transform.position - cam.transform.position; d.y = 0f; if (d.sqrMagnitude > 0.01f) label.transform.rotation = Quaternion.LookRotation(d); }
            if (orb) orb.localPosition = new Vector3(0f, 2.75f + Mathf.Sin(Time.time * 2f) * 0.05f, 0f);
        }

        public static bool TryUse(ArenaPlayer player)
        {
            foreach (var s in All)
            {
                if (!s || s.used) continue;
                float d = player.IsVr
                    ? Mathf.Min(Vector3.Distance(player.RightHand.position, s.transform.position + Vector3.up * 1.2f), (player.FeetPosition - s.transform.position).Flat().magnitude)
                    : (player.FeetPosition - s.transform.position).Flat().magnitude;
                if (d > UseRange) continue;
                s.Use(player);
                return true;
            }
            return false;
        }

        void Use(ArenaPlayer player)
        {
            var st = player.State;
            var game = ArenaGame.Instance;
            if (st.Gold < price) { game?.Message("A estátua pede " + price + " de ouro (você tem " + st.Gold + ")", 1.5f); ArenaAudio.Play2D(Sfx.Deny, 0.6f); return; }
            st.SpendGold(price);
            st.ChooseBlessing(offer);
            var info = RunBlessings.Info(offer);
            ArenaFx.Burst(transform.position + Vector3.up * 2.7f, new Color(info.R, info.G, info.B), 30, 4f);
            ArenaAudio.Play2D(Sfx.Level, 0.9f, 1.3f);
            ArenaInput.Haptic(0.7f, 0.15f);
            game?.Message("Bênção da estátua: " + info.Name + " — " + info.Desc, 2.5f);
            game?.RequestSave();
            if (repeat) { price += 30; Roll(); return; }
            used = true;
            if (orb) Destroy(orb.gameObject);
            label.text = "ESTÁTUA DOS DEUSES\n<size=40>(já abençoou você)</size>";
            label.color = new Color(0.6f, 0.6f, 0.6f);
        }

        void OnDestroy() => All.Remove(this);

        public static void ClearAll()
        {
            for (int i = All.Count - 1; i >= 0; i--) if (All[i]) Destroy(All[i].gameObject);
            All.Clear();
        }
    }
}
