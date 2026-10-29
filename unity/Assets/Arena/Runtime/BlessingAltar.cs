using System.Collections.Generic;
using Arena.Core;
using UnityEngine;

namespace Arena
{
    // Altar de bênçãos (estilo Soul Knight): 3 orbes flutuando com nome e efeito. Tocar num orbe com qualquer mão
    // (VR) ou andar até ele (desktop) escolhe; os outros somem. Valem até o fim da partida.
    public sealed class BlessingAltar : MonoBehaviour
    {
        public static readonly List<BlessingAltar> All = new List<BlessingAltar>();
        const float OrbHeight = 1.15f, Spacing = 1.1f, TouchRange = 0.28f, WalkRange = 0.7f, ArmDelay = 0.8f;

        readonly List<(BlessingId id, Transform orb, Transform label)> orbs = new List<(BlessingId, Transform, Transform)>();
        float age;
        bool taken;

        public static BlessingAltar Spawn(Vector3 pos, Vector3 towardPlayer, List<BlessingId> choices, Transform parent)
        {
            var go = new GameObject("Altar de bênçãos");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(pos.x, 0f, pos.z);
            var f = towardPlayer.Flat();
            go.transform.rotation = Quaternion.LookRotation(f.sqrMagnitude > 0.01f ? f.normalized : Vector3.forward);
            var a = go.AddComponent<BlessingAltar>();
            a.Build(choices);
            All.Add(a);
            ArenaAudio.Play(Sfx.Skill, pos + Vector3.up, 0.7f, 1.4f);
            return a;
        }

        void Build(List<BlessingId> choices)
        {
            var stone = ArenaUtil.Lit(new Color(0.32f, 0.3f, 0.34f));
            for (int i = 0; i < choices.Count; i++)
            {
                var info = RunBlessings.Info(choices[i]);
                var col = new Color(info.R, info.G, info.B);
                float x = (i - (choices.Count - 1) * 0.5f) * Spacing;
                // Pedestal + orbe brilhante + nome/efeito em cima.
                ArenaUtil.Prim(PrimitiveType.Cylinder, transform, new Vector3(x, 0.4f, 0f), new Vector3(0.32f, 0.4f, 0.32f), stone);
                var orb = ArenaUtil.Prim(PrimitiveType.Sphere, transform, new Vector3(x, OrbHeight, 0f), Vector3.one * 0.26f,
                                         ArenaUtil.Lit(col, col * 1.6f)).transform;
                var label = new GameObject("Rotulo").transform;
                label.SetParent(transform, false);
                label.localPosition = new Vector3(x, OrbHeight + 0.5f, 0f);
                ArenaFx.MakeText(label, "<b>" + info.Name + "</b>\n<size=40>" + info.Desc + "</size>", 0.0085f, col);
                orbs.Add((info.Id, orb, label));
            }
            var l = new GameObject("Luz").AddComponent<Light>();
            l.transform.SetParent(transform, false);
            l.transform.localPosition = new Vector3(0f, 1.6f, 0.4f);
            l.type = LightType.Point; l.color = new Color(1f, 0.9f, 0.7f); l.range = 4f; l.intensity = 1.5f;
        }

        void Update()
        {
            var game = ArenaGame.Instance;
            if (taken || !game || ArenaGame.Blocked) return;
            var pl = game.Player;
            age += Time.deltaTime;
            for (int i = 0; i < orbs.Count; i++)
            {
                var o = orbs[i];
                var lp = o.orb.localPosition;
                lp.y = OrbHeight + Mathf.Sin(age * 2f + i) * 0.05f;
                o.orb.localPosition = lp;
                var toLabel = o.label.position - pl.HeadPosition;
                if (toLabel.sqrMagnitude > 0.01f) o.label.rotation = Quaternion.LookRotation(toLabel.Flat().sqrMagnitude > 0.01f ? toLabel.Flat() : toLabel);
            }
            if (age < ArmDelay) return;
            for (int i = 0; i < orbs.Count; i++)
            {
                var p = orbs[i].orb.position;
                bool touched = pl.IsVr
                    ? (pl.RightHand && Vector3.Distance(pl.RightHand.position, p) < TouchRange) || (pl.leftHand && Vector3.Distance(pl.leftHand.position, p) < TouchRange)
                    : (pl.FeetPosition - p).Flat().magnitude < WalkRange;
                if (touched) { Choose(i, pl, game); return; }
            }
        }

        void Choose(int index, ArenaPlayer pl, ArenaGame game)
        {
            taken = true;
            var o = orbs[index];
            var info = RunBlessings.Info(o.id);
            pl.State.ChooseBlessing(o.id);
            var col = new Color(info.R, info.G, info.B);
            ArenaFx.Burst(o.orb.position, col, 30, 4f);
            ArenaAudio.Play2D(Sfx.Level, 0.8f, 1.3f);
            ArenaInput.Haptic(0.8f, 0.15f); ArenaInput.Haptic(0.8f, 0.15f, left: true);
            game.Message("Bênção: " + info.Name + " — " + info.Desc, 2.5f);

            Destroy(gameObject);
        }

        void OnDestroy() => All.Remove(this);

        public static void ClearAll()
        {
            for (int i = All.Count - 1; i >= 0; i--) if (All[i]) Destroy(All[i].gameObject);
            All.Clear();
        }
    }
}
