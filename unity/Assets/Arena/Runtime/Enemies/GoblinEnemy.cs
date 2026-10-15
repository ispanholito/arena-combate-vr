using Arena.Core;
using UnityEngine;

namespace Arena
{
    // Goblin: ataque anunciado com clava (brilha vermelho no wind-up). Pode ser aparado/bloqueado pela arma.
    public class GoblinEnemy : ArenaEnemy
    {
        protected enum AiState { Approach, Windup, Strike, Recover, Slam }

        protected float k = 1f;                 // escala (Rei Goblin = 1,75)
        protected AiState state = AiState.Approach;
        protected float stateT;
        protected Transform model, arm;
        protected Material skinMat, clubMat;
        protected float windup;

        bool hitDone;
        int strafe;
        float nextAttackDelay;

        protected override void BuildModel()
        {
            k = Def.Size;
            windup = Def.Windup;
            bodyBottom = 0.25f * k; bodyTop = 1.05f * k;
            model = new GameObject("Modelo").transform;
            model.SetParent(transform, false);
            model.localScale = Vector3.one * k;
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
            clubMat = ArenaUtil.Lit(new Color(0.42f, 0.27f, 0.15f));
            ArenaUtil.Prim(PrimitiveType.Cylinder, arm, new Vector3(0, 0.45f, 0), new Vector3(0.14f, 0.4f, 0.14f), clubMat);
            SetArm(-35f);
            strafe = Random.value < 0.5f ? -1 : 1;
            nextAttackDelay = Random.Range(0f, 0.8f);
        }

        // Ângulo do braço em graus no eixo X local (negativo = para trás/cima, positivo = golpe para baixo/frente).
        protected void SetArm(float deg) => arm.localRotation = Quaternion.Euler(deg, 0, 0);
        float ArmAngle => Mathf.DeltaAngle(0, arm.localEulerAngles.x);
        protected void LerpArm(float deg, float speed, float dt) => SetArm(Mathf.LerpAngle(ArmAngle, deg, dt * speed));

        public void ClubSegment(out Vector3 a, out Vector3 b)
        {
            a = arm.TransformPoint(new Vector3(0, 0.1f, 0));
            b = arm.TransformPoint(new Vector3(0, 0.88f, 0));
        }

        protected void SetState(AiState s)
        {
            state = s; stateT = 0f; hitDone = false;
            bool glow = s == AiState.Windup || s == AiState.Slam;
            clubMat.color = glow ? new Color(0.9f, 0.15f, 0.05f) : new Color(0.42f, 0.27f, 0.15f);
        }

        protected override void OnHit(DamageEvent ev)
        {
            // Goblin comum às vezes se assusta e cancela o ataque ao levar um golpe.
            if (state == AiState.Windup && !State.Posture.IsBroken && Def.Kind == EnemyKind.Goblin && Random.value < 0.3f) SetState(AiState.Recover);
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
            if (State.Rooted <= 0f) FacePlayer(toPlayer, dt);
            stateT += dt;
            if (ExtraAI(dt, d)) return;
            var dir = d > 1e-3f ? toPlayer / d : transform.forward;

            switch (state)
            {
                case AiState.Approach:
                {
                    var side = new Vector3(-dir.z, 0, dir.x) * strafe * 0.8f;
                    var want = d > 1.25f * k ? dir * Def.MoveSpeed : d < 0.9f * k ? -dir : Vector3.zero;
                    if (d < 2.2f * k) want += side;
                    Move(want, dt);
                    LerpArm(-35f, 5f, dt);
                    if (d < 1.45f * k && stateT > (k > 1f ? 0.6f : 1.0f) + nextAttackDelay && State.CanAct)
                    {
                        nextAttackDelay = Random.Range(0f, 0.8f);
                        SetState(AiState.Windup);
                    }
                    break;
                }
                case AiState.Windup:
                    LerpArm(-150f, 8f, dt);
                    if (stateT > windup) SetState(AiState.Strike);
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
                case AiState.Recover:
                    LerpArm(-35f, 4f, dt);
                    Move(-dir * 0.6f, dt);
                    if (stateT > (k > 1f ? 0.7f : 0.9f)) { SetState(AiState.Approach); strafe = -strafe; }
                    break;
            }
        }

        protected virtual void OnStunned() { }

        void CheckClubContact(float kk, float d)
        {
            ClubSegment(out var a, out var b);
            var w = Player.Weapon;
            if (w != null && w.Held)
            {
                float dist = Geometry.SegmentSegmentDistance(w.BasePoint.ToN(), w.TipPoint.ToN(), a.ToN(), b.ToN());
                // Desktop: sem mão de verdade, segurar o botão direito olhando para o inimigo já intercepta a clava.
                bool desktopGuard = !Player.IsVr && Player.Input.DesktopParry && kk > 0.5f
                                    && Vector3.Dot(Player.HeadForward, (transform.position - Player.FeetPosition).Flat().normalized) > 0.5f;
                if (dist < 0.25f * Mathf.Sqrt(k) || desktopGuard)
                {
                    hitDone = true;
                    ResolveGuard(w, b);
                    return;
                }
            }
            if (kk > 0.97f)
            {
                hitDone = true;
                if ((Player.HeadPosition - b).magnitude < 0.75f * k || d < 1.15f * k) Player.TakeDamage(Def.AttackDamage, transform.position);
            }
        }

        void ResolveGuard(PlayerWeapon w, Vector3 contact)
        {
            var ps = Player.State;
            float speed = Player.Input.DesktopParry ? Mathf.Max(w.TipSpeed, Combat.ParrySpeed + 1f) : w.TipSpeed;
            var r = Guard.Resolve(speed, ps.CounterStance > 0f, ps.Stamina.Current);
            switch (r)
            {
                case GuardResult.Counter:
                    ps.ConsumeCounterStance();
                    Parried(contact);
                    State.Posture.Break();
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
                    ArenaFx.Burst(contact, new Color(1f, 0.82f, 0.56f), 6, 2f);
                    ArenaInput.Haptic(0.7f, 0.07f);
                    Player.TakeDamage(Guard.DamageToPlayer(r, Def.AttackDamage), transform.position, ignoreGuardCooldown: true);
                    break;
                case GuardResult.GuardBroken:
                    ArenaFx.FloatText("GUARDA QUEBRADA", contact, new Color(1f, 0.44f, 0.44f), 1f);
                    Player.TakeDamage(Def.AttackDamage, transform.position, ignoreGuardCooldown: true);
                    break;
            }
        }

        void Parried(Vector3 contact)
        {
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
