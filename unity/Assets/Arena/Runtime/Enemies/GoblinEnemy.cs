using Arena.Core;
using UnityEngine;

namespace Arena
{
    // Goblin: ataque anunciado com clava (brilha vermelho no wind-up). Pode ser aparado/bloqueado pela arma.
    public class GoblinEnemy : ArenaEnemy
    {
        protected enum AiState { Approach, Windup, Strike, Recover, Slam, Retreat }

        protected float k = 1f;                 // escala (Rei Goblin = 1,75)
        protected AiState state = AiState.Approach;
        protected float stateT;
        protected Transform model, arm;
        protected Material skinMat, clubMat;
        protected float windup;

        bool hitDone;
        Vector3 clubPrevA, clubPrevB;
        bool clubPrevValid;
        int strafe;
        bool retreated;
        float nextAttackDelay;

        protected Color clubColor = new Color(0.42f, 0.27f, 0.15f);

        protected override void BuildModel()
        {
            k = Def.Size;
            windup = Def.Windup;
            bodyBottom = 0.25f * k; bodyTop = 1.05f * k;
            model = new GameObject("Modelo").transform;
            model.SetParent(transform, false);
            model.localScale = Vector3.one * k;
            BuildVisual();
            SetArm(-35f);
            strafe = Random.value < 0.5f ? -1 : 1;
            nextAttackDelay = Random.Range(0f, 0.8f);
        }

        // Corpo do goblin (o esqueleto sobrescreve). Precisa criar `arm` (com a arma ao longo de +Y) e `clubMat`.
        protected virtual void BuildVisual()
        {
            bool king = Def.Kind == EnemyKind.ReiGoblin;
            skinMat = ArenaUtil.Lit(king ? new Color(0.3f, 0.48f, 0.17f) : new Color(0.37f, 0.54f, 0.23f));
            var cloth = ArenaUtil.Lit(king ? new Color(0.35f, 0.12f, 0.12f) : new Color(0.35f, 0.25f, 0.16f));
            ArenaUtil.Prim(PrimitiveType.Cylinder, model, new Vector3(0, 0.55f, 0), new Vector3(0.5f, 0.35f, 0.5f), cloth);
            var head = ArenaUtil.Prim(PrimitiveType.Sphere, model, new Vector3(0, 1.02f, 0), Vector3.one * 0.4f, skinMat).transform;
            var eye = ArenaUtil.Lit(new Color(1f, 0.88f, 0.29f), new Color(0.6f, 0.5f, 0.1f));
            foreach (int s in new[] { -1, 1 })
            {
                var ear = ArenaUtil.Prim(PrimitiveType.Capsule, model, new Vector3(s * 0.24f, 1.08f, 0), new Vector3(0.07f, 0.14f, 0.07f), skinMat).transform;
                ear.localRotation = Quaternion.Euler(0, 0, -s * 65f);
                ArenaUtil.Prim(PrimitiveType.Sphere, model, new Vector3(s * 0.08f, 1.06f, 0.17f), Vector3.one * 0.07f, eye);
            }
            if (king)
                ArenaUtil.Prim(PrimitiveType.Cylinder, model, new Vector3(0, 1.2f, 0), new Vector3(0.3f, 0.05f, 0.3f),
                               ArenaUtil.Lit(new Color(0.88f, 0.69f, 0.19f), new Color(0.23f, 0.16f, 0f)));
            arm = new GameObject("Braco").transform;
            arm.SetParent(model, false);
            arm.localPosition = new Vector3(0.28f, 0.85f, 0f);
            clubMat = ArenaUtil.Lit(clubColor);
            ArenaUtil.Prim(PrimitiveType.Cylinder, arm, new Vector3(0, 0.45f, 0), new Vector3(0.14f, 0.4f, 0.14f), clubMat);
        }

        // Ganchos para variações (esqueleto sem braço não ataca, sem cabeça gira devagar e demora mais).
        protected virtual bool CanAttackNow => true;
        protected virtual float TurnSpeed => 10f;
        protected virtual float WindupTime => windup;

        // Ângulo do braço em graus no eixo X local (negativo = para trás/cima, positivo = golpe para baixo/frente).
        // Inclinação de -18° em Y: a clava sai do ombro direito e desce no centro (no jogador), não ao lado.
        protected void SetArm(float deg) { armAngle = deg; arm.localRotation = Quaternion.Euler(deg, -18f, 0); }
        float armAngle;
        float ArmAngle => armAngle;
        protected void LerpArm(float deg, float speed, float dt) => SetArm(Mathf.Lerp(ArmAngle, deg, Mathf.Min(1f, dt * speed)));

        public void ClubSegment(out Vector3 a, out Vector3 b)
        {
            a = arm.TransformPoint(new Vector3(0, 0.1f, 0));
            b = arm.TransformPoint(new Vector3(0, 0.88f, 0));
        }

        protected void SetState(AiState s)
        {
            state = s; stateT = 0f; hitDone = false; clubPrevValid = false;
            bool glow = s == AiState.Windup || s == AiState.Slam;
            if (s == AiState.Windup || s == AiState.Slam) ArenaAudio.Play(Sfx.Windup, transform.position + Vector3.up, 0.7f, 1f / k);
            clubMat.color = glow ? new Color(0.9f, 0.15f, 0.05f) : clubColor;
        }

        protected override void OnHit(DamageEvent ev)
        {
            // Goblin comum às vezes se assusta e cancela o ataque ao levar um golpe.
            if (state == AiState.Windup && !State.Posture.IsBroken && Def.Kind == EnemyKind.Goblin && Random.value < 0.3f) SetState(AiState.Recover);
            // Muito ferido: às vezes recua um pouco (uma vez) antes de voltar.
            if (!ev.Killed && state != AiState.Strike && GoblinTactics.ShouldRetreat(Def.Kind, State.HpFraction, retreated, Random.value))
            {
                retreated = true;
                SetState(AiState.Retreat);
                ArenaFx.FloatText("RECUOU", Center + Vector3.up * 0.7f, new Color(0.85f, 0.85f, 0.6f), 0.9f);
            }
        }

        // Override para o chefe; retorna true se assumiu o controle neste frame.
        protected virtual bool ExtraAI(float dt, float d) => false;

        protected override void Think(float dt, Vector3 toPlayer, float d)
        {
            if (State.Stun > 0f)
            {
                LerpArm(50f, 6f, dt);
                model.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(Time.time * 11f) * 5f);
                if (state != AiState.Recover) SetState(AiState.Recover);
                OnStunned();
                return;
            }
            model.localRotation = Quaternion.identity;
            if (State.Rooted <= 0f) FacePlayer(toPlayer, dt, TurnSpeed);
            stateT += dt;
            if (ExtraAI(dt, d)) return;
            var dir = d > 1e-3f ? toPlayer / d : transform.forward;

            switch (state)
            {
                case AiState.Approach:
                {
                    var side = new Vector3(-dir.z, 0, dir.x) * strafe * 0.8f;
                    var want = d > 1.25f * k ? dir * Def.MoveSpeed : d < 0.9f * k ? -dir : Vector3.zero;
                    // Em grupo: cada um vai para a sua vaga num arco à frente do jogador (cercam pelos lados).
                    float slot = GroupSlot();
                    if (Mathf.Abs(slot) > 1f && d < GoblinTactics.EngageDistance * k)
                    {
                        var spot = Player.FeetPosition + Quaternion.Euler(0, slot, 0) * Player.HeadForward * 1.3f * k;
                        var toSpot = (spot - transform.position).Flat();
                        want = toSpot.magnitude > 0.35f ? toSpot.normalized * Def.MoveSpeed * 0.9f : Vector3.zero;
                        if (d < 0.9f * k) want -= dir;
                    }
                    else if (d < 2.2f * k) want += side;
                    Move(want, dt);
                    LerpArm(-35f, 5f, dt);
                    if (d < 1.45f * k && stateT > (k > 1f ? 0.6f : 1.0f) + nextAttackDelay && State.CanAct && CanAttackNow)
                    {
                        nextAttackDelay = Random.Range(0f, 0.8f);
                        SetState(AiState.Windup);
                    }
                    break;
                }
                case AiState.Windup:
                    LerpArm(-150f, 8f, dt);
                    if (stateT > WindupTime) SetState(AiState.Strike);
                    break;
                case AiState.Strike:
                {
                    float kk = Mathf.Min(1f, stateT / 0.28f);
                    SetArm(Mathf.Lerp(-150f, 75f, kk * kk));
                    Move(dir * 1.2f, dt);
                    if (!hitDone) CheckClubContact(kk, d);
                    if (stateT > 0.4f) SetState(AiState.Recover);
                    break;
                }
                case AiState.Retreat:
                    LerpArm(-20f, 4f, dt);
                    Move(-dir * Def.MoveSpeed * 1.1f, dt);
                    if (stateT > GoblinTactics.RetreatSeconds) SetState(AiState.Approach);
                    break;
                case AiState.Recover:
                    LerpArm(-35f, 4f, dt);
                    Move(-dir * 0.6f, dt);
                    if (stateT > (k > 1f ? 0.7f : 0.9f)) { SetState(AiState.Approach); strafe = -strafe; }
                    break;
            }
        }

        protected virtual void OnStunned() { }

        // Vaga deste goblin no grupo (goblins e esqueletos vivos, sem o chefe nem arqueiros), em ordem estável.
        float GroupSlot()
        {
            if (this is GoblinKing) return 0f;   // o chefe vem de frente
            int count = 0, index = 0, me = GetInstanceID();
            foreach (var e in All)
            {
                if (!(e is GoblinEnemy) || e is GoblinKing || e is ArcherEnemy || e.Dead) continue;
                count++;
                if (e.GetInstanceID() < me) index++;
            }
            return GoblinTactics.SlotAngle(index, count);
        }

        // Testa o caminho inteiro da clava entre o frame anterior e o atual (golpe rápido não atravessa a defesa).
        // Ordem: escudo, espada, corpo do jogador.
        void CheckClubContact(float kk, float d)
        {
            ClubSegment(out var a, out var b);
            if (!clubPrevValid) { clubPrevA = a; clubPrevB = b; clubPrevValid = true; }
            float tol = 0.06f * Mathf.Sqrt(k);
            var shield = Player.Shield;
            var w = Player.Weapon;
            bool wind = kk > 0.35f;   // o começo do arco é atrás da cabeça do goblin

            if (shield && (shield.Intercepts(clubPrevA, clubPrevB, a, b, tol) || (wind && shield.DesktopCovers(transform.position))))
            {
                hitDone = true;
                ResolveGuard(Guard.ResolveShield(shield.GuardSpeed, Player.State.CounterStance > 0f, Player.State.Stamina.Current), b);
                return;
            }
            if (w != null && w.Held)
            {
                bool touched = Geometry.SweptSegmentsHit(w.BasePrev.ToN(), w.TipPrev.ToN(), w.BasePoint.ToN(), w.TipPoint.ToN(),
                                                         clubPrevA.ToN(), clubPrevB.ToN(), a.ToN(), b.ToN(), 0.1f + tol);
                // Desktop sem escudo (Elementalista): botão direito olhando para o inimigo intercepta com a arma.
                bool desktopGuard = !Player.IsVr && Player.Input.DesktopParry && wind && (shield == null || !shield.Equipped)
                                    && Vector3.Dot(Player.HeadForward, (transform.position - Player.FeetPosition).Flat().normalized) > 0.5f;
                if (touched || desktopGuard)
                {
                    hitDone = true;
                    float speed = desktopGuard ? Combat.ParrySpeed + 1f : Mathf.Max(w.TipSpeed, (w.TipPoint - w.TipPrev).magnitude / Mathf.Max(Time.deltaTime, 1e-4f));
                    ResolveGuard(Guard.Resolve(speed, Player.State.CounterStance > 0f, Player.State.Stamina.Current), b);
                    return;
                }
            }
            // Corpo do jogador: da cintura à cabeça. Só toma dano se a clava realmente chegar nele.
            var head = Player.HeadPosition;
            var waist = new Vector3(head.x, Player.FeetPosition.y + 0.6f, head.z);
            if (wind && Geometry.SweptSegmentsHit(waist.ToN(), head.ToN(), waist.ToN(), head.ToN(),
                                                  clubPrevA.ToN(), clubPrevB.ToN(), a.ToN(), b.ToN(), 0.22f + tol))
            {
                hitDone = true;
                Player.TakeDamage(Def.AttackDamage, transform.position);
            }
            clubPrevA = a; clubPrevB = b;
            if (kk >= 1f) hitDone = true;
        }

        void ResolveGuard(GuardResult r, Vector3 contact)
        {
            var ps = Player.State;
            switch (r)
            {
                case GuardResult.Counter:
                    ps.ConsumeCounterStance();
                    Parried(contact);
                    ArenaFx.FloatText("CONTRA-ATAQUE!", Center + Vector3.up * 0.9f * k, new Color(1f, 0.83f, 0.42f), 1.4f);
                    ApplyDamage(Combat.CounterDamage * (ps.Has(SkillIds.FioAfiado) ? Combat.SharpEdgeBonus : 1f), 0.7f,
                                transform.position - Player.FeetPosition, 4f, new Color(1f, 0.83f, 0.42f));
                    ArenaInput.Haptic(1f, 0.2f);
                    Player.Vignette(0.5f);
                    break;
                case GuardResult.Parry:
                    Parried(contact);
                    break;
                case GuardResult.Block:
                    ps.Stamina.TrySpend(Guard.BlockStaminaCost, 1f);
                    State.AddPosture(Guard.BlockPosture);
                    ArenaFx.FloatText("BLOQUEIO", contact, new Color(0.8f, 0.8f, 0.8f), 0.9f);
                    ArenaAudio.Play(Sfx.Block, contact);
                    ArenaFx.Burst(contact, new Color(1f, 0.82f, 0.56f), 6, 2f);
                    ArenaInput.Haptic(0.7f, 0.07f);
                    Player.TakeDamage(Guard.DamageToPlayer(r, Def.AttackDamage), transform.position, ignoreGuardCooldown: true);
                    break;
                case GuardResult.ShieldBlock:
                    ps.Stamina.TrySpend(Guard.ShieldBlockStaminaCost, 1f);
                    State.AddPosture(Guard.ShieldBlockPosture);
                    ArenaFx.FloatText("BLOQUEIO", contact, new Color(0.85f, 0.85f, 0.85f), 0.9f);
                    ArenaAudio.Play(Sfx.Block, contact);
                    ArenaFx.Burst(contact, new Color(1f, 0.82f, 0.56f), 8, 2f);
                    ArenaInput.Haptic(0.6f, 0.07f, left: true);
                    break;
                case GuardResult.GuardBroken:
                    ArenaFx.FloatText("GUARDA QUEBRADA", contact, new Color(1f, 0.44f, 0.44f), 1f);
                    Player.TakeDamage(Def.AttackDamage, transform.position, ignoreGuardCooldown: true);
                    break;
            }
        }

        void Parried(Vector3 contact)
        {
            ArenaAudio.Play(Sfx.Parry, contact);
            ArenaFx.FloatText("APARO!", contact, new Color(0.62f, 0.88f, 1f), 1.1f);
            ArenaFx.Burst(contact, new Color(1f, 0.91f, 0.63f), 16, 4f);
            ArenaInput.Haptic(1f, 0.09f);
            State.AddPosture(Guard.ParryPosture);
            State.ApplyStun(k > 1f ? 0.7f : 1.1f);
            knock += (transform.position - Player.FeetPosition).Flat().normalized * 2f;
            Player.State.Stamina.Restore(Guard.ParryStaminaRefund);
            SetState(AiState.Recover);
        }
    }
}
