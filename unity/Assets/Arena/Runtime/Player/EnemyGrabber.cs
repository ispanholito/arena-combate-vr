using Arena.Core;
using UnityEngine;

namespace Arena
{
    // Agarrar inimigos com a mão VAZIA (pedido do Pablo, 2026-10-17), como no Blade & Sorcery:
    //  - mão livre rastreada: fechar a mão perto do inimigo agarra; abrir solta (com a mão rápida, arremessa);
    //  - controle esquerdo com o escudo nas costas, ou controle direito sem a arma: grip perto do inimigo agarra,
    //    soltar o grip solta/arremessa.
    // Segurado, o inimigo não ataca nem anda (fica "preso" como espetado) e vai junto com a mão. Arremessado, bate em
    // parede/chão/outro inimigo e leva dano pela velocidade (ArenaEnemy.Launch). Orc, chefe e mímico são pesados demais.
    [RequireComponent(typeof(ArenaPlayer))]
    public sealed class EnemyGrabber : MonoBehaviour
    {
        public static EnemyGrabber Instance { get; private set; }

        ArenaPlayer player;
        readonly ArenaEnemy[] held = new ArenaEnemy[2];        // 0 = esquerda, 1 = direita
        readonly Vector3[] offset = new Vector3[2];            // pés do inimigo - ponto da mão
        readonly Vector3[] targetOffset = new Vector3[2];      // offset com a superfície encostada na palma (chega em ~0,1 s)
        readonly bool[] byController = new bool[2];
        readonly Vector3[] ctrlPrev = new Vector3[2], ctrlVel = new Vector3[2];
        float heavyMsgCd;

        // Goblin físico (ragdoll ativo): a mão é um corpo cinemático preso ao osso por uma junta.
        readonly Rigidbody[] handBody = new Rigidbody[2];
        readonly ConfigurableJoint[] ragJoint = new ConfigurableJoint[2];
        readonly RagdollGoblin[] ragOwner = new RagdollGoblin[2];

        void Awake()
        {
            player = GetComponent<ArenaPlayer>(); Instance = this;
            for (int i = 0; i < 2; i++)
            {
                var go = new GameObject(i == 0 ? "MaoFisicaEsq" : "MaoFisicaDir");
                handBody[i] = go.AddComponent<Rigidbody>();
                handBody[i].isKinematic = true;
                handBody[i].interpolation = RigidbodyInterpolation.Interpolate;
            }
        }
        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            foreach (var h in handBody) if (h) Destroy(h.gameObject);
        }

        public bool Holding(int side) => (held[side] && !held[side].Dead) || ragJoint[side];
        public bool HoldingWithTrackedHand(int side) => Holding(side) && !byController[side];

        public static bool CanGrab(EnemyKind k) => GrabRules.CanGrab(k);

        // Procura o inimigo mais perto da palma; agarra se couber na mão.
        public bool TryGrab(int side, Vector3 palm, bool tracked)
        {
            if (Holding(side)) return false;
            // Primeiro o goblin físico: agarra o osso mais perto (na superfície).
            handBody[side].position = palm; handBody[side].transform.position = palm;
            var rj = RagdollGoblin.TryGrab(palm, handBody[side], out var rOwner);
            if (rj)
            {
                ragJoint[side] = rj; ragOwner[side] = rOwner;
                byController[side] = !tracked;
                ctrlPrev[side] = player.trackingSpace.InverseTransformPoint(palm); ctrlVel[side] = Vector3.zero;
                ArenaInput.Haptic(0.6f, 0.08f, left: side == 0);
                return true;
            }
            ArenaEnemy best = null; float bestD = float.MaxValue;
            foreach (var e in ArenaEnemy.All)
            {
                if (!e || e.Dead || e.Impaled) continue;
                e.BodyAxis(out var a, out var b);
                float d = DistToSegment(palm, a, b) - e.Radius;
                if (d < GrabRules.Reach && d < bestD) { bestD = d; best = e; }
            }
            if (!best) return false;
            if (!CanGrab(best.Def.Kind))
            {
                if (heavyMsgCd <= 0f) { ArenaGame.Instance?.Message(best.Def.Name + " é pesado demais para agarrar", 1f); heavyMsgCd = 1.5f; }
                return false;
            }
            held[side] = best;
            // A mão fica POR FORA do inimigo: o inimigo se ajusta para a superfície encostar na palma.
            best.BodyAxis(out var ax, out var bx);
            var axis = bx - ax;
            var onAxis = ax + axis * Mathf.Clamp01(Vector3.Dot(palm - ax, axis) / Mathf.Max(axis.sqrMagnitude, 1e-6f));
            var outDir = palm - onAxis; outDir.y = 0f;
            if (outDir.sqrMagnitude < 1e-4f) outDir = -player.HeadForward;
            outDir.Normalize();
            float surf = best.SurfaceRadiusAt(best.HeightFraction(palm)) + GrabRules.HandThickness;
            var shift = (palm - outDir * surf) - onAxis;
            offset[side] = best.transform.position - palm;
            targetOffset[side] = best.transform.position + shift - palm;
            byController[side] = !tracked;
            ctrlPrev[side] = player.trackingSpace.InverseTransformPoint(palm); ctrlVel[side] = Vector3.zero;
            best.SetImpaled(true);
            ArenaFx.FloatText("AGARROU!", best.Center + Vector3.up * 0.5f, new Color(1f, 0.85f, 0.6f), 0.8f);
            ArenaAudio.Play(Sfx.Impact, best.Center, 0.5f, 1.4f);
            ArenaInput.Haptic(0.6f, 0.08f, left: side == 0);
            return true;
        }

        // Com o controle: o grip agarra o inimigo perto da mão do controle.
        public bool TryGrabWithController(bool right)
        {
            var hand = right ? player.RightHand : player.leftHand;
            return hand && TryGrab(right ? 1 : 0, hand.position, tracked: false);
        }

        public void Hold(int side, Vector3 palm)
        {
            if (ragJoint[side]) { handBody[side].MovePosition(palm); return; }
            var e = held[side];
            if (!e || e.Dead) { Clear(side); return; }
            offset[side] = Vector3.Lerp(offset[side], targetOffset[side], Mathf.Clamp01(Time.deltaTime * 15f));
            var feet = palm + offset[side];
            if (feet.y < 0f) feet.y = 0f;
            e.transform.position = ArenaUtil.ClampToArena(feet);
        }

        public void Release(int side, Vector3 handVelocity)
        {
            if (ragJoint[side] || ragOwner[side])
            {
                if (ragJoint[side])
                {
                    // Arremesso: dá ao osso agarrado a velocidade da mão (o resto do corpo vem junto pelas juntas).
                    var rb = ragJoint[side].GetComponent<Rigidbody>();
                    Destroy(ragJoint[side]);
                    if (rb && handVelocity.magnitude > GrabRules.MinThrowSpeed) rb.linearVelocity = handVelocity * 1.3f;
                }
                if (ragOwner[side]) ragOwner[side].OnGrabReleased();
                ragJoint[side] = null; ragOwner[side] = null;
                return;
            }
            var e = held[side];
            Clear(side);
            if (!e || e.Dead) return;
            e.SetImpaled(false);
            var v = GrabRules.ThrowVelocity(handVelocity.ToN(), e.Def.Kind).ToU();
            e.Launch(v);
            if (v.magnitude > 3f) { ArenaAudio.Play(Sfx.Whoosh, e.Center, 1f, 0.8f); ArenaInput.Haptic(0.7f, 0.08f, left: side == 0); }
        }

        void Clear(int side) { held[side] = null; byController[side] = false; }

        void Update()
        {
            heavyMsgCd -= Time.deltaTime;
            if (!player.IsVr || ArenaGame.Blocked) return;
            var I = player.Input;
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            // Controle esquerdo: grip com o escudo nas costas (e sem segurar a arma com as duas mãos) agarra.
            bool leftEmpty = I.LeftControllerConnected && !(player.Shield && player.Shield.Equipped) && !player.Weapon.TwoHanded;
            if (leftEmpty && I.LeftGripPressed && !Holding(0)) TryGrabWithController(false);
            for (int side = 0; side < 2; side++)
            {
                if (!Holding(side)) { if (held[side]) Clear(side); continue; }
                if (!byController[side]) continue;   // a mão rastreada é atualizada pelos poderes da mão
                var hand = side == 1 ? player.RightHand : player.leftHand;
                // Velocidade da mão relativa ao corpo (andar não arremessa).
                var local = player.trackingSpace.InverseTransformPoint(hand.position);
                ctrlVel[side] = player.trackingSpace.TransformDirection((local - ctrlPrev[side]) / dt);
                ctrlPrev[side] = local;
                bool gripHeld = side == 1 ? I.GripHeld : I.LeftGripHeld;
                if (gripHeld) Hold(side, hand.position);
                else Release(side, ctrlVel[side]);
            }
        }

        static float DistToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            var ab = b - a;
            float t = ab.sqrMagnitude < 1e-6f ? 0f : Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector3.Distance(p, a + ab * t);
        }
    }
}
