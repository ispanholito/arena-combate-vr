using System;

namespace Arena.Core
{
    // Sala de Reflexo (pedido do Pablo, 2026-11-04): treino de reação fora da demo, numa sala limpa. Totens atiram
    // orbes de magia (os do xamã) e flechas (as do arqueiro) em você; uma máquina de aparo golpeia com uma clava.
    // Você escolhe quantos totens (1 a 8) e o que cada um atira. Cada estação tem a própria velocidade (×). O modo
    // progressivo começa lento e acelera até um limite no tempo escolhido; vale para as duas estações.
    // As escolhas ficam no arena_options.json (GameOptions.reflex).
    public enum TotemShot { Orbe, Flecha, Misto }

    [Serializable]
    public sealed class ReflexSettings
    {
        public const int MaxTotems = 8;

        public bool projectiles = true, machine = true;
        public int totems = 4;
        public int[] shots = { 2, 2, 2, 2, 2, 2, 2, 2 };   // TotemShot de cada totem (2 = os dois)
        public int projectileSpeed = 2;   // índice em ReflexRules.Speeds (2 = 1×)
        public int machineSpeed = 2;
        public bool ramp;
        public int rampStart = 0;         // índice em ReflexRules.RampStarts
        public int rampLimit = 2;         // índice em ReflexRules.RampLimits
        public int rampTime = 1;          // índice em ReflexRules.RampTimes

        public int TotemCount => totems < 1 ? 1 : totems > MaxTotems ? MaxTotems : totems;
        public void CycleTotems() => totems = TotemCount % MaxTotems + 1;

        public TotemShot ShotOf(int i)
        {
            if (shots == null || i < 0 || i >= shots.Length) return TotemShot.Misto;
            int v = shots[i];
            return v >= 0 && v <= 2 ? (TotemShot)v : TotemShot.Misto;
        }

        public void CycleShot(int i)
        {
            if (shots == null || shots.Length < MaxTotems)
            {
                var n = new int[MaxTotems];
                for (int k = 0; k < MaxTotems; k++) n[k] = shots != null && k < shots.Length ? shots[k] : 2;
                shots = n;
            }
            if (i >= 0 && i < MaxTotems) shots[i] = ((int)ShotOf(i) + 1) % 3;
        }

        // Todos os totens com o mesmo tipo (atalho do painel).
        public void SetAllShots(TotemShot s) { shots = new int[MaxTotems]; for (int k = 0; k < MaxTotems; k++) shots[k] = (int)s; }

        public float ProjectileSpeed => ReflexRules.Speeds[ReflexRules.Clamp(projectileSpeed, ReflexRules.Speeds.Length)];
        public float MachineSpeed => ReflexRules.Speeds[ReflexRules.Clamp(machineSpeed, ReflexRules.Speeds.Length)];
        public float RampStart => ReflexRules.RampStarts[ReflexRules.Clamp(rampStart, ReflexRules.RampStarts.Length)];
        public float RampLimit => ReflexRules.RampLimits[ReflexRules.Clamp(rampLimit, ReflexRules.RampLimits.Length)];
        public float RampSeconds => ReflexRules.RampTimes[ReflexRules.Clamp(rampTime, ReflexRules.RampTimes.Length)];

        public void CycleProjectileSpeed() => projectileSpeed = (ReflexRules.Clamp(projectileSpeed, ReflexRules.Speeds.Length) + 1) % ReflexRules.Speeds.Length;
        public void CycleMachineSpeed() => machineSpeed = (ReflexRules.Clamp(machineSpeed, ReflexRules.Speeds.Length) + 1) % ReflexRules.Speeds.Length;
        public void CycleRampStart() => rampStart = (ReflexRules.Clamp(rampStart, ReflexRules.RampStarts.Length) + 1) % ReflexRules.RampStarts.Length;
        public void CycleRampLimit() => rampLimit = (ReflexRules.Clamp(rampLimit, ReflexRules.RampLimits.Length) + 1) % ReflexRules.RampLimits.Length;
        public void CycleRampTime() => rampTime = (ReflexRules.Clamp(rampTime, ReflexRules.RampTimes.Length) + 1) % ReflexRules.RampTimes.Length;

        // Velocidade (×) de cada estação agora. Progressivo: as duas seguem a rampa; senão, cada uma a sua.
        public float ProjectileNow(float elapsed) => ramp ? ReflexRules.Ramp(RampStart, RampLimit, RampSeconds, elapsed) : ProjectileSpeed;
        public float MachineNow(float elapsed) => ramp ? ReflexRules.Ramp(RampStart, RampLimit, RampSeconds, elapsed) : MachineSpeed;
    }

    public enum ReflexOutcome { Rebateu, Bloqueou, Desviou, Acertou, Aparou }

    public static class ReflexRules
    {
        public static readonly float[] Speeds = { 0.5f, 0.75f, 1f, 1.25f, 1.5f, 2f, 2.5f, 3f };
        public static readonly float[] RampStarts = { 0.5f, 0.75f, 1f };
        public static readonly float[] RampLimits = { 1.5f, 2f, 2.5f, 3f };
        public static readonly float[] RampTimes = { 30f, 60f, 120f, 180f, 300f };

        public static int Clamp(int i, int n) => i < 0 ? 0 : i >= n ? n - 1 : i;

        public static string ShotLabel(TotemShot s) => s == TotemShot.Orbe ? "orbe" : s == TotemShot.Flecha ? "flecha" : "orbe e flecha";

        // Rampa linear de start até limit em seconds; depois fica no limite.
        public static float Ramp(float start, float limit, float seconds, float elapsed)
        {
            if (seconds <= 0f || elapsed >= seconds) return limit;
            if (elapsed <= 0f) return start;
            return start + (limit - start) * (elapsed / seconds);
        }

        // Aceleração da rampa em "× por minuto" (para mostrar no painel).
        public static float RampPerMinute(float start, float limit, float seconds) => seconds <= 0f ? 0f : (limit - start) / seconds * 60f;

        // ---------- Totens ----------
        // Em arco na frente do jogador, a 9 m. Um só fica a 25° (não atrás da máquina de aparo); mais totens abrem o
        // arco 30° por totem, até 150°.
        public const float TotemRadius = 9f;
        public static float TotemAngle(int i, int n)
        {
            if (n <= 1) return 25f;
            float spread = Math.Min(150f, 30f * (n - 1));
            return -spread / 2f + spread * i / (n - 1);
        }

        // Cada totem atira sozinho: em 1×, um disparo a cada 5 s por totem (4 totens ≈ 1 disparo por segundo), aviso
        // de 0,6 s, orbe na velocidade do xamã e flecha na do arqueiro.
        public const float BaseShotInterval = 5f, MinShotInterval = 0.8f;
        public const float BaseTelegraph = 0.6f, MinTelegraph = 0.15f;
        public static float ShotInterval(float speed) => Math.Max(MinShotInterval, BaseShotInterval / Math.Max(0.1f, speed));
        public static float Telegraph(float speed) => Math.Max(MinTelegraph, BaseTelegraph / Math.Max(0.1f, speed));
        public static float OrbSpeed(float speed) => MagicOrbRules.Speed * speed;
        // Flecha tem gravidade: abaixo de ~10,5 m/s não alcança os 9 m do totem (alcance máximo v²/g).
        public const float MinArrowSpeed = 10.5f;
        public static float ArrowSpeed(float speed) => Math.Max(MinArrowSpeed, ArrowRules.Speed * speed);

        // ---------- Máquina de aparo ----------
        // Em 1×: ergue a clava em 0,9 s (o aviso), desce em 0,32 s e espera 1,1 s até o próximo golpe.
        public const float BaseWindup = 0.9f, MinWindup = 0.2f;
        public const float BaseSwing = 0.32f, MinSwing = 0.08f;
        public const float BasePause = 1.1f, MinPause = 0.3f;
        public static float Windup(float speed) => Math.Max(MinWindup, BaseWindup / Math.Max(0.1f, speed));
        public static float Swing(float speed) => Math.Max(MinSwing, BaseSwing / Math.Max(0.1f, speed));
        public static float Pause(float speed) => Math.Max(MinPause, BasePause / Math.Max(0.1f, speed));

        public static string Label(float speed) => speed.ToString("0.##", System.Globalization.CultureInfo.GetCultureInfo("pt-BR")) + "×";
    }

    // Placar de uma estação: contagem por resultado, sequência atual e melhor sequência (sem tomar um acerto).
    public sealed class ReflexScore
    {
        readonly int[] counts = new int[5];
        public int Streak { get; private set; }
        public int BestStreak { get; private set; }

        public int Count(ReflexOutcome o) => counts[(int)o];
        public int Total { get { int t = 0; foreach (var c in counts) t += c; return t; } }

        public void Add(ReflexOutcome o)
        {
            counts[(int)o]++;
            if (o == ReflexOutcome.Acertou) Streak = 0;
            else { Streak++; if (Streak > BestStreak) BestStreak = Streak; }
        }

        public void Reset() { Array.Clear(counts, 0, counts.Length); Streak = 0; BestStreak = 0; }
    }
}
