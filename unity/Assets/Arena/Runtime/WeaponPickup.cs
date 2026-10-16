using System.Collections.Generic;
using System.Text;
using Arena.Core;
using UnityEngine;

namespace Arena
{
    // Arma caída no chão (loot ou a arma trocada). Gira devagar com um feixe de luz na cor da raridade; perto dela
    // aparece o nome e os atributos. Grip (VR, com a mão perto) ou E (desktop) pega: troca pela arma da mão,
    // e a antiga fica no chão.
    public sealed class WeaponPickup : MonoBehaviour
    {
        public static readonly List<WeaponPickup> All = new List<WeaponPickup>();
        public const float VrHandRange = 0.8f, FeetRange = 1.2f, DesktopRange = 2.2f, LabelRange = 4f;

        public ItemInstance Item { get; private set; }
        Transform model, beam;
        TextMesh label;
        float t, settle;
        Vector3 vel;

        public static WeaponPickup Spawn(ItemInstance item, Vector3 pos, Vector3 velocity = default)
        {
            var go = new GameObject("Loot " + item.DisplayName);
            go.transform.position = pos;
            var p = go.AddComponent<WeaponPickup>();
            p.Init(item, velocity);
            return p;
        }

        void Init(ItemInstance item, Vector3 velocity)
        {
            Item = item;
            vel = velocity;
            var c = WeaponModels.RarityColor(item.Rarity);
            model = new GameObject("Modelo").transform;
            model.SetParent(transform, false);
            var inner = new GameObject("Arma").transform;
            inner.SetParent(model, false);
            WeaponModels.Build(inner, item.Base, item.Rarity);
            // Deitada, centrada no meio do comprimento.
            inner.localRotation = Quaternion.Euler(0, 0, 0);
            inner.localPosition = new Vector3(0, 0, -item.Base.Length * 0.5f);
            model.localRotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
            foreach (var col in model.GetComponentsInChildren<Collider>()) Destroy(col);

            float h = 0.8f + 0.5f * (int)item.Rarity;
            beam = ArenaUtil.Prim(PrimitiveType.Cylinder, transform, new Vector3(0, h * 0.5f, 0), new Vector3(0.05f, h * 0.5f, 0.05f),
                                  ArenaUtil.Unlit(new Color(c.r, c.g, c.b, 0.35f))).transform;
            label = ArenaFx.MakeText(transform, Describe(item), 0.006f, c);
            label.transform.localPosition = new Vector3(0, 0.75f, 0);
            label.gameObject.SetActive(false);
            All.Add(this);
        }

        public static string Describe(ItemInstance item)
        {
            var sb = new StringBuilder();
            sb.Append(item.DisplayName).Append("  <size=48>(").Append(ItemInstance.RarityName(item.Rarity)).Append(")</size>");
            sb.Append("\n<size=48><color=#dddddd>dano x").Append(item.DamageMultiplier.ToString("0.00")).Append("</color></size>");
            foreach (var a in item.Affixes) sb.Append("\n<size=48>").Append(a.Label).Append("</size>");
            return sb.ToString();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            t += dt;
            // Cai até o chão (loot arremessado do inimigo).
            if (transform.position.y > 0.02f || vel.y > 0f)
            {
                vel += Physics.gravity * dt;
                var p = transform.position + vel * dt;
                if (p.y <= 0f) { p.y = 0f; vel = Vector3.zero; settle = 0.2f; }
                transform.position = ArenaUtil.ClampToArena(p);
            }
            settle -= dt;
            model.localPosition = new Vector3(0, 0.25f + Mathf.Sin(t * 2f) * 0.05f, 0);
            model.Rotate(Vector3.up, 40f * dt, Space.World);
            var cam = Camera.main;
            if (cam)
            {
                bool near = (cam.transform.position - transform.position).Flat().magnitude < LabelRange;
                label.gameObject.SetActive(near);
                if (near) label.transform.rotation = Quaternion.LookRotation(label.transform.position - cam.transform.position);
            }
        }

        void OnDestroy() => All.Remove(this);

        public static void ClearAll()
        {
            for (int i = All.Count - 1; i >= 0; i--) if (All[i]) Destroy(All[i].gameObject);
            All.Clear();
        }

        // Pega a arma mais próxima ao alcance. Retorna true se pegou (o grip não arremessa a arma nesse quadro).
        public static bool TryPickup(ArenaPlayer player)
        {
            WeaponPickup best = null;
            float bestD = float.MaxValue;
            foreach (var p in All)
            {
                if (!p) continue;
                float d;
                if (player.IsVr)
                {
                    float hand = Vector3.Distance(player.RightHand.position, p.transform.position + Vector3.up * 0.25f);
                    float feet = (player.FeetPosition - p.transform.position).Flat().magnitude;
                    if (hand > VrHandRange && feet > FeetRange) continue;
                    d = Mathf.Min(hand, feet);
                }
                else
                {
                    var to = (p.transform.position - player.FeetPosition).Flat();
                    d = to.magnitude;
                    if (d > DesktopRange) continue;
                }
                if (d < bestD) { bestD = d; best = p; }
            }
            if (!best || !player.Weapon.Held) return false;
            var item = best.Item;
            var pos = best.transform.position;
            All.Remove(best);
            Destroy(best.gameObject);
            var old = player.State.EquipToActive(item);
            player.EquipActive();
            if (old != null) Spawn(old, pos + Vector3.up * 0.05f);
            ArenaAudio.Play2D(Sfx.Pickup, 0.9f);
            ArenaGame.Instance?.RequestSave();
            ArenaInput.Haptic(0.5f, 0.08f);
            var proficient = Proficiency.IsProficient(player.State.Class.Id, item.Base.Kind);
            ArenaGame.Instance?.Message(item.DisplayName + (proficient ? "" : "\n<size=48>(fora da classe: -15% dano)</size>"), 1.6f);
            return true;
        }
    }
}
