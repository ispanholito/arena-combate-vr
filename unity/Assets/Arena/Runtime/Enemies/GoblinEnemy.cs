using System.Collections.Generic;
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

        // Movimento com aceleração, chegada em curva, esquiva e tranco ao apanhar (Core/EnemyMotion.cs).
        Vector3 moveVel, dodgeDir, flinchDir;
        float approachAngle = float.NaN, weaveSeed, dodgeT, dodgeCd, dodgeReactT = -1f, flinchT, flinchDur, flinchTilt, flinchStumble, stepT;
        bool Heavy => this is IBoss || Def.Kind == EnemyKind.Orc;

        void Steer(Vector3 want, float dt)
        {
            float rate = want.sqrMagnitude > moveVel.sqrMagnitude ? EnemyMotion.Accel : EnemyMotion.Decel;
            moveVel = Vector3.MoveTowards(moveVel, want, rate * dt);
            Move(moveVel, dt);
            // Passo: balança o corpo conforme anda.
            float sp = moveVel.magnitude;
            if (sp > 0.2f && flinchT <= 0f)
            {
                stepT += dt * (4f + sp * 2.5f);
                var local = transform.InverseTransformDirection(moveVel) / Mathf.Max(sp, 0.01f);
                model.localRotation = Quaternion.Euler(local.z * Mathf.Min(sp, 3f) * 3f, 0f, Mathf.Sin(stepT) * 3f - local.x * 4f);
            }
        }

        // Tranco: começa no OnHit; enquanto dura, joga o corpo para trás e recua uns passos.
        void StartFlinch(DamageEvent ev)
        {
            if (Heavy || ev.Killed || ev.Dot) return;
            float frac = ev.Amount / Mathf.Max(1f, Def.MaxHp);
            flinchDur = EnemyMotion.FlinchSeconds(frac);
            flinchT = flinchDur;
            flinchTilt = EnemyMotion.FlinchTiltDeg(frac);
            flinchStumble = EnemyMotion.StumbleSpeed(frac);
            var away = Player ? (transform.position - Player.FeetPosition).Flat() : -transform.forward;
            flinchDir = away.sqrMagnitude > 1e-4f ? away.normalized : -transform.forward;
            moveVel = flinchDir * flinchStumble;
        }

        // Retorna true se o tranco tomou conta deste quadro.
        bool UpdateFlinch(float dt)
        {
            if (flinchT <= 0f) return false;
            flinchT -= dt;
            float t = 1f - Mathf.Clamp01(flinchT / Mathf.Max(flinchDur, 0.01f));
            float curve = Mathf.Sin(t * Mathf.PI);   // vai e volta
            var local = transform.InverseTransformDirection(flinchDir);
            model.localRotation = Quaternion.Euler(-flinchTilt * curve * Mathf.Max(0.3f, -local.z), 0f, flinchTilt * 0.5f * curve * local.x);
            // Atacando: só o tranco visual (o ataque continua, a não ser que o golpe forte já tenha interrompido).
            if (state == AiState.Windup || state == AiState.Strike) return false;
            LerpArm(30f, 10f, dt);
            Move(flinchDir * flinchStumble * (1f - t), dt);
            moveVel = Vector3.zero;
            return true;
        }

        // Arma do jogador vindo rápido perto do corpo: passo para o lado ou para trás (chance pela dificuldade).
        bool UpdateDodge(float dt, Vector3 dir, float d)
        {
            dodgeCd -= dt;
            // Tempo de reação: viu o golpe, só sai do lugar depois de ReactionDelay.
            if (dodgeReactT >= 0f)
            {
                dodgeReactT -= dt;
                if (dodgeReactT < 0f && State.CanAct) { dodgeT = EnemyMotion.DodgeSeconds; ArenaAudio.Play(Sfx.Dash, Center, 0.4f, 1.3f); }
            }
            if (dodgeT > 0f)
            {
                dodgeT -= dt;
                // Acelera até a velocidade de andar (não sai disparado).
                moveVel = Vector3.MoveTowards(moveVel, dodgeDir * EnemyMotion.DodgeSpeed, EnemyMotion.Accel * dt);
                Move(moveVel, dt);
                model.localRotation = Quaternion.Euler(-8f, 0f, Vector3.Dot(dodgeDir, transform.right) * -14f);
                return true;
            }
            if (Heavy || dodgeCd > 0f || state != AiState.Approach || !State.CanAct || Player == null) return false;
            var w = Player.Weapon;
            if (w == null || !w.Held || w.TipSpeed < EnemyMotion.DodgeTipSpeed) return false;
            if (Vector3.Distance(w.TipPoint, Center) > EnemyMotion.DodgeRange * k) return false;
            dodgeCd = EnemyMotion.DodgeCooldown;
            if (Random.value >= EnemyMotion.MeleeDodgeChance(SaveSystem.Options.Difficulty)) return false;
            var side = new Vector3(-dir.z, 0f, dir.x) * (Random.value < 0.5f ? -1f : 1f);
            dodgeDir = (side * 0.8f - dir * 0.6f).normalized;
            dodgeReactT = CombatSteering.ReactionDelay(SaveSystem.Options.Difficulty, Random.value);
            return false;
        }

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
            armBase = arm.localPosition;
            SetArm(-35f);
            nextAttack = EnemyAttacks.Pick(Def.Kind, SaveSystem.Options.Difficulty, Random.value);
            strafe = Random.value < 0.5f ? -1 : 1;
            nextAttackDelay = Random.Range(0f, 0.8f);
        }

        // Corpo do goblin (o esqueleto sobrescreve). Precisa criar `arm` (com a arma ao longo de +Y) e `clubMat`.
        protected virtual void BuildVisual()
        {
            bool king = EnemyDef.IsBoss(Def.Kind);
            skinMat = ArenaUtil.Lit(king ? new Color(0.3f, 0.48f, 0.17f) : new Color(0.37f, 0.54f, 0.23f));
            var cloth = ArenaUtil.Lit(king ? new Color(0.35f, 0.12f, 0.12f) : new Color(0.35f, 0.25f, 0.16f));
            ArenaUtil.Prim(PrimitiveType.Cylinder, model, new Vector3(0, 0.55f, 0), new Vector3(0.5f, 0.35f, 0.5f), cloth);
            var head = ArenaUtil.Prim(PrimitiveType.Sphere, model, new Vector3(0, 1.02f, 0), Vector3.one * 0.4f, skinMat).transform;
            headParts.Clear();
            headParts.Add(head);
            var eye = ArenaUtil.Lit(new Color(1f, 0.88f, 0.29f), new Color(0.6f, 0.5f, 0.1f));
            foreach (int s in new[] { -1, 1 })
            {
                var ear = ArenaUtil.Prim(PrimitiveType.Capsule, model, new Vector3(s * 0.24f, 1.08f, 0), new Vector3(0.07f, 0.14f, 0.07f), skinMat).transform;
                ear.localRotation = Quaternion.Euler(0, 0, -s * 65f);
                headParts.Add(ear);
                headParts.Add(ArenaUtil.Prim(PrimitiveType.Sphere, model, new Vector3(s * 0.08f, 1.06f, 0.17f), Vector3.one * 0.07f, eye).transform);
            }
            if (king)
                headParts.Add(ArenaUtil.Prim(PrimitiveType.Cylinder, model, new Vector3(0, 1.2f, 0), new Vector3(0.3f, 0.05f, 0.3f),
                               ArenaUtil.Lit(new Color(0.88f, 0.69f, 0.19f), new Color(0.23f, 0.16f, 0f))).transform);
            arm = new GameObject("Braco").transform;
            arm.SetParent(model, false);
            arm.localPosition = new Vector3(0.28f, 0.85f, 0f);
            clubMat = ArenaUtil.Lit(clubColor);
            ArenaUtil.Prim(PrimitiveType.Cylinder, arm, new Vector3(0, 0.45f, 0), new Vector3(0.14f, 0.4f, 0.14f), clubMat);
            if (Def.Kind == EnemyKind.Goblin || Def.Kind == EnemyKind.Arqueiro || Def.Kind == EnemyKind.Xama) DressGoblinModel();
        }

        // ---------- Modelo do goblin físico (gerado por IA) também nos goblins simples (2026-10-30, pedido do Pablo) ----------
        // Goblin comum, arqueiro e xamã usam as mesmas malhas do goblin físico (Resources/Models/Goblin), montadas na pose
        // de repouso: corpo parado em "Modelo" e o braço direito preso ao `arm` (o mesmo que levanta a clava). As formas
        // simples continuam lá (hitbox da cabeça, brilho), só invisíveis. A clava fica mais curta e sai da mão.
        public const string ArmModelName = "BracoModelo";
        protected bool Dressed { get; private set; }
        // Altura da mão no `arm` com o modelo (a arma começa aqui). Sem modelo = 0 (a clava sai do ombro, como antes).
        protected float HandY => Dressed ? 0.42f : 0f;
        static Material goblinModelMat;
        const float ModelScale = 0.93f;   // o goblin simples é um pouco menor que o físico
        static readonly RagBone[] BodyPieces = { RagBone.Bacia, RagBone.Tronco, RagBone.Cabeca, RagBone.BracoE, RagBone.AntebracoE, RagBone.CoxaE, RagBone.CanelaE, RagBone.CoxaD, RagBone.CanelaD };
        static readonly RagBone[] ArmPieces = { RagBone.BracoD, RagBone.AntebracoD };

        void DressGoblinModel()
        {
            if (!goblinModelMat)
            {
                var tex = Resources.Load<Texture2D>("Models/Goblin/Goblin_cor");
                if (!tex) return;
                goblinModelMat = new Material(Shader.Find("Standard")) { mainTexture = tex, color = Color.white };
                goblinModelMat.SetFloat("_Glossiness", 0.12f);
            }
            if (!Resources.Load<Mesh>("Models/Goblin/Goblin_" + RagBone.Tronco)) return;
            foreach (var r in model.GetComponentsInChildren<Renderer>()) if (!r.transform.IsChildOf(arm)) r.enabled = false;
            var body = new GameObject("CorpoModelo").transform;
            body.SetParent(model, false);
            body.localScale = Vector3.one * ModelScale;
            foreach (var id in BodyPieces) AddPiece(body, id);
            // Braço direito: no `arm`, de cabeça para baixo (no `arm` o "para cima" é para onde a clava aponta) e com o
            // ombro do modelo no pivô do `arm`.
            var armHolder = new GameObject(ArmModelName).transform;
            armHolder.SetParent(arm, false);
            armHolder.localRotation = Quaternion.Euler(180f, 0f, 0f);
            armHolder.localScale = Vector3.one * ModelScale;
            var shoulder = new Vector3(0.23f, 0.93f, 0f) * ModelScale;
            armHolder.localPosition = new Vector3(-shoulder.x, shoulder.y, 0f);
            foreach (var id in ArmPieces) AddPiece(armHolder, id);
            // Clava mais curta saindo da mão (mesmo alcance de antes: a ponta continua em ~0,88).
            foreach (Transform c in arm)
                if (c != armHolder) { c.localPosition = new Vector3(0f, 0.66f, 0f); c.localScale = new Vector3(0.15f, 0.23f, 0.15f); }
            Dressed = true;
        }

        static void AddPiece(Transform parent, RagBone id)
        {
            var mesh = Resources.Load<Mesh>("Models/Goblin/Goblin_" + id);
            if (!mesh) return;
            var v = new GameObject("Modelo_" + id);
            v.transform.SetParent(parent, false);
            v.AddComponent<MeshFilter>().sharedMesh = mesh;
            v.AddComponent<MeshRenderer>().sharedMaterial = goblinModelMat;
        }

        protected readonly List<Transform> headParts = new List<Transform>();
        protected override List<Transform> HeadParts => headParts;

        // Ganchos para variações (esqueleto sem braço não ataca, sem cabeça gira devagar e demora mais).
        protected virtual bool CanAttackNow => true;
        protected virtual float TurnSpeed => 10f;
        protected virtual float WindupTime => windup;

        // Ângulo do braço em graus no eixo X local (negativo = para trás/cima, positivo = golpe para baixo/frente).
        // Inclinação de -18° em Y: a clava sai do ombro direito e desce no centro (no jogador), não ao lado.
        protected void SetArm(float deg) => SetPose(deg, -18f, 0f);
        float armAngle, armYaw = -18f, armFwd;
        Vector3 armBase;
        float ArmAngle => armAngle;
        protected void LerpArm(float deg, float speed, float dt) => LerpPose(new ArmPose(deg, -18f), speed, dt);

        // Pose do braço da arma (pitch/yaw do ombro + avanço para a estocada). Braço arrancado (esqueleto) não é mexido.
        protected void SetPose(float pitch, float yaw, float forward)
        {
            armAngle = pitch; armYaw = yaw; armFwd = forward;
            if (arm == null || arm.parent != model) return;
            arm.localRotation = Quaternion.Euler(pitch, yaw, 0);
            arm.localPosition = armBase + Vector3.forward * forward;
        }
        void LerpPose(ArmPose p, float speed, float dt)
        {
            float t = Mathf.Min(1f, dt * speed);
            SetPose(Mathf.Lerp(armAngle, p.Pitch, t), Mathf.LerpAngle(armYaw, p.Yaw, t), Mathf.Lerp(armFwd, p.Forward, t));
        }

        // Repertório de ataques (EnemyAttacks): o próximo é sorteado ao fim de cada ataque.
        protected EnemyAttack attack = EnemyAttack.Cima, nextAttack = EnemyAttack.Cima;

        public void ClubSegment(out Vector3 a, out Vector3 b)
        {
            a = arm.TransformPoint(new Vector3(0, 0.1f, 0));
            b = arm.TransformPoint(new Vector3(0, 0.88f, 0));
        }

        protected void SetState(AiState s)
        {
            state = s; stateT = 0f; hitDone = false; clubPrevValid = false;
            if (s != AiState.Windup && s != AiState.Strike) { ReleaseToken(); if (s != AiState.Windup) riposting = false; }
            if (s == AiState.Windup) attack = nextAttack;
            if (s == AiState.Recover) nextAttack = EnemyAttacks.Pick(Def.Kind, SaveSystem.Options.Difficulty, Random.value);
            bool glow = s == AiState.Windup || s == AiState.Slam;
            if (s == AiState.Windup || s == AiState.Slam) ArenaAudio.Play(Sfx.Windup, transform.position + Vector3.up, 0.7f, 1f / k);
            clubMat.color = glow ? new Color(0.9f, 0.15f, 0.05f) : clubColor;
        }

        protected override void OnHit(DamageEvent ev)
        {
            lastDamagedT = Time.time;   // janela de combo: logo depois de apanhar não bloqueia
            StartFlinch(ev);
            // Golpe forte (fração da vida, pela dificuldade) cancela o ataque anunciado.
            if (state == AiState.Windup && !ev.Killed && !(this is IBoss) && CombatAI.Interrupts(ev.Amount, Def.MaxHp, SaveSystem.Options.Difficulty))
            {
                SetState(AiState.Recover);
                ArenaFx.FloatText("INTERROMPIDO", Center + Vector3.up * 0.8f, new Color(1f, 0.9f, 0.5f), 0.9f);
            }
            // Muito ferido: às vezes recua um pouco (uma vez) antes de voltar.
            if (!ev.Killed && state != AiState.Strike && GoblinTactics.ShouldRetreat(Def.Kind, State.HpFraction, retreated, Random.value))
            {
                retreated = true;
                SetState(AiState.Retreat);
                ArenaFx.FloatText("RECUOU", Center + Vector3.up * 0.7f, new Color(0.85f, 0.85f, 0.6f), 0.9f);
            }
        }

        // Bloqueio com a clava (GoblinTactics): só de frente, parado ou se recuperando, sem estar atordoado.
        float blockCd;
        protected override float BlockMultiplier(float tipSpeed, WeaponDef weapon, float damage)
        {
            if (this is IBoss || this is ArcherEnemy || this is ShamanEnemy) return 1f;
            bool canBlock = blockCd <= 0f && State.CanAct && !State.Posture.IsBroken && (state == AiState.Approach || state == AiState.Recover)
                            && CombatAI.CanBlockAfterHit(Time.time - lastDamagedT)
                            && (this is not SkeletonEnemy || CanAttackNow);   // esqueleto sem o braço da espada não bloqueia
            var from = (Player.FeetPosition - transform.position).Flat();
            float facing = from.sqrMagnitude > 1e-4f ? Vector3.Dot(transform.forward, from.normalized) : 0f;
            var r = GoblinTactics.ResolveBlock(Def.Kind, canBlock, facing, Random.value, tipSpeed, weapon.Weight, Player.Weapon.TwoHanded);
            if (r == GoblinTactics.BlockOutcome.None) return 1f;
            blockCd = GoblinTactics.BlockCooldown;
            var at = arm.TransformPoint(new Vector3(0, 0.5f, 0));
            SetArm(-80f);   // clava atravessada na frente
            State.AddPosture(damage * GoblinTactics.BlockPostureFactor);
            ArenaAudio.Play(Sfx.Block, at, 0.9f, 0.9f);
            ArenaFx.Burst(at, new Color(1f, 0.82f, 0.56f), 8, 2.5f);
            ArenaInput.Haptic(0.6f, 0.06f);
            if (r == GoblinTactics.BlockOutcome.Blocked)
            {
                ArenaFx.FloatText("BLOQUEOU", at + Vector3.up * 0.2f, new Color(0.85f, 0.85f, 0.85f), 0.9f);
                // Revide: depois de bloquear, contra-ataca rápido (chance e espera pela dificuldade).
                var diff = SaveSystem.Options.Difficulty;
                if (Random.value < CombatAI.RiposteChance(diff)) riposteT = CombatAI.Pick(CombatAI.RiposteDelay(diff), Random.value);
                return 0f;
            }
            ArenaFx.FloatText("ATRAVESSOU A DEFESA", at + Vector3.up * 0.2f, new Color(1f, 0.7f, 0.4f), 0.9f);
            return 0.5f;
        }

        protected override void Update()
        {
            blockCd -= Time.deltaTime;
            base.Update();
        }

        // ---------- Fichas de ataque, revide ----------
        float lastDamagedT = -10f, riposteT;
        bool riposting;
        bool UsesTokens => !(this is IBoss) && !(this is ArcherEnemy) && !(this is ShamanEnemy);
        bool HasToken => !UsesTokens || ArenaGame.Tokens.Has(GetInstanceID());
        bool TokensFull => UsesTokens && !HasToken && ArenaGame.Tokens.InUse >= ArenaGame.Tokens.Capacity;
        bool TryTakeToken() => !UsesTokens || ArenaGame.Tokens.TryTake(GetInstanceID());
        void ReleaseToken() { if (UsesTokens) ArenaGame.Tokens.Release(GetInstanceID()); }

        protected override void OnDestroy() { ReleaseToken(); base.OnDestroy(); }

        protected override EnemyActivity Activity => State.Stun > 0f ? EnemyActivity.Cambaleando
            : state == AiState.Windup || state == AiState.Strike || state == AiState.Slam ? EnemyActivity.Atacando : EnemyActivity.Guarda;

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
            if (moveVel.sqrMagnitude < 0.04f && flinchT <= 0f) { model.localRotation = Quaternion.Slerp(model.localRotation, Quaternion.identity, dt * 10f); }
            if (State.Rooted <= 0f) FacePlayer(toPlayer, dt, TurnSpeed);
            stateT += dt;
            if (ExtraAI(dt, d)) return;
            var dir = d > 1e-3f ? toPlayer / d : transform.forward;
            if (UpdateFlinch(dt)) return;
            if (UpdateDodge(dt, dir, d)) return;
            if (riposteT > 0f)
            {
                riposteT -= dt;
                if (riposteT <= 0f && (state == AiState.Approach || state == AiState.Recover) && State.CanAct && CanAttackNow && d < 1.8f * k)
                {
                    TryTakeToken();   // o revide não espera ficha
                    riposting = true;
                    SetState(AiState.Windup);
                    ArenaFx.FloatText("REVIDE!", Center + Vector3.up * 0.9f, new Color(1f, 0.5f, 0.4f), 1f);
                    return;
                }
            }

            switch (state)
            {
                case AiState.Approach:
                {
                    var side = new Vector3(-dir.z, 0, dir.x) * strafe * 0.8f;
                    // Chega em curva (ângulo próprio que some perto), corre de longe e ziguezagueia na média distância.
                    if (float.IsNaN(approachAngle)) { approachAngle = Heavy ? 0f : EnemyMotion.PickApproachAngle(Random.value); weaveSeed = Random.value * 10f; }
                    var curved = Quaternion.Euler(0f, EnemyMotion.ApproachAngle(approachAngle, d / k), 0f) * dir;
                    var lateral = new Vector3(-dir.z, 0, dir.x) * (Heavy ? 0f : EnemyMotion.Weave(Time.time, weaveSeed, d / k));
                    var want = d > 1.25f * k ? (curved + lateral).normalized * Def.MoveSpeed * EnemyMotion.SpeedFactor(d / k) : d < 0.9f * k ? -dir : Vector3.zero;
                    // Em grupo: cada um vai para a sua vaga num arco à frente do jogador (cercam pelos lados).
                    float slot = GroupSlot();
                    if (Mathf.Abs(slot) > 1f && d < GoblinTactics.EngageDistance * k)
                    {
                        var spot = Player.FeetPosition + Quaternion.Euler(0, slot, 0) * Player.HeadForward * 1.3f * k;
                        var toSpot = (spot - transform.position).Flat();
                        want = toSpot.magnitude > 0.35f ? toSpot.normalized * Def.MoveSpeed * 0.9f : Vector3.zero;
                        if (d < 0.9f * k) want -= dir;
                    }
                    else if (EnemyMotion.Engaged(d / k)) want += side + dir * EnemyMotion.Pace(Time.time, weaveSeed, d / k) * Def.MoveSpeed;   // de perto: de lado e vai-e-volta
                    // Sem ficha de ataque: fica rondando a ~2 m, esperando a vez.
                    if (TokensFull && d < 2f * k) want = -dir * Def.MoveSpeed * 0.5f + side;
                    Steer(want, dt);
                    LerpArm(-35f, 5f, dt);
                    if (d < EnemyAttacks.Range(nextAttack) * k && stateT > (k > 1f ? 0.6f : 1.0f) + nextAttackDelay && State.CanAct && CanAttackNow && TryTakeToken())
                    {
                        nextAttackDelay = CombatAI.Pick(CombatAI.AttackDelay(SaveSystem.Options.Difficulty), Random.value);
                        SetState(AiState.Windup);
                    }
                    break;
                }
                case AiState.Windup:
                    LerpPose(EnemyAttacks.Poses(attack).windup, 8f, dt);
                    if (attack == EnemyAttack.Passo && d > 1.2f * k) Move(dir * 0.8f, dt);   // vem chegando
                    if (stateT > WindupTime * (riposting ? CombatAI.RiposteWindupMultiplier : 1f)) SetState(AiState.Strike);
                    break;
                case AiState.Strike:
                {
                    float kk = Mathf.Min(1f, stateT / 0.28f);
                    var (w0, w1) = EnemyAttacks.Poses(attack);
                    float e = kk * kk;
                    SetPose(Mathf.Lerp(w0.Pitch, w1.Pitch, e), Mathf.Lerp(w0.Yaw, w1.Yaw, e), Mathf.Lerp(w0.Forward, w1.Forward, e));
                    float lunge = attack == EnemyAttack.Passo ? EnemyAttacks.StepSpeed : attack == EnemyAttack.Estocada ? EnemyAttacks.ThrustLunge : 1.2f;
                    if (d > 0.8f * k) Move(dir * lunge, dt);
                    if (!hitDone) CheckClubContact(kk, d);
                    if (stateT > 0.4f) SetState(AiState.Recover);
                    break;
                }
                case AiState.Retreat:
                    LerpArm(-20f, 4f, dt);
                    Steer(-dir * Def.MoveSpeed * 1.1f, dt);
                    if (stateT > GoblinTactics.RetreatSeconds) SetState(AiState.Approach);
                    break;
                case AiState.Recover:
                    LerpArm(-35f, 4f, dt);
                    Steer(-dir * 0.6f + new Vector3(-dir.z, 0, dir.x) * strafe * 0.5f, dt);   // recua de lado
                    if (stateT > (k > 1f ? 0.7f : 0.9f)) { SetState(AiState.Approach); strafe = -strafe; }
                    break;
            }
        }

        protected virtual void OnStunned() { }

        // Vaga deste goblin no grupo (goblins e esqueletos vivos, sem o chefe nem arqueiros), em ordem estável.
        float GroupSlot()
        {
            if (this is IBoss) return 0f;   // o chefe vem de frente
            int count = 0, index = 0, me = GetInstanceID();
            foreach (var e in All)
            {
                if (!(e is GoblinEnemy) || e is IBoss || e is ArcherEnemy || e is ShamanEnemy || e.Dead) continue;
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
