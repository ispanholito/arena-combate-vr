using Arena.Core;
using UnityEngine;

namespace Arena
{
    // Coldres e mão esquerda (2026-10-30, estilo Blade & Sorcery):
    //   - 2 coldres nos OMBROS (qualquer arma, inclusive grandes: escudo, lança, martelo...) e 2 na CINTURA (pequenas e
    //     médias). Mão (com controle) perto do coldre + grip = puxa a arma; soltar o grip com a arma perto de um coldre
    //     livre onde ela cabe = guarda.
    //   - A mão ESQUERDA segura qualquer arma ou o escudo, enquanto o grip estiver apertado (depois do 1º aperto).
    //     Soltou longe de um coldre = a arma cai. Largou o controle esquerdo (para usar os poderes da mão) = guarda num
    //     coldre livre (o escudo vai para as costas, como antes), ou cai se não couber.
    //   - Desktop: Q troca a arma da mão direita pela próxima dos coldres; F põe/tira o escudo da mão esquerda.
    [RequireComponent(typeof(ArenaPlayer))]
    public sealed class Holsters : MonoBehaviour
    {
        public const float DrawRange = 0.2f, StoreRange = 0.26f;
        public static Holsters Instance { get; private set; }

        ArenaPlayer player;
        readonly Transform[] visuals = new Transform[4];
        readonly ItemInstance[] shown = new ItemInstance[4];
        readonly Vector3[] anchor = new Vector3[4];
        readonly Quaternion[] pose = new Quaternion[4];
        bool leftArmed, leftWasGrip, leftWasController;
        ItemInstance leftArmedFor;
        Vector3 leftPrevPos, leftVel;

        void Awake() { player = GetComponent<ArenaPlayer>(); Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; foreach (var v in visuals) if (v) Destroy(v.gameObject); foreach (var h in hint) if (h) Destroy(h.gameObject); }

        PlayerState S => player.State;

        // ---------- Posição dos coldres (presos ao corpo: altura da cabeça e direção para onde o corpo olha) ----------
        void PlaceSlots()
        {
            var head = player.Head;
            var f = player.HeadForward; f.y = 0f;
            if (f.sqrMagnitude < 1e-4f) f = Vector3.forward;
            f.Normalize();
            var r = Vector3.Cross(Vector3.up, f);
            float feetY = player.FeetPosition.y;
            float h = Mathf.Clamp(head.position.y - feetY, 1.1f, 2f);
            var hip = new Vector3(head.position.x, feetY + h * 0.55f, head.position.z);
            for (int s = 0; s < 4; s++)
            {
                float side = s == PlayerState.OmbroE || s == PlayerState.CinturaE ? -1f : 1f;
                var item = S.Holsters[s];
                if (PlayerState.IsShoulder(s))
                {
                    // Cabo por cima/atrás do ombro; a arma desce cruzando as costas.
                    anchor[s] = head.position - Vector3.up * 0.12f + r * (0.15f * side) - f * 0.14f;
                    var dir = (-Vector3.up * 0.85f - r * (0.45f * side) - f * 0.08f).normalized;
                    pose[s] = item != null && item.Base.IsShield ? Quaternion.LookRotation(-Vector3.up, r) : Quaternion.LookRotation(dir, -f);
                }
                else
                {
                    // Cintura: cabo na altura da mão caída, lâmina para baixo e um pouco para trás (bainha).
                    anchor[s] = hip + r * (0.21f * side) + f * 0.03f;
                    pose[s] = Quaternion.LookRotation((-Vector3.up * 0.9f - f * 0.35f).normalized, r);
                }
            }
        }

        public Vector3 SlotPosition(int s) => anchor[s];

        // Coldre mais perto da mão (dentro do alcance); onlyWith: com arma (puxar) / livre e que caiba (guardar).
        int NearestSlot(Vector3 hand, float range, ItemInstance fitting = null, bool needItem = false)
        {
            int best = -1; float bestD = range;
            for (int s = 0; s < 4; s++)
            {
                if (needItem && S.Holsters[s] == null) continue;
                if (fitting != null && (S.Holsters[s] != null || !PlayerState.Fits(s, fitting))) continue;
                float d = Vector3.Distance(hand, anchor[s]);
                if (d < bestD) { bestD = d; best = s; }
            }
            return best;
        }

        // ---------- Mão direita (chamado pelo SkillCaster) ----------
        // Grip apertado com a mão direita vazia (ou com a arma caída longe) perto de um coldre com arma: puxa.
        public bool TryDrawRight()
        {
            if (!player.IsVr || player.Weapon.Held || player.Weapon.Recalling) return false;
            int s = NearestSlot(player.RightHand.position, DrawRange, needItem: true);
            if (s < 0) return false;
            // A arma da mão direita estava caída/cravada longe: vira uma arma no chão (sua) e a mão pega a do coldre.
            if (S.Loadout[0] != null && !player.Weapon.Stowed)
            {
                var drop = WeaponPickup.Spawn(S.Loadout[0], player.Weapon.Visual.position + Vector3.up * 0.1f);
                drop.Owned = true;
            }
            S.Loadout[0] = S.TakeFromHolster(s);
            player.EquipActive();
            Feedback(s, false, "Sacou ");
            return true;
        }

        // Soltou o grip direito com a arma perto de um coldre livre onde ela cabe: guarda (em vez de largar).
        public bool TryStoreRight()
        {
            if (!player.IsVr || S.Loadout[0] == null) return false;
            int s = NearestSlot(player.Weapon.Visual ? player.RightHand.position : player.RightHand.position, StoreRange, fitting: S.Loadout[0]);
            if (s < 0) return false;
            S.StoreInHolster(s, S.Loadout[0]);
            S.Loadout[0] = null;
            player.EquipActive();
            Feedback(s, true, "Guardou ");
            return true;
        }

        void Feedback(int slot, bool store, string verb)
        {
            ArenaAudio.Play(store ? Sfx.Click : Sfx.Whoosh, anchor[slot], 0.6f, store ? 0.8f : 1.4f);
            bool left = slot == PlayerState.OmbroE || slot == PlayerState.CinturaE;
            ArenaInput.Haptic(0.45f, 0.06f, left: left);
            ArenaGame.Instance?.RequestSave();
        }

        void Update()
        {
            if (S == null) return;
            PlaceSlots();
            UpdateVisuals();
            if (ArenaGame.Blocked || player.TreeOpen) return;
            if (player.IsVr) { UpdateLeftHand(); UpdateHints(); }
            else UpdateDesktop();
        }

        // ---------- Mão esquerda (VR) ----------
        void UpdateLeftHand()
        {
            var I = player.Input;
            var lh = player.leftHand;
            float dt = Mathf.Max(1e-4f, Time.deltaTime);
            leftVel = (lh.position - leftPrevPos) / dt;
            leftPrevPos = lh.position;
            bool controller = I.LeftControllerConnected;
            bool grip = controller && I.LeftGripHeld;
            bool pressed = grip && !leftWasGrip;
            bool released = !grip && leftWasGrip;
            leftWasGrip = grip;

            var item = S.LeftItem;
            if (item != leftArmedFor) { leftArmedFor = item; leftArmed = grip; }   // item novo: só depende do grip depois do 1º aperto

            // Largou o controle esquerdo (vai usar a mão livre): guarda o que estava na mão num coldre livre.
            if (!controller)
            {
                if (leftWasController && item != null) StoreOrDropLeft(forceStore: true);
                leftWasController = false;
                return;
            }
            leftWasController = true;

            if (item != null)
            {
                if (grip) leftArmed = true;
                if (released && leftArmed) StoreOrDropLeft(forceStore: false);
                return;
            }
            if (!pressed) return;
            // Mão esquerda vazia apertou o grip: segurar a arma da direita com as duas mãos tem prioridade (TwoHandGrip).
            var w = player.Weapon;
            if (w.Held && Vector3.Distance(lh.position, w.SecondGripWorld) < 0.3f) return;
            int s = NearestSlot(lh.position, DrawRange, needItem: true);
            if (s >= 0)
            {
                S.Loadout[1] = S.TakeFromHolster(s);
                leftArmedFor = S.LeftItem; leftArmed = true;
                player.EquipActive();
                Feedback(s, false, "Sacou ");
                return;
            }
            if (WeaponPickup.TryPickup(player, hand: 1)) { leftArmedFor = S.LeftItem; leftArmed = true; return; }
            if (ItemPull.Instance) ItemPull.Instance.TryStart(0);   // mirando uma arma no chão: grip + puxar traz
        }

        // Soltou o que estava na mão esquerda: perto de um coldre livre = guarda; senão cai (arremessa se a mão vinha rápida).
        // forceStore (largou o controle): procura qualquer coldre livre onde caiba.
        void StoreOrDropLeft(bool forceStore)
        {
            var item = S.LeftItem;
            if (item == null) return;
            int s = forceStore ? S.FreeHolsterFor(item) : NearestSlot(player.leftHand.position, StoreRange, fitting: item);
            if (s >= 0 && S.StoreInHolster(s, item))
            {
                S.Loadout[1] = null;
                player.EquipActive();
                Feedback(s, true, "Guardou ");
                return;
            }
            S.Loadout[1] = null;
            var at = player.WeaponL && player.WeaponL.Held ? player.WeaponL.Visual.position : player.leftHand.position;
            var v = leftVel.magnitude > WeaponPhysics.ThrowMinHandSpeed ? leftVel * 1.4f : leftVel * 0.5f;
            var drop = WeaponPickup.Spawn(item, at, v);
            drop.Owned = true;
            player.EquipActive();
            ArenaInput.Haptic(0.3f, 0.04f, left: true);
            ArenaGame.Instance?.RequestSave();
        }

        // ---------- Desktop ----------
        void UpdateDesktop()
        {
            var I = player.Input;
            // F: escudo na mão esquerda ↔ coldre.
            if (I.ShieldToggle)
            {
                if (S.LeftItem != null)
                {
                    int s = S.FreeHolsterFor(S.LeftItem);
                    if (s >= 0) { S.StoreInHolster(s, S.LeftItem); S.Loadout[1] = null; player.EquipActive(); ArenaGame.Instance?.Message("Guardou " + S.Holsters[s].DisplayName, 0.9f); }
                    else ArenaGame.Instance?.Message("Sem coldre livre", 0.9f);
                }
                else
                {
                    for (int s = 0; s < 4; s++)
                        if (S.Holsters[s] != null && S.Holsters[s].Base.IsShield)
                        {
                            S.Loadout[1] = S.TakeFromHolster(s); player.EquipActive();
                            ArenaGame.Instance?.Message("Escudo na mão", 0.9f);
                            return;
                        }
                    ArenaGame.Instance?.Message("Sem escudo nos coldres", 0.9f);
                }
            }
        }

        // Q (desktop) / clique do analógico esquerdo: guarda a arma da mão direita e saca a próxima dos coldres.
        public void CycleRight()
        {
            int start = -1;
            for (int i = 0; i < 4; i++)
                if (S.Holsters[i] != null && !S.Holsters[i].Base.IsShield) { start = i; break; }
            if (start < 0) { ArenaGame.Instance?.Message("Nenhuma arma nos coldres", 0.9f); return; }
            if (!player.Weapon.Held && S.Loadout[0] != null && !player.Weapon.Stowed) return;   // a da mão está caída: pegue antes
            var next = S.TakeFromHolster(start);
            var cur = S.Loadout[0];
            S.Loadout[0] = next;
            if (cur != null)
            {
                // Pequena prefere a cintura livre; senão vai para o coldre de onde saiu a nova (se couber).
                int f = S.FreeHolsterFor(cur);
                if (f >= 0 && !PlayerState.IsShoulder(f)) S.Holsters[f] = cur;
                else if (PlayerState.Fits(start, cur)) S.Holsters[start] = cur;
                else if (f >= 0) S.Holsters[f] = cur;
                else { var d = WeaponPickup.Spawn(cur, player.FeetPosition + player.HeadForward * 0.6f + Vector3.up * 0.5f); d.Owned = true; }
            }
            player.EquipActive();
            ArenaGame.Instance?.Message(next.DisplayName, 0.9f);
        }

        // ---------- Aviso do coldre (Pablo, 2026-10-30, como no B&S) ----------
        // Segurando uma arma perto de um coldre livre onde ela cabe: aparece "CINTURA DIREITA" / "OMBRO ESQUERDO"... e um
        // anel no lugar (soltar o grip guarda). Mão vazia perto de um coldre com arma: aparece o nome da arma (grip saca).
        static readonly string[] SlotNames = { "OMBRO ESQUERDO", "OMBRO DIREITO", "CINTURA ESQUERDA", "CINTURA DIREITA" };
        readonly Transform[] hint = new Transform[2];
        readonly int[] hintSlot = { -1, -1 };

        void UpdateHints()
        {
            for (int side = 0; side < 2; side++)
            {
                var hand = side == 0 ? player.leftHand : player.rightHand;
                ItemInstance held = side == 0 ? S.LeftItem : (player.Weapon.Held ? S.Loadout[0] : null);
                bool controller = side == 0 ? player.Input.LeftControllerConnected : !player.Input.RightHandFree;
                int slot = -1; string text = null;
                if (controller && held != null)
                {
                    slot = NearestSlot(hand.position, StoreRange, fitting: held);
                    if (slot >= 0) text = SlotNames[slot] + "\n<size=40>solte para guardar</size>";
                }
                else if (controller && (side == 0 ? S.LeftItem == null : S.Loadout[0] == null || player.Weapon.Stowed))
                {
                    slot = NearestSlot(hand.position, DrawRange, needItem: true);
                    if (slot >= 0) text = S.Holsters[slot].DisplayName + "\n<size=40>" + SlotNames[slot].ToLower() + " · grip</size>";
                }
                if (slot != hintSlot[side] && slot >= 0) ArenaInput.Haptic(0.2f, 0.03f, left: side == 0);   // entrou no alcance
                hintSlot[side] = slot;
                ShowHint(side, slot, text);
            }
        }

        void ShowHint(int side, int slot, string text)
        {
            if (!hint[side])
            {
                hint[side] = new GameObject("AvisoColdre").transform;
                var ring = ArenaUtil.Prim(PrimitiveType.Sphere, hint[side], Vector3.zero, Vector3.one * 0.12f, ArenaUtil.Unlit(new Color(1f, 0.85f, 0.35f, 0.35f))).transform;
                foreach (var c in ring.GetComponents<Collider>()) Destroy(c);
                var t = ArenaFx.MakeText(hint[side], "", 0.0035f, new Color(1f, 0.9f, 0.55f));
                t.transform.localPosition = new Vector3(0f, 0.11f, 0f);
            }
            if (slot < 0) { hint[side].gameObject.SetActive(false); return; }
            hint[side].gameObject.SetActive(true);
            hint[side].position = anchor[slot];
            hint[side].GetChild(0).localScale = Vector3.one * (0.12f + 0.015f * Mathf.Sin(Time.time * 10f));
            var label = hint[side].GetComponentInChildren<TextMesh>();
            label.text = text;
            // O texto fica entre o coldre e os olhos (os da cintura/ombro saem do campo de visão): puxa para a frente.
            var head = player.Head;
            label.transform.position = Vector3.Lerp(anchor[slot], head.position, 0.35f) + player.HeadForward.Flat().normalized * 0.15f;
            label.transform.rotation = Quaternion.LookRotation(label.transform.position - head.position);
        }

        // ---------- Armas nos coldres (visíveis no VR) ----------
        void UpdateVisuals()
        {
            bool show = player.IsVr && !ArenaGame.InMenu;
            for (int s = 0; s < 4; s++)
            {
                var item = S.Holsters[s];
                if (item != shown[s])
                {
                    if (visuals[s]) Destroy(visuals[s].gameObject);
                    visuals[s] = null;
                    shown[s] = item;
                    if (item != null)
                    {
                        visuals[s] = new GameObject("Coldre " + s).transform;
                        var inner = new GameObject("Arma").transform;
                        inner.SetParent(visuals[s], false);
                        WeaponModels.Build(inner, item.Base, item.Rarity);
                        if (item.Base.IsShield) inner.localPosition = new Vector3(0f, 0f, -0.05f);
                        foreach (var c in visuals[s].GetComponentsInChildren<Collider>()) Destroy(c);
                    }
                }
                if (!visuals[s]) continue;
                visuals[s].gameObject.SetActive(show);
                var back = item.Base.IsShield ? -player.HeadForward.Flat().normalized * 0.12f : Vector3.zero;
                visuals[s].SetPositionAndRotation(anchor[s] + back, pose[s]);
            }
        }
    }
}
