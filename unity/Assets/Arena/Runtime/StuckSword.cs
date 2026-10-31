using System.Collections.Generic;
using Arena.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Arena
{
    // Espada cravada num cadáver (roteiro da demo, cena 1). Segure o CABO e PUXE ao longo da lâmina: ela vai saindo aos
    // poucos (ExtractRules), vibrando e rangendo, sem voltar para dentro; quando a ponta sai, a espada vai para a sua
    // mão. Pegar: grip do controle esquerdo, mão livre fechando, ou o controle direito (grip com a mão vazia ou
    // gatilho perto do cabo). Desktop: segure E perto.
    public sealed class StuckSword : MonoBehaviour
    {
        public static readonly List<StuckSword> All = new List<StuckSword>();

        public ItemInstance Item { get; private set; }
        public bool Freed { get; private set; }
        public float Extracted => extracted;

        Transform sword;
        Vector3 entry, axisOut;        // ponto onde a lâmina entra no corpo e direção de saída (para o cabo)
        float bladeLen, extracted, maxAlong, ripT, hapticT;
        int holder = -1;               // mão segurando: 0 = esquerda, 1 = direita, 2 = desktop
        bool holderTrigger;            // direita segurando pelo gatilho (senão pelo grip)
        readonly bool[] wasClosed = new bool[2];

        // Corpo deitado em `pos`, virado para `facing`; a espada entra no peito inclinada.
        public static StuckSword Spawn(ItemInstance item, Vector3 pos, Vector3 facing, Transform parent = null)
        {
            var go = new GameObject("Espada cravada");
            if (parent) go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(facing.Flat().sqrMagnitude > 1e-4f ? facing.Flat().normalized : Vector3.forward));
            var s = go.AddComponent<StuckSword>();
            s.Init(item);
            return s;
        }

        void Init(ItemInstance item)
        {
            Item = item;
            BuildCorpse();
            // Entra no peito, inclinada 25° da vertical (o cabo pende para a cabeça).
            entry = transform.TransformPoint(new Vector3(0f, 0.2f, 0.05f));
            axisOut = (transform.rotation * Quaternion.Euler(25f, 0f, 0f) * Vector3.up).normalized;
            bladeLen = item.Base.Length;
            sword = new GameObject("Espada").transform;
            sword.SetParent(transform, true);
            var inner = new GameObject("Arma").transform;
            inner.SetParent(sword, false);
            WeaponModels.Build(inner, item.Base, item.Rarity);
            foreach (var c in sword.GetComponentsInChildren<Collider>()) Destroy(c);
            Place(0f);
            All.Add(this);
        }

        // Aventureiro caído de costas (formas simples até a arte).
        void BuildCorpse()
        {
            var cloth = ArenaUtil.Lit(new Color(0.22f, 0.32f, 0.55f));
            var leather = ArenaUtil.Lit(new Color(0.4f, 0.26f, 0.14f));
            var skin = ArenaUtil.Lit(new Color(0.78f, 0.62f, 0.5f));
            var metal = ArenaUtil.Lit(new Color(0.55f, 0.57f, 0.6f));
            var t = transform;
            ArenaUtil.Prim(PrimitiveType.Capsule, t, new Vector3(0f, 0.14f, 0f), new Vector3(0.42f, 0.36f, 0.24f), cloth).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            ArenaUtil.Prim(PrimitiveType.Sphere, t, new Vector3(0f, 0.12f, 0.5f), Vector3.one * 0.22f, skin);
            ArenaUtil.Prim(PrimitiveType.Sphere, t, new Vector3(0f, 0.16f, 0.52f), new Vector3(0.25f, 0.16f, 0.25f), metal);   // elmo amassado
            ArenaUtil.Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.15f, -0.14f), new Vector3(0.36f, 0.06f, 0.08f), leather);   // cinto
            foreach (float x in new[] { -0.11f, 0.11f })
            {
                ArenaUtil.Prim(PrimitiveType.Capsule, t, new Vector3(x, 0.08f, -0.5f), new Vector3(0.13f, 0.3f, 0.13f), leather).transform.localRotation = Quaternion.Euler(90f, 0f, x * 40f);
                ArenaUtil.Prim(PrimitiveType.Capsule, t, new Vector3(x * 2.9f, 0.08f, 0.12f), new Vector3(0.1f, 0.26f, 0.1f), cloth).transform.localRotation = Quaternion.Euler(90f, x * 300f, 0f);
            }
            // Mancha escura no chão.
            ArenaUtil.Prim(PrimitiveType.Cylinder, t, new Vector3(0.05f, 0.005f, 0.05f), new Vector3(0.9f, 0.004f, 0.7f), ArenaUtil.Lit(new Color(0.18f, 0.05f, 0.06f)));
        }

        // A espada fica ao longo do eixo: a ponta a (Depth - extraído) dentro do corpo.
        void Place(float shake)
        {
            float inside = ExtractRules.Depth - extracted;
            var tip = entry - axisOut * inside;
            var rot = Quaternion.LookRotation(-axisOut, transform.forward);
            sword.SetPositionAndRotation(tip + axisOut * bladeLen + Random.insideUnitSphere * shake, rot);
        }

        Vector3 Hilt => sword.position;

        void OnDestroy() => All.Remove(this);

        // Controle direito: grip com a mão vazia ou gatilho perto do cabo (chamado pelo SkillCaster).
        public static bool TryGrabRight(ArenaPlayer p, bool trigger)
        {
            if (!p.IsVr) return false;
            var s = Nearest(p.RightHand.position);
            if (!s) return false;
            s.Grab(p, 1, trigger);
            return true;
        }

        // Cabo da espada que esta mão (0 = esquerda, 1 = direita) está segurando: a mão é desenhada grudada nele.
        public static bool TryGetHeldHilt(int side, out Vector3 hilt)
        {
            foreach (var s in All)
                if (s && !s.Freed && s.holder == side) { hilt = s.Hilt; return true; }
            hilt = default; return false;
        }

        static StuckSword Nearest(Vector3 hand)
        {
            StuckSword best = null; float bestD = ExtractRules.GrabRange;
            foreach (var s in All)
            {
                if (!s || s.Freed || s.holder >= 0) continue;
                float d = Vector3.Distance(hand, s.Hilt);
                if (d < bestD) { bestD = d; best = s; }
            }
            return best;
        }

        void Grab(ArenaPlayer p, int side, bool trigger)
        {
            holder = side; holderTrigger = trigger;
            maxAlong = side < 2 ? Vector3.Dot(HandPos(p, side) - entry, axisOut) : 0f;
            ArenaInput.Haptic(0.4f, 0.05f, left: side == 0);
            ArenaAudio.Play(Sfx.Click, Hilt, 0.5f, 0.8f);
        }

        static Vector3 HandPos(ArenaPlayer p, int side) => side == 0 ? p.leftHand.position : p.rightHand.position;

        void Update()
        {
            var game = ArenaGame.Instance;
            var p = game ? game.Player : null;
            if (Freed || !p || p.State == null || ArenaGame.Blocked) { holder = -1; return; }
            if (holder < 0) TryGrabOther(p);
            if (holder < 0) { Place(0f); return; }

            float before = extracted;
            if (holder == 2)
            {
                bool eHeld = Keyboard.current != null && Keyboard.current.eKey.isPressed;
                if (!eHeld || (p.FeetPosition - transform.position).Flat().magnitude > 2.2f) { holder = -1; return; }
                extracted = ExtractRules.Step(extracted, ExtractRules.DesktopSpeed * Time.deltaTime / ExtractRules.Yield(extracted), Time.deltaTime);
            }
            else
            {
                var hand = HandPos(p, holder);
                if (!Holding(p) || Vector3.Distance(hand, Hilt) > ExtractRules.SlipDistance) { Release(); return; }
                float along = Vector3.Dot(hand - entry, axisOut);
                if (along > maxAlong)
                {
                    extracted = ExtractRules.Step(extracted, along - maxAlong, Time.deltaTime);
                    maxAlong = along;
                }
                if (holder < 2 && (holder == 0 ? p.Input.LeftHandFree : p.Input.RightHandFree)) HandLock.Hold(holder, "cravada", 0.3f);
            }

            float moved = extracted - before;
            Place(moved > 0f ? 0.004f : 0f);
            if (moved > 0f)
            {
                hapticT -= Time.deltaTime;
                if (hapticT <= 0f) { hapticT = 0.05f; ArenaInput.Haptic(0.25f + 0.5f * extracted / ExtractRules.Depth, 0.05f, left: holder == 0); }
                ripT += moved;
                if (ripT > 0.03f) { ripT = 0f; ArenaAudio.Play(Sfx.Rip, entry, 0.35f, 0.7f + extracted * 1.5f); }
            }
            if (ExtractRules.IsFree(extracted)) Free(p);
        }

        // Mão esquerda (controle: grip; livre: fechar) e direita livre (fechar).
        void TryGrabOther(ArenaPlayer p)
        {
            if (!p.IsVr)
            {
                if (Keyboard.current != null && Keyboard.current.eKey.isPressed && (p.FeetPosition - transform.position).Flat().magnitude <= 2.2f) Grab(p, 2, false);
                return;
            }
            for (int side = 0; side < 2; side++)
            {
                bool free = side == 0 ? p.Input.LeftHandFree : p.Input.RightHandFree;
                if (side == 1 && !free) continue;   // controle direito: SkillCaster chama TryGrabRight
                bool closed = free ? HandPoses.Closed(p, side) : p.Input.LeftGripHeld;
                bool started = closed && !wasClosed[side];
                wasClosed[side] = closed;
                if (started && Vector3.Distance(HandPos(p, side), Hilt) < ExtractRules.GrabRange) { Grab(p, side, false); return; }
            }
        }

        bool Holding(ArenaPlayer p)
        {
            bool free = holder == 0 ? p.Input.LeftHandFree : p.Input.RightHandFree;
            if (free) return HandPoses.Closed(p, holder);
            if (holder == 0) return p.Input.LeftGripHeld;
            return holderTrigger ? p.Input.TriggerHeld : p.Input.GripHeld;
        }

        void Release()
        {
            ArenaInput.Haptic(0.2f, 0.03f, left: holder == 0);
            holder = -1;
        }

        void Free(ArenaPlayer p)
        {
            Freed = true;
            holder = -1;
            ArenaAudio.Play(Sfx.Whoosh, Hilt, 0.8f, 1.2f);
            ArenaAudio.Play(Sfx.Rip, entry, 0.6f, 1.3f);
            ArenaFx.Burst(entry, new Color(0.55f, 0.08f, 0.1f), 8, 1.5f, 0.6f);
            ArenaInput.Haptic(0.9f, 0.12f);
            ArenaInput.Haptic(0.9f, 0.12f, left: true);
            Destroy(sword.gameObject);
            WeaponPickup.GiveToPlayer(p, Item, entry + Vector3.up * 0.3f);
        }
    }
}
