using System.Collections.Generic;
using System.Text;
using Arena.Core;
using UnityEngine;

namespace Arena
{
    public enum RackKind { Nenhuma, Permanente, Temporaria, Loja }

    // Arma caída no chão (loot ou a arma trocada), como no Blade & Sorcery (Pablo, 2026-10-30: "não arma de Fortnite
    // girando e brilhando"): é um OBJETO FÍSICO — cai, quica, rola, a sua arma empurra. Sem feixe e sem girar; a
    // raridade aparece só na gema. Nome e atributos só com a mão perto (VR) ou chegando perto (desktop).
    // Pegar: leve a mão até a arma (VR: grip, gatilho perto ou pinça da mão livre) ou E (desktop); a da mão vai para a
    // 2ª vaga vazia ou fica no chão.
    public sealed class WeaponPickup : MonoBehaviour
    {
        public static readonly List<WeaponPickup> All = new List<WeaponPickup>();
        // VR: a mão tem que chegar na arma (distância até a linha cabo→ponta), como pegar de verdade. Modo sentado
        // também conta os pés (sentado não dá para alcançar o chão).
        public const float VrHandRange = 0.5f, RackHandRange = 0.8f, FeetRange = 1.2f, DesktopRange = 2.2f;
        public const float LabelHandRange = 0.7f, LabelDesktopRange = 2.5f;

        public ItemInstance Item { get; private set; }
        public bool Owned;   // arma do próprio jogador (trocada): se ficar para trás, volta no santuário
        // Pendurada no suporte de armas do santuário: em pé, parada; o nome só aparece bem perto.
        public bool OnRack => Rack != RackKind.Nenhuma;
        public RackKind Rack { get; private set; }
        public Vector3 RackPos { get; private set; }
        public Quaternion RackRot { get; private set; }

        public WeaponPickup PlaceOnRack(Vector3 pos, Quaternion facing, RackKind kind = RackKind.Temporaria)
        {
            Rack = kind; RackPos = pos; RackRot = facing;
            body.isKinematic = true;
            foreach (var c in GetComponents<Collider>()) c.enabled = false;
            transform.SetPositionAndRotation(pos, facing);
            model.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            model.localPosition = new Vector3(0, Item.Base.Length * 0.5f + 0.1f, 0);
            return this;
        }
        Transform model;
        TextMesh label;
        Rigidbody body;
        static PhysicsMaterial floorMat;

        // Centro e pontas da arma no mundo (o modelo fica centrado no meio do comprimento).
        Vector3 SegA => model.TransformPoint(new Vector3(0, 0, -Item.Base.Length * 0.5f));
        Vector3 SegB => model.TransformPoint(new Vector3(0, 0, Item.Base.Length * 0.5f));
        public Vector3 Center => OnRack ? RackPos + Vector3.up * (Item.Base.Length * 0.5f + 0.1f) : model.position;
        float DistanceTo(Vector3 p)
        {
            if (OnRack) return Vector3.Distance(p, Center);
            Vector3 a = SegA, ab = SegB - a;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
            return Vector3.Distance(p, a + ab * t);
        }

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
            var c = WeaponModels.RarityColor(item.Rarity);
            model = new GameObject("Modelo").transform;
            model.SetParent(transform, false);
            var inner = new GameObject("Arma").transform;
            inner.SetParent(model, false);
            WeaponModels.Build(inner, item.Base, item.Rarity);
            // Centrada no meio do comprimento.
            inner.localPosition = new Vector3(0, 0, -item.Base.Length * 0.5f);
            foreach (var col in model.GetComponentsInChildren<Collider>()) Destroy(col);

            // Corpo físico: uma CAIXA do tamanho do modelo (achatada como a lâmina) e massa real. Era uma cápsula, que
            // rola sem parar sobre o próprio eixo — as armas "saíam correndo" pelo chão (Pablo, 2026-10-30).
            var bounds = new Bounds(transform.position, Vector3.zero);
            bool any = false;
            foreach (var r in model.GetComponentsInChildren<Renderer>()) { if (any) bounds.Encapsulate(r.bounds); else { bounds = r.bounds; any = true; } }
            if (!floorMat) floorMat = new PhysicsMaterial("ArmaNoChao") { dynamicFriction = 0.8f, staticFriction = 1f, bounciness = 0.05f,
                                                                          frictionCombine = PhysicsMaterialCombine.Maximum, bounceCombine = PhysicsMaterialCombine.Minimum };
            var box = gameObject.AddComponent<BoxCollider>();
            box.center = bounds.center - transform.position;
            box.size = new Vector3(Mathf.Max(0.04f, bounds.size.x), Mathf.Max(0.03f, bounds.size.y), Mathf.Max(0.1f, bounds.size.z));
            box.material = floorMat;
            transform.rotation = Quaternion.Euler(Random.Range(-10f, 10f), Random.Range(0f, 360f), Random.Range(-10f, 10f));
            body = gameObject.AddComponent<Rigidbody>();
            body.mass = WeaponPhysics.Mass(item.Base);
            body.linearDamping = 0.2f; body.angularDamping = 2f;
            body.maxDepenetrationVelocity = 1f;   // nascendo encostada em algo, não sai disparada
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearVelocity = velocity;
            body.angularVelocity = velocity.sqrMagnitude > 0.01f ? Random.insideUnitSphere * 3f : Vector3.zero;

            // O rótulo não fica preso à arma (ela gira): segue por cima, sempre em pé.
            label = ArenaFx.MakeText(null, Describe(item), 0.006f, c);
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
            // Caiu por um buraco no chão (não devia): volta para cima, dentro da área.
            if (!OnRack && transform.position.y < -1f)
            {
                body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero;
                body.position = ArenaUtil.ClampToArena(new Vector3(transform.position.x, 0.4f, transform.position.z));
            }
            UpdateLabel();
        }

        void UpdateLabel()
        {
            var cam = Camera.main;
            var player = ArenaGame.Instance ? ArenaGame.Instance.Player : null;
            if (!cam || !player) { label.gameObject.SetActive(false); return; }
            bool near = player.IsVr
                ? Mathf.Min(DistanceTo(player.RightHand.position), DistanceTo(player.leftHand.position)) < (OnRack ? RackHandRange : LabelHandRange)
                : (cam.transform.position - Center).Flat().magnitude < (OnRack ? 1.3f : LabelDesktopRange);
            label.gameObject.SetActive(near);
            if (!near) return;
            label.transform.position = OnRack ? RackPos + Vector3.up * (Item.Base.Length + 0.35f) : Center + Vector3.up * 0.45f;
            label.transform.rotation = Quaternion.LookRotation(label.transform.position - cam.transform.position);
        }

        void OnDestroy()
        {
            All.Remove(this);
            if (label) Destroy(label.gameObject);
        }

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
        // handOnly (gatilho perto da arma): só conta a mão, bem perto (não pega sem querer no meio da luta).
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
                    float hand = Mathf.Min(p.DistanceTo(player.RightHand.position), p.DistanceTo(player.leftHand.position));
                    bool feetOk = SaveSystem.Options.seated && !handOnly;
                    float feet = feetOk ? (player.FeetPosition - p.Center).Flat().magnitude : float.MaxValue;
                    float range = handOnly ? TriggerHandRange : p.OnRack ? RackHandRange : VrHandRange;
                    if (hand > range && feet > FeetRange) continue;
                    d = Mathf.Min(hand, feet);
                }
                else
                {
                    var to = (p.Center - player.FeetPosition).Flat();
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
            var pos = player.Weapon.Held ? best.Center : player.Weapon.Visual.position;
            All.Remove(best);
            Destroy(best.gameObject);
            if (best.Rack == RackKind.Permanente) player.State.Stash.Remove(item);
            else if (best.Rack != RackKind.Loja) player.State.LostItems.Remove(item);   // era uma arma esquecida: achou
            var dropped = GiveToPlayer(player, item, pos);
            if (dropped && best.OnRack && best.Rack != RackKind.Loja)   // troca na estante: a sua fica pendurada no mesmo gancho
            {
                dropped.PlaceOnRack(best.RackPos, best.RackRot, best.Rack);
                if (best.Rack == RackKind.Permanente) player.State.AddToStash(dropped.Item); else player.State.AddLostItem(dropped.Item);
            }
            return true;
        }

        // Põe a arma na mão do jogador; a antiga vai para a 2ª vaga vazia ou cai em dropAt (devolvida). Usado ao pegar do
        // chão/estante/loja e ao arrancar uma arma cravada.
        public static WeaponPickup GiveToPlayer(ArenaPlayer player, ItemInstance item, Vector3 dropAt)
        {
            var old = player.State.TakeItem(item, out bool toSecond);
            player.EquipActive();   // a nova sempre vai para a mão
            WeaponPickup dropped = null;
            if (old != null)
            {
                dropped = Spawn(old, dropAt + Vector3.up * 0.05f);
                dropped.Owned = true;
            }
            ArenaAudio.Play2D(Sfx.Pickup, 0.9f);
            ArenaGame.Instance?.RequestSave();
            ArenaInput.Haptic(0.5f, 0.08f);
            var proficient = Proficiency.IsProficient(player.State.Class.Id, item.Base.Kind);
            ArenaGame.Instance?.Message(item.DisplayName
                + (toSecond ? "\n<size=48>(a antiga foi para a 2ª arma)</size>" : "")
                + (proficient ? "" : "\n<size=48>(fora da classe: -15% dano)</size>"), 1.6f);
            return dropped;
        }
    }
}
