using System.Collections.Generic;
using Arena.Core;
using UnityEngine;

namespace Arena
{
    // Sala de Reflexo (pedido do Pablo, 2026-11-04). Roda dentro do Treino (sem dano, estamina infinita), mas numa
    // SALA LIMPA própria (piso, paredes e luz; a arena e os barris somem). Portal REFLEXO no santuário. Você entra com
    // DUAS ESPADAS e fica no círculo dourado:
    //   - 1 a 8 totens em arco atiram orbes de magia (verdes, os do xamã) e/ou flechas (as do arqueiro), cada um com o
    //     seu tipo. Cada totem atira sozinho. O cristal acende antes (verde = orbe, amarelo = flecha). Rebata com a
    //     arma, bloqueie com o escudo ou desvie.
    //   - Máquina de aparo bem na frente: ergue a clava (brilho laranja = aviso) e golpeia de cima, da esquerda ou da
    //     direita. Arma rápida contra a clava = APARO; parada = bloqueio; corpo = acerto.
    //   - Progressivo: as duas estações começam lentas e aceleram até o limite no tempo escolhido.
    // Ajustes: pausa → "Ajustes da Sala de Reflexo" abre o painel (OpenPanel). O placar fica em cima da máquina.
    public sealed class ReflexRoom : MonoBehaviour
    {
        public static ReflexRoom Instance { get; private set; }

        static readonly Vector3 Stand = new Vector3(0f, 0f, -3f);
        static readonly Vector3 Facing = Vector3.forward;
        const float MachineDistance = 1.3f, PivotHeight = 1.6f;
        const float ArmIn = 0.7f, ArmOut = 1.45f;   // trecho da clava ao longo do braço (m a partir do pivô)
        // Sala limpa: retângulo em volta do círculo, com folga para os totens do arco mais aberto.
        const float RoomX0 = -11f, RoomX1 = 11f, RoomZ0 = -8f, RoomZ1 = 8f, WallH = 4f;
        static readonly Color OrbCol = new Color(0.55f, 1f, 0.35f), ArrowCol = new Color(1f, 0.85f, 0.3f), Warn = new Color(1f, 0.5f, 0.1f);

        public readonly ReflexScore MachineScore = new ReflexScore(), ProjectileScore = new ReflexScore();
        Transform root, machine, pivot, totemRoot;
        Material clubMat, stone;
        TextMesh board;
        readonly List<GameObject> shots = new List<GameObject>();
        float elapsed;
        bool lastRamp;

        static ReflexSettings S => SaveSystem.Options.reflex;
        ArenaPlayer Player => ArenaGame.Instance.Player;

        void Awake()
        {
            Instance = this;
            var game = ArenaGame.Instance;
            root = new GameObject("SalaDeReflexo").transform;
            // Sala limpa: some a arena (e a arte do Treino, que é filha dela), o boneco e os barris.
            foreach (var e in ArenaEnemy.All.ToArray()) if (e) Destroy(e.gameObject);
            Breakable.ClearAll();
            if (game.arenaWorld) game.arenaWorld.gameObject.SetActive(false);
            ArenaUtil.Area = new WalkArea(new[] { new Rect2(RoomX0 + 0.4f, RoomZ0 + 0.4f, RoomX1 - 0.4f, RoomZ1 - 0.4f) });
            BuildRoom();
            ArenaUtil.Prim(PrimitiveType.Cylinder, root, Stand + Vector3.up * 0.02f, new Vector3(1.1f, 0.02f, 1.1f), ArenaUtil.Unlit(new Color(1f, 0.85f, 0.4f, 0.35f)));
            BuildMachine();
            board = ArenaFx.MakeText(root, "", 0.011f, Color.white);
            board.transform.position = Stand + Facing * (MachineDistance + 1.2f) + Vector3.up * 2.6f;
            board.transform.rotation = Quaternion.LookRotation(Facing);
            lastRamp = S.ramp;
            GiveTwoSwords();
            game.Player.TeleportTo(Stand, Stand + Facing * 5f);
            game.Message("SALA DE REFLEXO: fique no círculo. Pausa → Ajustes da Sala de Reflexo.", 4.5f);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            PanelOpen = false;
            if (root) Destroy(root.gameObject);
            foreach (var s in shots) if (s) Destroy(s);
            // A arena e a área voltam no próximo StartMode (LeaveDungeon).
        }

        // Duas espadas nas mãos; o escudo vai para o ombro esquerdo (dá para trocar pelos coldres).
        void GiveTwoSwords()
        {
            var st = Player.State;
            if (st.Loadout[1] != null && st.Loadout[1].Base.IsShield && st.Holsters[0] == null) st.Holsters[0] = st.Loadout[1];
            st.Loadout[0] = new ItemInstance(WeaponDef.EspadaLonga);
            st.Loadout[1] = new ItemInstance(WeaponDef.EspadaLonga);
            Player.EquipActive();
        }

        void BuildRoom()
        {
            stone = ArenaUtil.Lit(new Color(0.36f, 0.36f, 0.4f));
            var wall = ArenaUtil.Lit(new Color(0.26f, 0.26f, 0.3f));
            var line = ArenaUtil.Unlit(new Color(0.5f, 0.5f, 0.56f));
            float cx = (RoomX0 + RoomX1) / 2f, cz = (RoomZ0 + RoomZ1) / 2f, w = RoomX1 - RoomX0, d = RoomZ1 - RoomZ0;
            ArenaUtil.Prim(PrimitiveType.Cube, root, new Vector3(cx, -0.05f, cz), new Vector3(w, 0.1f, d), stone, keepCollider: true);
            // Grade leve no piso (a cada 2 m) para ter noção de distância.
            for (float x = RoomX0 + 2f; x < RoomX1; x += 2f) ArenaUtil.Prim(PrimitiveType.Cube, root, new Vector3(x, 0.002f, cz), new Vector3(0.02f, 0.002f, d), line);
            for (float z = RoomZ0 + 2f; z < RoomZ1; z += 2f) ArenaUtil.Prim(PrimitiveType.Cube, root, new Vector3(cx, 0.002f, z), new Vector3(w, 0.002f, 0.02f), line);
            ArenaUtil.Prim(PrimitiveType.Cube, root, new Vector3(cx, WallH / 2f, RoomZ1 + 0.1f), new Vector3(w + 0.4f, WallH, 0.2f), wall, keepCollider: true);
            ArenaUtil.Prim(PrimitiveType.Cube, root, new Vector3(cx, WallH / 2f, RoomZ0 - 0.1f), new Vector3(w + 0.4f, WallH, 0.2f), wall, keepCollider: true);
            ArenaUtil.Prim(PrimitiveType.Cube, root, new Vector3(RoomX1 + 0.1f, WallH / 2f, cz), new Vector3(0.2f, WallH, d), wall, keepCollider: true);
            ArenaUtil.Prim(PrimitiveType.Cube, root, new Vector3(RoomX0 - 0.1f, WallH / 2f, cz), new Vector3(0.2f, WallH, d), wall, keepCollider: true);
            var sun = new GameObject("LuzSala").AddComponent<Light>();
            sun.transform.SetParent(root, false);
            sun.type = LightType.Directional; sun.intensity = 0.9f; sun.color = new Color(1f, 0.96f, 0.9f);
            sun.transform.rotation = Quaternion.Euler(55f, -30f, 0f);
            var fill = new GameObject("LuzCentro").AddComponent<Light>();
            fill.transform.SetParent(root, false);
            fill.transform.position = Stand + Vector3.up * 3.5f + Facing * 2f;
            fill.type = LightType.Point; fill.range = 14f; fill.intensity = 1.2f;
        }

        // Zera o placar e recomeça o progressivo.
        public void Restart()
        {
            MachineScore.Reset(); ProjectileScore.Reset();
            elapsed = 0f; phase = MPhase.Rest; phaseT = 0f; machineWait = 1f;
            foreach (var t in totems) { t.Cd = Random.Range(0.8f, 2.5f); t.Tele = -1f; }
            foreach (var s in shots) if (s) Destroy(s);
            shots.Clear();
            ArenaGame.Instance.Message("Reflexo: placar zerado" + (S.ramp ? ", progressivo recomeçou" : ""), 2f);
        }

        void Update()
        {
            UpdatePanel();
            if (ArenaGame.Paused) return;
            float dt = Time.deltaTime;
            if (S.ramp != lastRamp) { lastRamp = S.ramp; elapsed = 0f; }
            if (S.projectiles || S.machine) elapsed += dt;
            UpdateTotems(dt);
            UpdateMachine(dt);
            shots.RemoveAll(s => !s);
            UpdateBoard();
        }

        // ---------- Totens ----------
        sealed class Totem { public Transform T; public Material Crystal; public float Cd, Tele = -1f; public bool Orb; }
        readonly List<Totem> totems = new List<Totem>();

        // Recria os totens quando a quantidade muda (posições do arco dependem do total).
        void SyncTotems()
        {
            int n = S.TotemCount;
            if (totems.Count == n) return;
            if (totemRoot) Destroy(totemRoot.gameObject);
            totems.Clear();
            totemRoot = new GameObject("Totens").transform;
            totemRoot.SetParent(root, false);
            for (int i = 0; i < n; i++) totems.Add(BuildTotem(ReflexRules.TotemAngle(i, n), i));
        }

        Totem BuildTotem(float angle, int index)
        {
            var dir = Quaternion.AngleAxis(angle, Vector3.up) * Facing;
            var t = new GameObject("Totem " + (index + 1)).transform;
            t.SetParent(totemRoot, false);
            t.position = Stand + dir * ReflexRules.TotemRadius;
            t.rotation = Quaternion.LookRotation(-dir);
            var dark = ArenaUtil.Lit(new Color(0.2f, 0.2f, 0.24f));
            ArenaUtil.Prim(PrimitiveType.Cylinder, t, new Vector3(0, 0.7f, 0), new Vector3(0.5f, 0.7f, 0.5f), dark, keepCollider: true);
            ArenaUtil.Prim(PrimitiveType.Cube, t, new Vector3(0, 1.45f, 0), new Vector3(0.6f, 0.12f, 0.6f), dark);
            var crystal = ArenaUtil.Lit(new Color(0.25f, 0.25f, 0.3f), Color.black);
            var c = ArenaUtil.Prim(PrimitiveType.Sphere, t, new Vector3(0, 1.75f, 0), Vector3.one * 0.32f, crystal);
            c.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
            var label = ArenaFx.MakeText(t, "", 0.008f, Color.white);
            label.transform.localPosition = new Vector3(0, 2.25f, 0);
            label.transform.rotation = Quaternion.LookRotation(dir);
            label.name = "Rotulo";
            return new Totem { T = t, Crystal = crystal, Cd = Random.Range(0.8f, 2.5f) + index * 0.35f };
        }

        void UpdateTotems(float dt)
        {
            SyncTotems();
            float speed = S.ProjectileNow(elapsed);
            for (int i = 0; i < totems.Count; i++)
            {
                var tm = totems[i];
                var kind = S.ShotOf(i);
                var label = tm.T.Find("Rotulo")?.GetComponent<TextMesh>();
                if (label) label.text = (i + 1) + "\n<size=40>" + ReflexRules.ShotLabel(kind) + "</size>";
                if (tm.Tele >= 0f)
                {
                    tm.Tele += dt;
                    float tele = ReflexRules.Telegraph(speed);
                    float k = Mathf.Clamp01(tm.Tele / tele);
                    tm.Crystal.SetColor("_EmissionColor", (tm.Orb ? OrbCol : ArrowCol) * (0.4f + 2.2f * k * (0.75f + 0.25f * Mathf.Sin(Time.time * 40f))));
                    if (tm.Tele >= tele) { Fire(tm.T, tm.Orb, speed); tm.Crystal.SetColor("_EmissionColor", Color.black); tm.Tele = -1f; }
                    continue;
                }
                if (!S.projectiles) continue;
                tm.Cd -= dt;
                if (tm.Cd > 0f) continue;
                tm.Cd = ReflexRules.ShotInterval(speed) * Random.Range(0.8f, 1.2f);
                tm.Orb = kind == TotemShot.Orbe || (kind == TotemShot.Misto && Random.value < 0.5f);
                tm.Tele = 0f;
                ArenaAudio.Play(Sfx.Windup, tm.T.position + Vector3.up * 1.7f, 0.5f, tm.Orb ? 0.8f : 1.3f);
            }
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
                + (s.projectiles ? "<b>TOTENS</b> " + s.TotemCount + " " + (s.ramp ? "" : ReflexRules.Label(s.ProjectileSpeed)) : "<color=#888>TOTENS desligados</color>")
                + "\n<size=40>rebatidos " + p.Count(ReflexOutcome.Rebateu) + " · bloqueados " + p.Count(ReflexOutcome.Bloqueou) + " · desviados " + p.Count(ReflexOutcome.Desviou)
                + " · <color=#ff8080>acertos " + p.Count(ReflexOutcome.Acertou) + "</color> · sequência " + p.Streak + " (melhor " + p.BestStreak + ")</size>"
                + (speedLine.Length > 0 ? "\n" + speedLine : "")
                + "\n<size=36><color=#999>Pausa → Ajustes da Sala de Reflexo</color></size>";
        }

        // ---------- Painel de ajustes (abre pela pausa, como o Livro de magias) ----------
        public static bool PanelOpen { get; private set; }
        public static int PanelClosedFrame { get; private set; } = -1;   // a pausa ignora o B do quadro em que o painel fechou
        static int panelOpenedFrame = -1;
        MenuPanel panel;
        int cursor;
        readonly List<(string label, System.Func<bool> act)> rows = new List<(string, System.Func<bool>)>();
        readonly List<string> labels = new List<string>();

        public static void OpenPanel() { PanelOpen = true; panelOpenedFrame = Time.frameCount; }

        void ClosePanel() { PanelOpen = false; PanelClosedFrame = Time.frameCount; if (panel != null) panel.Hide(); ArenaAudio.Play2D(Sfx.Click, 0.6f); }

        void BuildRows()
        {
            var s = S;
            string On(bool b) => b ? "<color=#9fe89f>ligado</color>" : "<color=#ff9090>desligado</color>";
            const string Note = " <size=40><color=#999>";
            rows.Clear();
            rows.Add(("Totens atirando: " + On(s.projectiles), () => { s.projectiles = !s.projectiles; return true; }));
            rows.Add(("Quantidade de totens: " + s.TotemCount + Note + "(1 a 8)</color></size>", () => { s.CycleTotems(); return true; }));
            for (int i = 0; i < s.TotemCount; i++)
            {
                int k = i;
                rows.Add(("   Totem " + (i + 1) + ": " + ReflexRules.ShotLabel(s.ShotOf(i)), () => { s.CycleShot(k); return true; }));
            }
            rows.Add(("   Todos: orbe", () => { s.SetAllShots(TotemShot.Orbe); return true; }));
            rows.Add(("   Todos: flecha", () => { s.SetAllShots(TotemShot.Flecha); return true; }));
            rows.Add(("   Todos: orbe e flecha", () => { s.SetAllShots(TotemShot.Misto); return true; }));
            rows.Add(("Velocidade dos totens: " + ReflexRules.Label(s.ProjectileSpeed) + (s.ramp ? Note + "(o progressivo manda)</color></size>" : ""), () => { s.CycleProjectileSpeed(); return true; }));
            rows.Add(("Máquina de aparo: " + On(s.machine), () => { s.machine = !s.machine; return true; }));
            rows.Add(("Velocidade dos golpes: " + ReflexRules.Label(s.MachineSpeed) + (s.ramp ? Note + "(o progressivo manda)</color></size>" : ""), () => { s.CycleMachineSpeed(); return true; }));
            rows.Add(("Progressivo: " + On(s.ramp), () => { s.ramp = !s.ramp; return true; }));
            if (s.ramp)
            {
                rows.Add(("   começa em " + ReflexRules.Label(s.RampStart), () => { s.CycleRampStart(); return true; }));
                rows.Add(("   limite " + ReflexRules.Label(s.RampLimit), () => { s.CycleRampLimit(); return true; }));
                rows.Add(("   chega ao limite em " + s.RampSeconds + " s" + Note + "(+" + ReflexRules.RampPerMinute(s.RampStart, s.RampLimit, s.RampSeconds).ToString("0.##") + "× por minuto)</color></size>", () => { s.CycleRampTime(); return true; }));
            }
            rows.Add(("Zerar placar e recomeçar", () => { Restart(); return false; }));
            rows.Add(("<color=#ffd36b>Voltar</color>", () => { ClosePanel(); return false; }));
        }

        void UpdatePanel()
        {
            if (panel == null) panel = new MenuPanel("AjustesReflexo", 1.05f, 1.0f, new Color(0.04f, 0.05f, 0.08f, 0.94f));
            if (PanelOpen && !ArenaGame.Paused) PanelOpen = false;
            if (!PanelOpen) { panel.Hide(); return; }
            panel.Show(Player);
            BuildRows();
            var input = Player.Input;
            if (Time.frameCount != panelOpenedFrame)   // o A/pinça que abriu não conta aqui
            {
                int n = rows.Count;
                if (input.MenuUp || input.MenuDown) ArenaAudio.Play2D(Sfx.Click, 0.5f);
                if (input.MenuUp) cursor = (cursor + n - 1) % n;
                if (input.MenuDown) cursor = (cursor + 1) % n;
                if (panel.Hover >= 0) cursor = panel.Hover;
                cursor = Mathf.Clamp(cursor, 0, n - 1);
                if (input.MenuRespec) { ClosePanel(); return; }
                int click = panel.TakeClick();
                if (click >= 0) cursor = click;
                if (click >= 0 || input.MenuConfirm)
                {
                    if (click < 0) ArenaAudio.Play2D(Sfx.Click, 0.8f, 1.3f);
                    if (rows[cursor].act()) SaveSystem.SaveOptions();
                    if (!PanelOpen) return;
                    BuildRows();
                }
            }
            labels.Clear();
            foreach (var r in rows) labels.Add(r.label);
            cursor = Mathf.Clamp(cursor, 0, labels.Count - 1);
            panel.Render("<b>SALA DE REFLEXO</b>", labels, cursor,
                "<size=40><color=#999>Aponte e faça a pinça, ou cima/baixo + A · B volta para a pausa</color></size>");
        }
    }
}
