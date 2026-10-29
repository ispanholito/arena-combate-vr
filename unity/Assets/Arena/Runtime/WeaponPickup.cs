using System.Collections.Generic;
using System.Text;
using Arena.Core;
using UnityEngine;

namespace Arena
{
    public enum RackKind { Nenhuma, Permanente, Temporaria, Loja }

    // Arma caída no chão (loot ou a arma trocada). Gira devagar com um feixe de luz na cor da raridade; perto dela
    // aparece o nome e os atributos. Grip (VR, com a mão perto) ou E (desktop) pega: troca pela arma da mão,
    // e a antiga fica no chão.
    public sealed class WeaponPickup : MonoBehaviour
    {
        public static readonly List<WeaponPickup> All = new List<WeaponPickup>();
        public const float VrHandRange = 0.8f, FeetRange = 1.2f, DesktopRange = 2.2f, LabelRange = 4f;

        public ItemInstance Item { get; private set; }
        public bool Owned;   // arma do próprio jogador (trocada): se ficar para trás, volta no santuário
        // Pendurada no suporte de armas do santuário: em pé, parada, sem feixe; o nome só aparece bem perto.
        public bool OnRack => Rack != RackKind.Nenhuma;
        public RackKind Rack { get; private set; }
        public Vector3 RackPos { get; private set; }
        public Quaternion RackRot { get; private set; }

        public WeaponPickup PlaceOnRack(Vector3 pos, Quaternion facing, RackKind kind = RackKind.Temporaria)
        {
            Rack = kind; RackPos = pos; RackRot = facing;
            vel = Vector3.zero;
            transform.SetPositionAndRotation(pos, facing);
            model.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            model.localPosition = new Vector3(0, Item.Base.Length * 0.5f + 0.1f, 0);
            if (beam) beam.gameObject.SetActive(false);
            label.transform.localPosition = new Vector3(0, Item.Base.Length + 0.35f, 0);
            return this;
        }
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
            sb.Append("\n<size=48><color=#ffe680>").Append(item.Ability.Name).Append(item.Rarity == Rarity.Lendario ? " desperta: " + WeaponAbilities.LegendaryEffect(item.Base.Kind) : "").Append("</color></size>");
            if (item.Passive != WeaponPassive.Nenhuma)
                sb.Append("\n<size=48><color=#c8a0ff>").Append(WeaponAbilities.PassiveName(item.Passive)).Append("</color></size>");
            foreach (var a in item.Affixes) sb.Append("\n<size=48>").Append(a.Label).Append("</size>");
            return sb.ToString();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            t += dt;
            if (OnRack) { UpdateLabel(1.3f); return; }
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
            UpdateLabel(LabelRange);
        }

        void UpdateLabel(float range)
        {
            var cam = Camera.main;
            if (!cam) return;
            bool near = (cam.transform.position - transform.position).Flat().magnitude < range;
            label.gameObject.SetActive(near);
            if (near) label.transform.rotation = Quaternion.LookRotation(label.transform.position - cam.transform.position);
        }

        void OnDestroy() => All.Remove(this);

        // À venda na banca do mercador: o rótulo mostra o preço.
        public WeaponPickup SetPriceTag(int price)
        {
            label.text = Describe(Item) + "\n<color=#ffd34d>" + price + " ouro</color> <size=40>(grip / E compra)</size>";
            return this;
        }

        // Some com o loot do chão. As armas do próprio jogador vão para os "esquecidos" (voltam no santuário).
        public static void ClearAll(PlayerState keeper = null)
        {
            for (int i = All.Count - 1; i >= 0; i--)
            {
                if (!All[i]) continue;
                // As das estantes já estão nas listas (permanente/temporária); as do chão vão para a temporária.
                if (keeper != null && All[i].Owned && !All[i].OnRack) keeper.AddLostItem(All[i].Item);
                Destroy(All[i].gameObject);
            }
            All.Clear();
        }

        // Pega a arma mais próxima ao alcance. Retorna true se pegou (o grip não arremessa a arma nesse quadro).
        // handOnly (gatilho perto da arma): só conta a mão, bem perto (não pega sem querer pelos pés no meio da luta).
        public const float TriggerHandRange = 0.45f;
        public static bool TryPickup(ArenaPlayer player, bool handOnly = false)
        {
            WeaponPickup best = null;
            float bestD = float.MaxValue;
            foreach (var p in All)
            {
                if (!p) continue;
                float d;
                if (player.IsVr)
                {
                    var center = p.transform.position + Vector3.up * (p.OnRack ? p.Item.Base.Length * 0.5f + 0.1f : 0.25f);
                    float hand = Vector3.Distance(player.RightHand.position, center);
                    float feet = (player.FeetPosition - p.transform.position).Flat().magnitude;
                    if (handOnly) { if (hand > TriggerHandRange) continue; d = hand; }
                    else { if (hand > VrHandRange && feet > FeetRange) continue; d = Mathf.Min(hand, feet); }
                }
                else
                {
                    var to = (p.transform.position - player.FeetPosition).Flat();
                    d = to.magnitude;
                    if (d > DesktopRange) continue;
                }
                if (d < bestD) { bestD = d; best = p; }
            }
            if (!best) return false;
            var item = best.Item;
            if (best.Rack == RackKind.Loja)
            {
                var g = ArenaGame.Instance;
                var shop = g && g.Dungeon && g.Dungeon.Active ? g.Dungeon.MerchantShop : g && g.Sanctuary ? g.Sanctuary.Shop : null;
                if (shop == null || !shop.TryBuy(item, player.State))
                {
                    ArenaGame.Instance?.Message("Custa " + Merchant.ShopPrice(item) + " de ouro (você tem " + player.State.Gold + ")", 1.4f);
                    ArenaAudio.Play2D(Sfx.Deny, 0.6f);
                    return true;
                }
                ArenaAudio.Play2D(Sfx.Level, 0.6f, 1.3f);
            }
            // A arma que sai cai onde estava a nova (ou onde a sua caiu, se você estava de mão vazia).
            var pos = player.Weapon.Held ? best.transform.position : player.Weapon.Visual.position;
            All.Remove(best);
            Destroy(best.gameObject);
            if (best.Rack == RackKind.Permanente) player.State.Stash.Remove(item);
            else if (best.Rack != RackKind.Loja) player.State.LostItems.Remove(item);   // era uma arma esquecida: achou
            var old = player.State.TakeItem(item, out bool toSecond);
            player.EquipActive();   // a nova sempre vai para a mão
            if (old != null)
            {
                var dropped = Spawn(old, pos + Vector3.up * 0.05f);
                dropped.Owned = true;
                if (best.OnRack && best.Rack != RackKind.Loja)   // troca na estante: a sua fica pendurada no mesmo gancho
                {
                    dropped.PlaceOnRack(best.RackPos, best.RackRot, best.Rack);
                    if (best.Rack == RackKind.Permanente) player.State.AddToStash(old); else player.State.AddLostItem(old);
                }
            }
            ArenaAudio.Play2D(Sfx.Pickup, 0.9f);
            ArenaGame.Instance?.RequestSave();
            ArenaInput.Haptic(0.5f, 0.08f);
            var proficient = Proficiency.IsProficient(player.State.Class.Id, item.Base.Kind);
            ArenaGame.Instance?.Message(item.DisplayName
                + (toSecond ? "\n<size=48>(a antiga foi para a 2ª arma)</size>" : "")
                + (proficient ? "" : "\n<size=48>(fora da classe: -15% dano)</size>"), 1.6f);
            return true;
        }
    }
}
