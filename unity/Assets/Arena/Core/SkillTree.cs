using System;
using System.Collections.Generic;
using System.Linq;

namespace Arena.Core
{
    public enum ResourceKind { Stamina, Mana }

    public sealed class SkillNode
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public int MinLevel { get; }
        public IReadOnlyList<string> Requires { get; }
        public bool IsActive { get; }          // ativa (tem botão) ou passiva
        public float Cooldown { get; }
        public float Cost { get; }
        public ResourceKind Resource { get; }

        public SkillNode(string id, string name, string description, int minLevel, string[] requires,
                         bool isActive = false, float cooldown = 0f, float cost = 0f, ResourceKind resource = ResourceKind.Stamina)
        {
            Id = id; Name = name; Description = description; MinLevel = minLevel;
            Requires = requires ?? Array.Empty<string>();
            IsActive = isActive; Cooldown = cooldown; Cost = cost; Resource = resource;
        }
    }

    // Árvore: 1 ponto por nível, pré-requisitos e nível mínimo. Respec grátis (na fogueira).
    public sealed class SkillTree
    {
        readonly Dictionary<string, SkillNode> nodes;
        readonly HashSet<string> unlocked = new HashSet<string>();
        readonly HashSet<string> granted = new HashSet<string>();   // de graça (habilidade inicial): respec não devolve ponto
        readonly Progression progression;

        public IReadOnlyCollection<SkillNode> Nodes => nodes.Values;
        public IReadOnlyCollection<string> Unlocked => unlocked;
        public event Action<string> Changed;

        public SkillTree(IEnumerable<SkillNode> nodes, Progression progression)
        {
            this.nodes = nodes.ToDictionary(n => n.Id);
            this.progression = progression;
        }

        public SkillNode Get(string id) => nodes.TryGetValue(id, out var n) ? n : null;
        public bool Has(string id) => unlocked.Contains(id);

        public bool CanUnlock(string id)
        {
            var n = Get(id);
            return n != null && !unlocked.Contains(id) && progression.UnspentPoints > 0
                   && progression.Level >= n.MinLevel && n.Requires.All(unlocked.Contains);
        }

        public bool Unlock(string id)
        {
            if (!CanUnlock(id) || !progression.TrySpendPoint()) return false;
            unlocked.Add(id);
            Changed?.Invoke(id);
            return true;
        }

        public void Grant(string id)
        {
            if (Get(id) == null) return;
            unlocked.Add(id);
            granted.Add(id);
            Changed?.Invoke(id);
        }

        public int SpentPoints => unlocked.Count(id => !granted.Contains(id));

        // Devolve todos os pontos gastos; as habilidades iniciais continuam.
        public int Respec()
        {
            int count = SpentPoints;
            unlocked.RemoveWhere(id => !granted.Contains(id));
            progression.RefundPoints(count);
            Changed?.Invoke(null);
            return count;
        }

        public static IEnumerable<SkillNode> For(ClassId cls) => cls == ClassId.Lamina ? SkillIds.LaminaTree : SkillIds.ElementalistaTree;
        public static string StartingSkill(ClassId cls) => cls == ClassId.Lamina ? SkillIds.Investida : SkillIds.BolaFogo;
    }

    public static class SkillIds
    {
        // Lâmina
        public const string Investida = "investida", Contra = "contra", VentoLamina = "ventolam";
        public const string FioAfiado = "afiado", PassoLeve = "leve", InvestidaCadeia = "cadeia";
        // Elementalista
        public const string BolaFogo = "fogo", LancaGelo = "gelo", Raizes = "raizes";
        public const string Combustao = "combustao", FonteArcana = "fonte", GeloProfundo = "profundo";

        public static readonly SkillNode[] LaminaTree =
        {
            new SkillNode(Investida, "Investida Perfurante", "Aponte a espada para frente + A: avança ~4,5 m atravessando inimigos", 1, null, true, 3f, 20f),
            new SkillNode(Contra, "Postura de Contra", "Gatilho: por 2 s o próximo aparo/bloqueio (espada ou escudo) vira um contra-ataque de dano alto", 2, new[] { Investida }, true, 4f, 15f),
            new SkillNode(VentoLamina, "Lâmina de Vento", "Botão B: os próximos 2 golpes lançam cortes de vento", 4, new[] { Contra }, true, 2f, 15f),
            new SkillNode(FioAfiado, "Fio Afiado", "+25% de dano corpo a corpo", 2, new[] { Investida }),
            new SkillNode(PassoLeve, "Passo Leve", "Esquiva custa 40% menos estamina", 3, new[] { FioAfiado }),
            new SkillNode(InvestidaCadeia, "Investida em Cadeia", "Investida recarrega 2x mais rápido e vai 40% mais longe", 5, new[] { PassoLeve }),
        };

        public static readonly SkillNode[] ElementalistaTree =
        {
            new SkillNode(BolaFogo, "Bola de Fogo", "Segure o gatilho e arremesse: explode em área", 1, null, true, 0.6f, 15f, ResourceKind.Mana),
            new SkillNode(LancaGelo, "Lança de Gelo", "Aponte o cajado + A: perfura até 3 e deixa lento", 2, new[] { BolaFogo }, true, 1.2f, 12f, ResourceKind.Mana),
            new SkillNode(Raizes, "Raízes", "Segure B, mire no chão e solte: prende inimigos por 3 s", 4, new[] { LancaGelo }, true, 4.5f, 20f, ResourceKind.Mana),
            new SkillNode(Combustao, "Combustão", "Explosão da Bola de Fogo 60% maior", 2, new[] { BolaFogo }),
            new SkillNode(FonteArcana, "Fonte Arcana", "Mana regenera 2x mais rápido", 3, new[] { Combustao }),
            new SkillNode(GeloProfundo, "Gelo Profundo", "Lança de Gelo congela totalmente por 1,5 s", 5, new[] { FonteArcana }),
        };
    }
}
