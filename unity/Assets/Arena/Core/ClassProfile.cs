namespace Arena.Core
{
    public enum ClassId { Lamina, Elementalista }

    // Atributos automáticos: crescem com o nível conforme o perfil da classe (sem distribuir pontos).
    public sealed class ClassProfile
    {
        public ClassId Id { get; }
        public string Name { get; }
        public float StrengthBase { get; }
        public float StrengthPerLevel { get; }
        public float HpPerLevel { get; }
        public float StaminaPerLevel { get; }
        public float ManaPerLevel { get; }

        public const float BaseHp = 100f, BaseStamina = 100f, BaseMana = 100f;

        public ClassProfile(ClassId id, string name, float strBase, float strPerLevel, float hpPerLevel, float staPerLevel, float manaPerLevel)
        {
            Id = id; Name = name;
            StrengthBase = strBase; StrengthPerLevel = strPerLevel;
            HpPerLevel = hpPerLevel; StaminaPerLevel = staPerLevel; ManaPerLevel = manaPerLevel;
        }

        public float Strength(int level) => StrengthBase + level * StrengthPerLevel;
        public float MaxHp(int level) => BaseHp + (level - 1) * HpPerLevel;
        public float MaxStamina(int level) => BaseStamina + (level - 1) * StaminaPerLevel;
        public float MaxMana(int level) => BaseMana + (level - 1) * ManaPerLevel;

        // Números do protótipo web (v0.3): Lâmina ganha muita força, Elementalista pouca e mais mana.
        public static readonly ClassProfile Lamina = new ClassProfile(ClassId.Lamina, "Lâmina", 3f, 1.2f, 14f, 10f, 4f);
        public static readonly ClassProfile Elementalista = new ClassProfile(ClassId.Elementalista, "Elementalista", 2f, 0.5f, 10f, 6f, 10f);

        public static ClassProfile Get(ClassId id) => id == ClassId.Lamina ? Lamina : Elementalista;
    }
}
