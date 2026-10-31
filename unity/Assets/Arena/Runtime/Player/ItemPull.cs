using System.Collections.Generic;
using Arena.Core;
using UnityEngine;

namespace Arena
{
    // Puxar a arma do chão com a mão (Pablo, 2026-10-30: "ninguém quer se abaixar tanto"; como no Blade & Sorcery).
    // Com a mão VAZIA (controle na mão), aponte o controle para uma arma no chão perto dos pés (até 2,5 m): ela acende
    // (anel embaixo + nome). Aperte o grip e PUXE a mão em direção ao corpo: a arma voa até a mão e fica nela.
    // Soltar o grip antes de puxar cancela.
    [RequireComponent(typeof(ArenaPlayer))]
    public sealed class ItemPull : MonoBehaviour
    {
        public const float MaxFeetDistance = 2.5f, AimDegrees = 14f, PullSpeed = 1.1f, PullDistance = 0.12f, LockSeconds = 1.5f, FlySeconds = 0.25f;
        public static ItemPull Instance { get; private set; }

        ArenaPlayer player;
        readonly WeaponPickup[] aimed = new WeaponPickup[2];
        readonly WeaponPickup[] locked = new WeaponPickup[2];
        readonly Vector3[] lockHandPos = new Vector3[2], prevHand = new Vector3[2];
        readonly float[] lockT = new float[2];
        readonly Transform[] marker = new Transform[2];
        Material markMat;

        // Arma voando para a mão.
        sealed class Flight { public WeaponPickup Item; public int Hand; public float T; public Vector3 From; }
        readonly List<Flight> flights = new List<Flight>();

        void Awake() { player = GetComponent<ArenaPlayer>(); Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; foreach (var m in marker) if (m) Destroy(m.gameObject); }

        Transform Hand(int side) => side == 0 ? player.leftHand : player.rightHand;

        bool HandEmpty(int side)
        {
            var I = player.Input;
            if (side == 0) return I.LeftControllerConnected && player.State.LeftItem == null && !player.Weapon.TwoHanded;
            return !I.RightHandFree && (player.State.Loadout[0] == null || player.Weapon.Stowed);
        }

        // Grip apertado: se a mão está mirando uma arma, trava nela (o puxão traz). Chamado pelo SkillCaster (direita) e
        // pelos Holsters (esquerda).
        public bool TryStart(int side)
        {
            if (!player.IsVr || !HandEmpty(side) || !aimed[side]) return false;
            locked[side] = aimed[side];
            lockHandPos[side] = Hand(side).position;
            lockT[side] = LockSeconds;
            ArenaInput.Haptic(0.35f, 0.05f, left: side == 0);
            return true;
        }

        void Update()
        {
            if (player.State == null) return;
            float dt = Mathf.Max(1e-4f, Time.deltaTime);
            UpdateFlights(dt);   // (também fora do VR: o voo termina mesmo se o headset sair)
            if (!player.IsVr) { HideAll(); return; }
            if (ArenaGame.Blocked || player.TreeOpen) { HideAll(); return; }
            for (int side = 0; side < 2; side++)
            {
                var h = Hand(side);
                var vel = (h.position - prevHand[side]) / dt;
                prevHand[side] = h.position;
                if (!HandEmpty(side)) { aimed[side] = null; locked[side] = null; Show(side, null); continue; }

                if (locked[side])
                {
                    bool grip = side == 0 ? player.Input.LeftGripHeld : player.Input.GripHeld;
                    lockT[side] -= dt;
                    if (!grip || lockT[side] <= 0f || !locked[side]) { locked[side] = null; }
                    else
                    {
                        // Puxão: mão vindo rápido em direção ao corpo, ou 12 cm para trás desde o aperto.
                        var toBody = (player.Head.position - Vector3.up * 0.4f - h.position).normalized;
                        bool pulled = Vector3.Dot(vel, toBody) > PullSpeed || Vector3.Dot(h.position - lockHandPos[side], toBody) > PullDistance;
                        if (pulled) { Launch(locked[side], side); locked[side] = null; Show(side, null); continue; }
                        Show(side, locked[side]);
                        continue;
                    }
                }
                aimed[side] = FindAimed(h);
                Show(side, aimed[side]);
            }
        }

        // Arma no chão perto dos pés mais alinhada com a direção para onde o controle aponta.
        WeaponPickup FindAimed(Transform h)
        {
            WeaponPickup best = null; float bestA = AimDegrees;
            var dir = h.forward;
            foreach (var p in WeaponPickup.All)
            {
                if (!p || p.OnRack) continue;
                if ((p.Center - player.FeetPosition).Flat().magnitude > MaxFeetDistance) continue;
                var to = p.Center - h.position;
                if (to.magnitude < 0.25f) continue;   // já está na mão: pega direto
                float a = Vector3.Angle(dir, to);
                if (a < bestA) { bestA = a; best = p; }
            }
            return best;
        }

        void Launch(WeaponPickup item, int side)
        {
            if (item.Rack == RackKind.Loja) return;   // compra só pegando direto
            var body = item.GetComponent<Rigidbody>();
            if (body) body.isKinematic = true;
            foreach (var c in item.GetComponents<Collider>()) c.enabled = false;
            WeaponPickup.All.Remove(item);   // ninguém mais pega/mira no caminho
            flights.Add(new Flight { Item = item, Hand = side, From = item.transform.position });
            ArenaAudio.Play(Sfx.Whoosh, item.transform.position, 0.6f, 1.4f);
            ArenaInput.Haptic(0.5f, 0.06f, left: side == 0);
        }

        void UpdateFlights(float dt)
        {
            for (int i = flights.Count - 1; i >= 0; i--)
            {
                var f = flights[i];
                if (!f.Item) { flights.RemoveAt(i); continue; }
                f.T += dt / FlySeconds;
                var target = Hand(f.Hand).position;
                var p = Vector3.Lerp(f.From, target, Mathf.SmoothStep(0f, 1f, f.T)) + Vector3.up * Mathf.Sin(Mathf.Clamp01(f.T) * Mathf.PI) * 0.25f;
                f.Item.transform.position = p;
                f.Item.transform.Rotate(Vector3.up, 540f * dt, Space.World);
                if (f.T < 1f) continue;
                flights.RemoveAt(i);
                var item = f.Item.Item;
                Destroy(f.Item.gameObject);
                player.State.LostItems.Remove(item);   // era uma arma esquecida: achou
                WeaponPickup.GiveToPlayer(player, item, target, f.Hand == 0 ? 1 : 0);
            }
        }

        // Anel em volta da arma mirada + o nome dela.
        void Show(int side, WeaponPickup p)
        {
            if (!marker[side])
            {
                markMat = markMat ? markMat : ArenaUtil.Unlit(new Color(0.45f, 0.9f, 1f, 0.45f));
                marker[side] = new GameObject("MiraDaArma").transform;
                var ring = ArenaUtil.Prim(PrimitiveType.Cylinder, marker[side], Vector3.zero, new Vector3(0.7f, 0.004f, 0.7f), markMat).transform;
                foreach (var rc in ring.GetComponents<Collider>()) Destroy(rc);
                var t = ArenaFx.MakeText(marker[side], "", 0.005f, new Color(0.7f, 0.95f, 1f));
                t.transform.localPosition = new Vector3(0f, 0.45f, 0f);
                t.name = "Nome";
            }
            var m = marker[side];
            if (!p) { m.gameObject.SetActive(false); return; }
            m.gameObject.SetActive(true);
            var c = p.Center;
            m.position = new Vector3(c.x, player.FeetPosition.y + 0.01f, c.z);
            m.rotation = Quaternion.identity;
            float pulse = 1f + 0.08f * Mathf.Sin(Time.time * 8f);
            m.GetChild(0).localScale = new Vector3(0.7f * pulse, 0.004f, 0.7f * pulse);
            var label = m.GetComponentInChildren<TextMesh>();
            label.text = p.Item.DisplayName + (locked[side] == p ? "\n<size=40>puxe!</size>" : "\n<size=40>grip + puxe</size>");
            var cam = player.Head;
            label.transform.rotation = Quaternion.LookRotation(label.transform.position - cam.position);
        }

        void HideAll() { foreach (var m in marker) if (m) m.gameObject.SetActive(false); }
    }
}
