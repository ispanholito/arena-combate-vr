namespace Arena.Core
{
    // Armas de fogo de fantasia (pólvora negra): pistola de pederneira e bacamarte. Gatilho atira (no lugar da
    // habilidade do gatilho enquanto a arma está na mão); recarregar = apontar para cima e dar um tranco (VR) ou
    // esperar (desktop). Também batem como maça (coronhada). Qualquer classe usa sem penalidade.
    public sealed class GunSpec
    {
        public int Shots, Pellets;
        public float Damage, Spread, Range, Cooldown, Knock, Posture;
        public float BaseMultiplier;   // DamageMultiplier da arma base: raridade/ferreiro aumentam o tiro na mesma proporção
    }

    public static class GunRules
    {
        public const float FlickSpeed = 2.6f;      // m/s da ponta para cima
        public const float FlickUpDot = 0.55f;     // cano apontando para cima (~57°)
        public const float ReloadSeconds = 0.45f;  // depois do tranco
        public const float DesktopReload = 1.3f;   // desktop: recarrega sozinho quando esvazia
        public const float GunPitch = -12f;        // inclinação na mão (a espada usa -40°: com 0 o cano aponta como o controle)
        // Sem recarga (Pablo, 2026-10-30: apontar para cima "tira a dinâmica"). Munição infinita; o limite é a cadência.
        public const bool NeedsReload = false;

        // Sem recarga a cadência é o limite: ~25-30 de dano/s, menos que a espada (~45), porque atira de longe sem risco.
        public static readonly GunSpec Pistola = new GunSpec
        { Shots = 3, Pellets = 1, Damage = 24f, Spread = 0.6f, Range = 30f, Cooldown = 0.8f, Knock = 2.5f, Posture = 0.9f, BaseMultiplier = 0.6f };
        public static readonly GunSpec Bacamarte = new GunSpec
        { Shots = 2, Pellets = 7, Damage = 8f, Spread = 8f, Range = 14f, Cooldown = 1.4f, Knock = 6f, Posture = 0.6f, BaseMultiplier = 0.8f };

        public static readonly GunSpec Repeticao = new GunSpec
        { Shots = 8, Pellets = 1, Damage = 12f, Spread = 1.2f, Range = 28f, Cooldown = 0.35f, Knock = 1.5f, Posture = 0.7f, BaseMultiplier = 0.65f };

        public static GunSpec For(WeaponLook look) => look switch
        {
            WeaponLook.Pistola => Pistola,
            WeaponLook.Bacamarte => Bacamarte,
            WeaponLook.PistolaRepeticao => Repeticao,
            _ => null,
        };

        public static float ShotDamage(GunSpec s, WeaponDef w) => s.Damage * (w.DamageMultiplier / s.BaseMultiplier);

        // Tranco para recarregar: ponta subindo rápido com o cano apontando para cima.
        public static bool IsReloadFlick(float tipUpSpeed, float barrelUpDot) => tipUpSpeed >= FlickSpeed && barrelUpDot >= FlickUpDot;
    }
}
