using System;
using System.Collections.Generic;

namespace Arena.Core
{
    public enum Rarity { Comum, Incomum, Raro, Epico, Lendario }

    public enum AffixKind
    {
        Dano,         // +% dano
        Postura,      // +% dano de postura
        Leveza,       // -% peso (a arma segue melhor a mão)
        Folego,       // -% estamina por golpe
        RouboDeVida,  // % do dano vira vida
        Execucao,     // +% dano em inimigos com menos de 30% de vida
    }

    public readonly struct Affix
    {
        public readonly AffixKind Kind;
        public readonly float Value;   // fração (0,12 = 12%)
        public Affix(AffixKind kind, float value) { Kind = kind; Value = value; }

        public string Label => Kind switch
        {
            AffixKind.Dano => $"+{Pct(Value)}% dano",
            AffixKind.Postura => $"+{Pct(Value)}% postura",
            AffixKind.Leveza => $"-{Pct(Value)}% peso",
            AffixKind.Folego => $"-{Pct(Value)}% estamina",
            AffixKind.RouboDeVida => $"{Pct(Value)}% roubo de vida",
            _ => $"+{Pct(Value)}% execução",
        };

        static int Pct(float v) => (int)Math.Round(v * 100f);
    }

    // Uma arma concreta: base (WeaponDef) + raridade + atributos aleatórios + nível do ferreiro (+0..+5).
    public sealed class ItemInstance
    {
        public const int MaxUpgrade = 5;
        public const float UpgradeDamage = 0.06f;     // +6% de dano por nível do ferreiro
        public const float OffClassPenalty = 0.85f;   // arma fora da proficiência da classe: -15% de dano
        public const float ExecuteThreshold = 0.3f;

        public WeaponDef Base { get; }
        public Rarity Rarity { get; }
        public IReadOnlyList<Affix> Affixes => affixes;
        public int Upgrade { get; private set; }
        public WeaponPassive Passive { get; }   // só Rara ou melhor
        public WeaponAbilityDef Ability => WeaponAbilities.For(Base.Kind);
        readonly List<Affix> affixes;

        public ItemInstance(WeaponDef baseDef, Rarity rarity = Rarity.Comum, IEnumerable<Affix> affixes = null, int upgrade = 0,
                            WeaponPassive passive = WeaponPassive.Nenhuma)
        {
            Base = baseDef ?? throw new ArgumentNullException(nameof(baseDef));
            Rarity = rarity;
            this.affixes = affixes != null ? new List<Affix>(affixes) : new List<Affix>();
            Upgrade = Math.Clamp(upgrade, 0, MaxUpgrade);
            Passive = rarity >= Rarity.Raro ? passive : WeaponPassive.Nenhuma;
        }

        public static ItemInstance Basic(WeaponDef def) => new ItemInstance(def);

        // Raridade dá um pouco de dano por si só (o resto vem dos atributos).
        public static float RarityDamage(Rarity r) => 1f + 0.05f * (int)r;
        public static int MaxAffixes(Rarity r) => r switch
        {
            Rarity.Comum => 0, Rarity.Incomum => 1, Rarity.Raro => 2, _ => 3,
        };

        public float Sum(AffixKind k)
        {
            float s = 0f;
            foreach (var a in affixes) if (a.Kind == k) s += a.Value;
            return s;
        }

        public float DamageMultiplier => RarityDamage(Rarity) * (1f + Sum(AffixKind.Dano)) * (1f + UpgradeDamage * Upgrade);

        public string DisplayName
        {
            get
            {
                string n = Rarity switch
                {
                    Rarity.Incomum => Base.Name + " Fina",
                    Rarity.Raro => Base.Name + " Rara",
                    Rarity.Epico => Base.Name + " Épica",
                    Rarity.Lendario => Base.Look == WeaponLook.Padrao ? WeaponAbilities.LegendaryName(Base.Kind) : Base.Name,
                    _ => Base.Name,
                };
                return Upgrade > 0 ? $"{n} +{Upgrade}" : n;
            }
        }

        public static string RarityName(Rarity r) => r switch
        {
            Rarity.Incomum => "Incomum", Rarity.Raro => "Raro", Rarity.Epico => "Épico", Rarity.Lendario => "Lendário", _ => "Comum",
        };

        // Cor da raridade (r, g, b) 0..1.
        public static (float r, float g, float b) RarityColor(Rarity r) => r switch
        {
            Rarity.Incomum => (0.35f, 0.9f, 0.35f),
            Rarity.Raro => (0.3f, 0.55f, 1f),
            Rarity.Epico => (0.75f, 0.35f, 1f),
            Rarity.Lendario => (1f, 0.6f, 0.15f),
            _ => (0.85f, 0.85f, 0.85f),
        };

        // A arma efetiva para a classe: o combate inteiro usa este WeaponDef (dano, peso, estamina, postura...).
        public WeaponDef Resolve(ClassId cls)
        {
            bool prof = Proficiency.IsProficient(cls, Base.Kind) || Base.IsGun;   // arma de fogo: qualquer classe
            float dmg = Base.DamageMultiplier * DamageMultiplier * (prof ? 1f : OffClassPenalty);
            float weight = Base.Weight * Math.Max(0.4f, 1f - Sum(AffixKind.Leveza));
            float sta = Base.StaminaPerHit * Math.Max(0.4f, 1f - Sum(AffixKind.Folego));
            return Base.With(DisplayName, weight, dmg, sta, 1f + Sum(AffixKind.Postura), Sum(AffixKind.RouboDeVida), Sum(AffixKind.Execucao), prof);
        }

        // ---------- Ferreiro ----------
        public bool CanUpgrade => Upgrade < MaxUpgrade;
        public int UpgradeCost => Smithy.Cost(Rarity, Upgrade);
        public void SetUpgrade(int u) => Upgrade = Math.Clamp(u, 0, MaxUpgrade);
    }

    // Mercador do santuário: compra armas por ouro (para gastar no ferreiro). Raridade pesa muito; o ferreiro devolve
    // só uma parte do que custou.
    public static class Merchant
    {
        // Preço de VENDA ao mercador (o que ele paga): 25% do preço da loja, como no Blade & Sorcery.
        public static int Price(ItemInstance item)
        {
            int r = (int)item.Rarity + 1;
            int spent = 0;
            for (int u = 0; u < item.Upgrade; u++) spent += Smithy.Cost(item.Rarity, u);
            return 10 * r * r + item.Affixes.Count * 5 + spent / 3;
        }

        public const float BuyBackFraction = 0.25f;
        // Preço de COMPRA na loja: 4× o que ele paga (ele compra a 25%).
        public static int ShopPrice(ItemInstance item) => (int)Math.Round(Price(item) / BuyBackFraction);
        public const int PotionPrice = 30;
    }

    // Banca do mercador no santuário: 3 armas e algumas poções. O estoque renova toda vez que você volta ao santuário.
    public sealed class Shop
    {
        public const int StockSize = 3, PotionStock = 3;
        public readonly List<ItemInstance> Stock = new List<ItemInstance>();
        public int PotionsLeft { get; private set; }

        public void Restock(LootTable loot, int tier = 1)
        {
            Stock.Clear();
            for (int i = 0; i < StockSize; i++) Stock.Add(loot.RollItem(Rarity.Comum, tier));
            PotionsLeft = PotionStock;
        }

        // Compra a arma do estoque (o runtime coloca na mão / 2ª vaga). Falha sem ouro ou se já foi vendida.
        public bool TryBuy(ItemInstance item, PlayerState ps)
        {
            if (item == null || !Stock.Contains(item)) return false;
            int price = Merchant.ShopPrice(item);
            if (ps.Gold < price) return false;
            ps.SpendGold(price);
            Stock.Remove(item);
            return true;
        }

        public bool TryBuyPotion(PlayerState ps)
        {
            if (PotionsLeft <= 0 || ps.Potions >= ps.PotionCap || ps.Gold < Merchant.PotionPrice) return false;
            ps.SpendGold(Merchant.PotionPrice);
            ps.AddPotion();
            PotionsLeft--;
            return true;
        }
    }

    public static class Smithy
    {
        public static int Cost(Rarity r, int currentUpgrade) => 40 * (currentUpgrade + 1) * ((int)r + 1);

        // Gasta o ouro e melhora. Retorna false se não pode (máximo ou sem ouro).
        public static bool TryUpgrade(ItemInstance item, PlayerState ps)
        {
            if (item == null || !item.CanUpgrade) return false;
            int cost = item.UpgradeCost;
            if (ps.Gold < cost) return false;
            ps.SpendGold(cost);
            item.SetUpgrade(item.Upgrade + 1);
            return true;
        }
    }

    // Qualquer classe usa qualquer arma; a proficiência libera os bônus da árvore e evita a penalidade de dano.
    public static class Proficiency
    {
        public static bool IsProficient(ClassId cls, WeaponKind kind) => cls switch
        {
            ClassId.Lamina => kind == WeaponKind.Espada || kind == WeaponKind.Machado || kind == WeaponKind.Maca
                              || kind == WeaponKind.Lanca || kind == WeaponKind.Adaga,
            ClassId.Elementalista => kind == WeaponKind.Cajado || kind == WeaponKind.Adaga,
            _ => false,
        };

        // As magias do Elementalista saem do cristal: precisam do cajado na mão.
        public static bool CanCastSpells(ClassId cls, WeaponKind kind) => cls != ClassId.Elementalista || kind == WeaponKind.Cajado;
    }

    // O que cai dos inimigos: ouro sempre, arma às vezes (raridade sorteada por peso).
    public sealed class LootTable
    {
        public static readonly int[] RarityWeights = { 60, 25, 10, 4, 1 };   // Comum..Lendário

        readonly Random rng;
        public LootTable(int seed = 0) { rng = seed == 0 ? new Random() : new Random(seed); }

        // Armas só saem de baús (e do mímico, que é um baú) e do chefe — pedido do Pablo: inimigo comum deixa só ouro,
        // para a dungeon não ficar cheia de armas no chão. (Os valores antigos ficam aqui para quando voltar a ter drop.)
        public static float DropChance(EnemyKind k) => k switch
        {
            EnemyKind.Mimico => 1f,
            EnemyKind.ReiGoblin or EnemyKind.RainhaSlime or EnemyKind.ChefeOrc or EnemyKind.Lich => 1f,
            _ => 0f,   // antes: slime 8%, goblin 22%, esqueleto 28%, arqueiro 20%, orc 35%
        };

        public static int BaseGold(EnemyKind k) => k switch
        {
            EnemyKind.Slime => 2,
            EnemyKind.Goblin => 6,
            EnemyKind.Esqueleto => 7,
            EnemyKind.Arqueiro => 6,
            EnemyKind.Xama => 8,
            EnemyKind.Mimico => 25,
            EnemyKind.Orc => 14,
            EnemyKind.ReiGoblin or EnemyKind.RainhaSlime or EnemyKind.ChefeOrc or EnemyKind.Lich => 60,
            _ => 0,
        };

        public static Rarity MinRarity(EnemyKind k) => EnemyDef.IsBoss(k) ? Rarity.Raro : k == EnemyKind.Mimico ? Rarity.Incomum : Rarity.Comum;

        // Ouro com variação de ±30%.
        public int RollGold(EnemyKind k, int tier = 1)
        {
            int g = BaseGold(k);
            if (g == 0) return 0;
            return Math.Max(1, (int)Math.Round(g * ArcadeRules.GoldMultiplier(tier) * (0.7 + rng.NextDouble() * 0.6)));
        }

        public Rarity RollRarity(Rarity min = Rarity.Comum, int tier = 1)
        {
            if (tier > 1)   // modo arcade: profundidades maiores favorecem as raras
            {
                var w = ArcadeRules.RarityWeights(tier);
                double tot = 0;
                for (int i = (int)min; i < w.Length; i++) tot += w[i];
                double x = rng.NextDouble() * tot;
                for (int i = (int)min; i < w.Length; i++) { x -= w[i]; if (x < 0) return (Rarity)i; }
                return Rarity.Lendario;
            }
            int total = 0;
            for (int i = (int)min; i < RarityWeights.Length; i++) total += RarityWeights[i];
            int r = rng.Next(total);
            for (int i = (int)min; i < RarityWeights.Length; i++)
            {
                r -= RarityWeights[i];
                if (r < 0) return (Rarity)i;
            }
            return Rarity.Lendario;
        }

        public ItemInstance RollItem(Rarity min = Rarity.Comum, int tier = 1)
        {
            var def = WeaponDef.All[rng.Next(WeaponDef.All.Length)];
            var rarity = RollRarity(min, tier);
            int count = rarity == Rarity.Comum ? 0 : rng.Next(Math.Max(1, ItemInstance.MaxAffixes(rarity) - 1), ItemInstance.MaxAffixes(rarity) + 1);
            var kinds = new List<AffixKind>((AffixKind[])Enum.GetValues(typeof(AffixKind)));
            var affixes = new List<Affix>();
            for (int i = 0; i < count && kinds.Count > 0; i++)
            {
                int j = rng.Next(kinds.Count);
                var k = kinds[j];
                kinds.RemoveAt(j);
                affixes.Add(new Affix(k, AffixValue(k, rarity, rng.NextDouble())));
            }
            var passive = rarity >= Rarity.Raro ? (WeaponPassive)rng.Next(1, Enum.GetValues(typeof(WeaponPassive)).Length) : WeaponPassive.Nenhuma;
            return new ItemInstance(def, rarity, affixes, 0, passive);
        }

        // Valor do atributo cresce com a raridade; t 0..1 = sorte.
        public static float AffixValue(AffixKind k, Rarity r, double t)
        {
            float scale = 1f + 0.35f * (int)r;
            float v = k switch
            {
                AffixKind.Dano => 0.06f,
                AffixKind.Postura => 0.12f,
                AffixKind.Leveza => 0.10f,
                AffixKind.Folego => 0.10f,
                AffixKind.RouboDeVida => 0.03f,
                _ => 0.15f,
            };
            return (float)Math.Round(v * scale * (0.75 + 0.5 * t), 3);
        }

        // Baú da dungeon: ouro (mais nos andares fundos) e 60% de chance de arma Incomum ou melhor.
        public (int gold, ItemInstance item) RollChest(int floor, int tier = 1)
        {
            int gold = (int)Math.Round((15 + 10 * floor) * ArcadeRules.GoldMultiplier(tier) * (0.8 + rng.NextDouble() * 0.4));
            var item = rng.NextDouble() < 0.6 ? RollItem(Rarity.Incomum, tier) : null;
            return (gold, item);
        }

        public const double ChestPotionChance = 0.35;
        public bool RollChestPotion() => rng.NextDouble() < ChestPotionChance;

        // Rola o loot de um inimigo morto. Retorna null se não caiu arma.
        public ItemInstance RollDrop(EnemyKind k, int tier = 1)
        {
            if (rng.NextDouble() >= DropChance(k)) return null;
            return RollItem(MinRarity(k), tier);
        }
    }
}
