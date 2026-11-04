using System.Collections.Generic;
using Arena.Core;
using UnityEngine;

namespace Arena
{
    // Sala de Reflexo (pedido do Pablo, 2026-11-04). Roda dentro do Treino (sem dano, estamina infinita), como a Sala
    // de Criação; portal REFLEXO no santuário. Você fica no círculo dourado:
    //   - 4 lançadores em arco atiram orbes de magia (verdes, os do xamã) e flechas (as do arqueiro). Cristal acende
    //     antes do disparo (verde = orbe, amarelo = flecha). Rebater com a arma, bloquear com o escudo ou desviar.
    //   - Máquina de aparo bem na frente: ergue a clava (brilho laranja = aviso) e golpeia de cima, da esquerda ou da
    //     direita. Arma rápida contra a clava (ou escudo empurrado) = APARO; parada = bloqueio; corpo = acerto.
    //   - Progressivo: as duas estações começam lentas e aceleram até o limite no tempo escolhido.
    // Tudo se ajusta na PAUSA (linhas "Reflexo: ..."; ReflexRows). O placar fica em cima da máquina.
    public sealed class ReflexRoom : MonoBehaviour
    {
        public static ReflexRoom Instance { get; private set; }

        static readonly Vector3 Stand = new Vector3(0f, 0f, -3f);
        static readonly Vector3 Facing = Vector3.forward;
        const float MachineDistance = 1.3f, LauncherRadius = 9f, PivotHeight = 1.6f;
        const float ArmIn = 0.7f, ArmOut = 1.45f;   // trecho da clava ao longo do braço (m a partir do pivô)
        static readonly float[] LauncherAngles = { -55f, -25f, 25f, 55f };
        static readonly Color OrbCol = new Color(0.55f, 1f, 0.35f), ArrowCol = new Color(1f, 0.85f, 0.3f), Warn = new Color(1f, 0.5f, 0.1f);

        public readonly ReflexScore MachineScore = new ReflexScore(), ProjectileScore = new ReflexScore();
        Transform root, machine, pivot;
        Material clubMat;
        TextMesh board;
        readonly List<(Transform t, Material crystal)> launchers = new List<(Transform, Material)>();
        readonly List<GameObject> shots = new List<GameObject>();
        float elapsed;
        bool lastRamp;

        static ReflexSettings S => SaveSystem.Options.reflex;
        ArenaPlayer Player => ArenaGame.Instance.Player;

        void Awake()
        {
            Instance = this;
            root = new GameObject("SalaDeReflexo").transform;
            foreach (var e in ArenaEnemy.All.ToArray()) if (e is TrainingDummy) Destroy(e.gameObject);
            // Círculo onde ficar.
            ArenaUtil.Prim(PrimitiveType.Cylinder, root, Stand + Vector3.up * 0.02f, new Vector3(1.1f, 0.02f, 1.1f), ArenaUtil.Unlit(new Color(1f, 0.85f, 0.4f, 0.35f)));
            BuildMachine();
            foreach (float a in LauncherAngles) BuildLauncher(a);
            board = ArenaFx.MakeText(root, "", 0.011f, Color.white);
            board.transform.position = Stand + Facing * (MachineDistance + 1.2f) + Vector3.up * 2.6f;
            board.transform.rotation = Quaternion.LookRotation(Facing);
            lastRamp = S.ramp;
            var game = ArenaGame.Instance;
            game.Player.TeleportTo(Stand, Stand + Facing * 5f);
            game.Message("SALA DE REFLEXO: fique no círculo. Pausa = ligar/desligar e mudar as velocidades.", 4.5f);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (root) Destroy(root.gameObject);
            foreach (var s in shots) if (s) Destroy(s);
        }

        // Zera o placar e recomeça o progressivo.
        public void Restart()
        {
            MachineScore.Reset(); ProjectileScore.Reset();
            elapsed = 0f; shotT = 1.5f; phase = MPhase.Rest; phaseT = 0f; machineWait = 1f;
            foreach (var s in shots) if (s) Destroy(s);
            shots.Clear();
            ArenaGame.Instance.Message("Reflexo: placar zerado" + (S.ramp ? ", progressivo recomeçou" : ""), 2f);
        }

        void Update()
        {
            if (ArenaGame.Paused) return;
            float dt = Time.deltaTime;
            if (S.ramp != lastRamp) { lastRamp = S.ramp; elapsed = 0f; }
            if (S.orbs || S.arrows || S.machine) elapsed += dt;
            UpdateLaunchers(dt);
            UpdateMachine(dt);
            shots.RemoveAll(s => !s);
            UpdateBoard();
        }

        // ---------- Lançadores ----------
        float shotT = 1.5f, teleT = -1f;
        int shotLauncher; bool shotIsOrb;

        void BuildLauncher(float angle)
        {
            var dir = Quaternion.AngleAxis(angle, Vector3.up) * Facing;
            var t = new GameObject("Lancador").transform;
            t.SetParent(root, false);
            t.position = Stand + dir * LauncherRadius;
            t.rotation = Quaternion.LookRotation(-dir);
            var stone = ArenaUtil.Lit(new Color(0.3f, 0.28f, 0.31f));
            ArenaUtil.Prim(PrimitiveType.Cylinder, t, new Vector3(0, 0.7f, 0), new Vector3(0.5f, 0.7f, 0.5f), stone, keepCollider: true);
            ArenaUtil.Prim(PrimitiveType.Cube, t, new Vector3(0, 1.45f, 0), new Vector3(0.6f, 0.12f, 0.6f), stone);
            var crystal = ArenaUtil.Lit(new Color(0.25f, 0.25f, 0.3f), Color.black);
            var c = ArenaUtil.Prim(PrimitiveType.Sphere, t, new Vector3(0, 1.75f, 0), Vector3.one * 0.32f, crystal);
            c.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
            launchers.Add((t, crystal));
        }

        void UpdateLaunchers(float dt)
        {
            bool any = S.orbs || S.arrows;
            float speed = S.ProjectileNow(elapsed);
            if (teleT >= 0f)
            {
                teleT += dt;
                float tele = ReflexRules.Telegraph(speed);
                var (t, crystal) = launchers[shotLauncher];
                float k = Mathf.Clamp01(teleT / tele);
                crystal.SetColor("_EmissionColor", (shotIsOrb ? OrbCol : ArrowCol) * (0.4f + 2.2f * k * (0.75f + 0.25f * Mathf.Sin(Time.time * 40f))));
                if (teleT >= tele) { Fire(t, shotIsOrb, speed); crystal.SetColor("_EmissionColor", Color.black); teleT = -1f; }
                return;
            }
            if (!any) return;
            shotT -= dt;
            if (shotT > 0f) return;
            shotT = ReflexRules.ShotInterval(speed) * Random.Range(0.8f, 1.2f);
            shotLauncher = Random.Range(0, launchers.Count);
            shotIsOrb = S.orbs && (!S.arrows || Random.value < 0.5f);
            teleT = 0f;
            ArenaAudio.Play(Sfx.Windup, launchers[shotLauncher].t.position + Vector3.up * 1.7f, 0.5f, shotIsOrb ? 0.8f : 1.3f);
        }

        void Fire(Transform launcher, bool orb, float speed)
        {
            var pl = Player;
            var from = launcher.position + Vector3.up * 1.75f + launcher.forward * 0.3f;
            var target = pl.HeadPosition - Vector3.up * Random.Range(0.1f, 0.55f) + Random.insideUnitSphere * 0.12f;
            if (orb)
            {
                var o = MagicOrb.Spawn(from, (target - from).normalized * ReflexRules.OrbSpeed(speed), 8f, null);
                o.OnResult = r => Result(ProjectileScore, r, o ? o.transform.position : from);
                shots.Add(o.gameObject);
                ArenaAudio.Play(Sfx.Skill, from, 0.7f, 0.7f);
            }
            else
            {
                float v = ReflexRules.ArrowSpeed(speed);
                var flat = (target - from).Flat();
                float lob = ArrowRules.LobAngle(flat.magnitude, v);
                var aim = Quaternion.AngleAxis(-lob * Mathf.Rad2Deg, Vector3.Cross(Vector3.up, flat.normalized)) * (target - from).normalized;
                var a = Arrow.Spawn(from, aim * v, 8f, null);
                a.ReturnTo = from;
                a.OnResult = r => Result(ProjectileScore, r, a ? a.transform.position : from);
                shots.Add(a.gameObject);
                ArenaAudio.Play(Sfx.Whoosh, from, 0.7f, 1.6f);
            }
        }

        // ---------- Máquina de aparo ----------
        enum MPhase { Rest, Windup, Swing, Recoil }
        enum MAttack { Cima, DaEsquerda, DaDireita }
        MPhase phase = MPhase.Rest;
        MAttack attack;
        float phaseT, machineWait = 1.5f, phaseLen, sideOffset, tilt;
        Quaternion poseFrom;
        Vector3 clubPrevA, clubPrevB;
        bool clubPrevValid, contact;

        void BuildMachine()
        {
            machine = new GameObject("MaquinaDeAparo").transform;
            machine.SetParent(root, false);
            machine.position = Stand + Facing * MachineDistance;
            machine.rotation = Quaternion.LookRotation(-Facing);
            var wood = ArenaUtil.Lit(new Color(0.42f, 0.27f, 0.13f));
            var iron = ArenaUtil.Lit(new Color(0.35f, 0.37f, 0.42f), new Color(0.03f, 0.03f, 0.04f));
            ArenaUtil.Prim(PrimitiveType.Cylinder, machine, new Vector3(0, 0.05f, 0), new Vector3(0.8f, 0.05f, 0.8f), iron);
            // O poste fica um pouco atrás do pivô para a clava passar na frente dele.
            ArenaUtil.Prim(PrimitiveType.Cylinder, machine, new Vector3(0, PivotHeight / 2f, -0.15f), new Vector3(0.22f, PivotHeight / 2f, 0.22f), wood, keepCollider: true);
            pivot = new GameObject("Pivo").transform;
            pivot.SetParent(machine, false);
            pivot.localPosition = new Vector3(0, PivotHeight, 0);
            ArenaUtil.Prim(PrimitiveType.Sphere, pivot, Vector3.zero, Vector3.one * 0.2f, iron);
            ArenaUtil.Prim(PrimitiveType.Cube, pivot, new Vector3(0, ArmIn / 2f, 0), new Vector3(0.06f, ArmIn, 0.06f), wood);
            clubMat = ArenaUtil.Lit(new Color(0.5f, 0.33f, 0.18f), Color.black);
            ArenaUtil.Prim(PrimitiveType.Cylinder, pivot, new Vector3(0, (ArmIn + ArmOut) / 2f, 0), new Vector3(0.12f, (ArmOut - ArmIn) / 2f, 0.12f), clubMat);
            pivot.localRotation = RestPose;
        }

        // Pose no espaço da máquina (frente = +z, para o jogador). O braço aponta para o +Y local do pivô.
        static Quaternion RestPose => Quaternion.AngleAxis(160f, Vector3.right);
        Quaternion Pose(float e)
        {
            switch (attack)
            {
                case MAttack.Cima: return Quaternion.AngleAxis(sideOffset, Vector3.up) * Quaternion.AngleAxis(Mathf.Lerp(-40f, 118f, e), Vector3.right);
                case MAttack.DaEsquerda: return Quaternion.AngleAxis(Mathf.Lerp(115f, -65f, e), Vector3.up) * Quaternion.AngleAxis(90f + tilt, Vector3.right);
                default: return Quaternion.AngleAxis(Mathf.Lerp(-115f, 65f, e), Vector3.up) * Quaternion.AngleAxis(90f + tilt, Vector3.right);
            }
        }

        void ClubSegment(out Vector3 a, out Vector3 b)
        {
            var d = pivot.up;
            a = pivot.position + d * ArmIn;
            b = pivot.position + d * ArmOut;
        }

        void SetPhase(MPhase p, float len) { phase = p; phaseT = 0f; phaseLen = len; poseFrom = pivot.localRotation; }

        void UpdateMachine(float dt)
        {
            float speed = S.MachineNow(elapsed);
            phaseT += dt;
            float k = phaseLen > 0f ? Mathf.Clamp01(phaseT / phaseLen) : 1f;
            switch (phase)
            {
                case MPhase.Rest:
                    pivot.localRotation = Quaternion.Slerp(pivot.localRotation, RestPose, 1f - Mathf.Exp(-6f * dt));
                    Glow(0f, Warn);
                    if (!S.machine) return;
                    machineWait -= dt;
                    if (machineWait > 0f) return;
                    attack = (MAttack)Random.Range(0, 3);
                    sideOffset = Random.Range(-18f, 18f);
                    tilt = Random.Range(8f, 32f);   // altura do golpe lateral: cabeça (8°) a peito (32°)
                    SetPhase(MPhase.Windup, ReflexRules.Windup(speed));
                    ArenaAudio.Play(Sfx.Windup, pivot.position, 0.6f, 0.9f);
                    break;
                case MPhase.Windup:
                    pivot.localRotation = Quaternion.Slerp(poseFrom, Pose(0f), Mathf.SmoothStep(0f, 1f, k));
                    Glow(0.3f + 1.7f * k, Warn);
                    if (k >= 1f) { SetPhase(MPhase.Swing, ReflexRules.Swing(speed)); clubPrevValid = false; contact = false; ArenaAudio.Play(Sfx.Whoosh, pivot.position, 0.8f, 0.8f); }
                    break;
                case MPhase.Swing:
                    pivot.localRotation = Pose(k * k);
                    Glow(2.2f, new Color(1f, 0.15f, 0.05f));
                    CheckContact();
                    if (contact) break;
                    if (k >= 1f)
                    {
                        // Passou sem encostar: se você estava ao alcance, desviou (saiu do golpe).
                        if ((Player.FeetPosition - machine.position).Flat().magnitude < MachineDistance + 0.9f) Result(MachineScore, ReflexOutcome.Desviou, pivot.position + pivot.up * ArmOut);
                        SetPhase(MPhase.Rest, 0f);
                        machineWait = ReflexRules.Pause(speed);
                    }
                    break;
                case MPhase.Recoil:
                    pivot.localRotation = Quaternion.Slerp(poseFrom, Pose(0.1f), Mathf.SmoothStep(0f, 1f, k));
                    Glow(0f, Warn);
                    if (k >= 1f) { SetPhase(MPhase.Rest, 0f); machineWait = ReflexRules.Pause(speed); }
                    break;
            }
        }

        void Glow(float level, Color c) => clubMat.SetColor("_EmissionColor", c * level);

        // Mesmo teste do goblin (GoblinEnemy.CheckClubContact): caminho inteiro da clava entre quadros contra escudo,
        // armas das duas mãos e corpo (cintura à cabeça).
        void CheckContact()
        {
            ClubSegment(out var a, out var b);
            if (!clubPrevValid) { clubPrevA = a; clubPrevB = b; clubPrevValid = true; }
            var pl = Player;
            const float tol = 0.06f;
            float stamina = pl.State.Stamina.Current;
            var shield = pl.Shield;
            if (shield && shield.Equipped && (shield.Intercepts(clubPrevA, clubPrevB, a, b, tol) || (!pl.IsVr && shield.DesktopCovers(machine.position))))
            {
                Guarded(Guard.ResolveShield(shield.GuardSpeed, false, stamina), b, true);
                return;
            }
            foreach (var w in new[] { pl.Weapon, pl.WeaponL })
            {
                if (w == null || !w.Held || w.Def == null) continue;
                if (!Geometry.SweptSegmentsHit(w.BasePrev.ToN(), w.TipPrev.ToN(), w.BasePoint.ToN(), w.TipPoint.ToN(),
                                               clubPrevA.ToN(), clubPrevB.ToN(), a.ToN(), b.ToN(), 0.1f + tol)) continue;
                float sp = Mathf.Max(w.TipSpeed, (w.TipPoint - w.TipPrev).magnitude / Mathf.Max(Time.deltaTime, 1e-4f));
                Guarded(Guard.Resolve(sp, false, stamina), b, w == pl.WeaponL);
                return;
            }
            // Desktop sem escudo: botão direito olhando para a máquina apara com a arma.
            if (!pl.IsVr && pl.Input.DesktopParry && (shield == null || !shield.Equipped)
                && Vector3.Dot(pl.HeadForward, (machine.position - pl.FeetPosition).Flat().normalized) > 0.5f)
            {
                Guarded(GuardResult.Parry, b, false);
                return;
            }
            var head = pl.HeadPosition;
            var waist = new Vector3(head.x, pl.FeetPosition.y + 0.45f, head.z);
            if (Geometry.SweptSegmentsHit(waist.ToN(), head.ToN(), waist.ToN(), head.ToN(), clubPrevA.ToN(), clubPrevB.ToN(), a.ToN(), b.ToN(), GoblinEnemy.PlayerHitRadius + tol))
            {
                contact = true;
                Result(MachineScore, ReflexOutcome.Acertou, b);
                SetPhase(MPhase.Recoil, 0.35f);
                return;
            }
            clubPrevA = a; clubPrevB = b;
        }

        void Guarded(GuardResult r, Vector3 at, bool left)
        {
            contact = true;
            if (r == GuardResult.Parry)
            {
                ArenaAudio.Play(Sfx.Parry, at);
                ArenaFx.Burst(at, new Color(1f, 0.91f, 0.63f), 16, 4f);
                ArenaInput.Haptic(1f, 0.09f, left);
                Result(MachineScore, ReflexOutcome.Aparou, at);
                SetPhase(MPhase.Recoil, 0.25f);   // a clava volta com o tranco
            }
            else if (r == GuardResult.GuardBroken)
            {
                Result(MachineScore, ReflexOutcome.Acertou, at);
                SetPhase(MPhase.Recoil, 0.35f);
            }
            else
            {
                ArenaAudio.Play(Sfx.Block, at);
                ArenaFx.Burst(at, new Color(1f, 0.82f, 0.56f), 8, 2f);
                ArenaInput.Haptic(0.6f, 0.07f, left);
                Result(MachineScore, ReflexOutcome.Bloqueou, at);
                SetPhase(MPhase.Recoil, 0.45f);
            }
        }

        // ---------- Placar ----------
        void Result(ReflexScore score, ReflexOutcome o, Vector3 at)
        {
            if (!this) return;   // a sala fechou antes do projétil terminar
            score.Add(o);
            switch (o)
            {
                case ReflexOutcome.Aparou: ArenaFx.FloatText("APARO!", at, new Color(0.62f, 0.88f, 1f), 1.1f); break;
                case ReflexOutcome.Bloqueou: ArenaFx.FloatText("BLOQUEIO", at, new Color(0.85f, 0.85f, 0.85f), 0.9f); break;
                case ReflexOutcome.Desviou: ArenaFx.FloatText("DESVIOU", at + Vector3.up * 0.2f, new Color(0.6f, 1f, 0.6f), 0.8f); break;
                case ReflexOutcome.Acertou:
                    ArenaFx.FloatText("ACERTOU VOCÊ", at, new Color(1f, 0.4f, 0.4f), 1f);
                    ArenaAudio.Play2D(Sfx.Hurt, 0.6f);
                    ArenaInput.Haptic(0.8f, 0.12f); ArenaInput.Haptic(0.8f, 0.12f, left: true);
                    Player.Vignette(0.5f);
                    break;
            }
        }

        void UpdateBoard()
        {
            var s = S;
            string speedLine = s.ramp
                ? "<color=#ffd36b>PROGRESSIVO " + ReflexRules.Label(s.ProjectileNow(elapsed)) + "</color> (" + ReflexRules.Label(s.RampStart) + " → " + ReflexRules.Label(s.RampLimit)
                  + (elapsed < s.RampSeconds ? ", falta " + Mathf.CeilToInt(s.RampSeconds - elapsed) + " s)" : ", no limite)")
                : "";
            var m = MachineScore; var p = ProjectileScore;
            board.text =
                (s.machine ? "<b>APARO</b> " + (s.ramp ? "" : ReflexRules.Label(s.MachineSpeed)) : "<color=#888>APARO desligado</color>")
                + "\n<size=40>aparos " + m.Count(ReflexOutcome.Aparou) + " · bloqueios " + m.Count(ReflexOutcome.Bloqueou) + " · desvios " + m.Count(ReflexOutcome.Desviou)
                + " · <color=#ff8080>acertos " + m.Count(ReflexOutcome.Acertou) + "</color> · sequência " + m.Streak + " (melhor " + m.BestStreak + ")</size>\n"
                + (s.orbs || s.arrows ? "<b>PROJÉTEIS</b> " + (s.ramp ? "" : ReflexRules.Label(s.ProjectileSpeed)) + "<size=40> (" + (s.orbs ? "orbes" : "") + (s.orbs && s.arrows ? " + " : "") + (s.arrows ? "flechas" : "") + ")</size>"
                                      : "<color=#888>PROJÉTEIS desligados</color>")
                + "\n<size=40>rebatidos " + p.Count(ReflexOutcome.Rebateu) + " · bloqueados " + p.Count(ReflexOutcome.Bloqueou) + " · desviados " + p.Count(ReflexOutcome.Desviou)
                + " · <color=#ff8080>acertos " + p.Count(ReflexOutcome.Acertou) + "</color> · sequência " + p.Streak + " (melhor " + p.BestStreak + ")</size>"
                + (speedLine.Length > 0 ? "\n" + speedLine : "")
                + "\n<size=36><color=#999>Pausa: ligar/desligar e mudar as velocidades</color></size>";
        }

        // Linhas da pausa enquanto a sala está aberta (o PauseMenu chama). true = salvar as opções.
        public static void ReflexRows(List<(string label, System.Func<bool> act)> rows)
        {
            var s = S;
            const string C = "<color=#7fe0ff>Reflexo</color> · ";
            string On(bool b) => b ? "ligado" : "desligado";
            rows.Add((C + "Orbes de magia: " + On(s.orbs), () => { s.orbs = !s.orbs; return true; }));
            rows.Add((C + "Flechas: " + On(s.arrows), () => { s.arrows = !s.arrows; return true; }));
            rows.Add((C + "Velocidade dos projéteis: " + ReflexRules.Label(s.ProjectileSpeed) + (s.ramp ? " <size=40><color=#999>(progressivo manda)</color></size>" : ""), () => { s.CycleProjectileSpeed(); return true; }));
            rows.Add((C + "Máquina de aparo: " + On(s.machine), () => { s.machine = !s.machine; return true; }));
            rows.Add((C + "Velocidade dos golpes: " + ReflexRules.Label(s.MachineSpeed) + (s.ramp ? " <size=40><color=#999>(progressivo manda)</color></size>" : ""), () => { s.CycleMachineSpeed(); return true; }));
            rows.Add((C + "Progressivo: " + On(s.ramp), () => { s.ramp = !s.ramp; return true; }));
            if (s.ramp)
            {
                rows.Add((C + "  começa em " + ReflexRules.Label(s.RampStart), () => { s.CycleRampStart(); return true; }));
                rows.Add((C + "  limite " + ReflexRules.Label(s.RampLimit), () => { s.CycleRampLimit(); return true; }));
                rows.Add((C + "  chega ao limite em " + s.RampSeconds + " s <size=40><color=#999>(+" + ReflexRules.RampPerMinute(s.RampStart, s.RampLimit, s.RampSeconds).ToString("0.##") + "× por minuto)</color></size>", () => { s.CycleRampTime(); return true; }));
            }
            rows.Add((C + "Zerar placar e recomeçar", () => { Instance?.Restart(); return false; }));
        }
    }
}
