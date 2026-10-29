using System;

namespace Arena.Core
{
    // NPCs resgatados, pets e mercenários (inspiração Soul Knight).
    // Na dungeon, às vezes uma sala de combate tem uma JAULA: limpou a sala, abre (grip/E) e o NPC vai morar no
    // santuário para sempre. Cada um oferece um serviço:
    //   Domadora  → pet (fada que ataca ou fada que cura), fica com você em todo lugar.
    //   Capitão   → contrata um mercenário para a PRÓXIMA ida (arena ou dungeon).
    //   Alquimista → elixir: +1 poção no máximo (até +2).
    public enum NpcId { Domadora, Capitao, Alquimista }
    public enum PetKind { Nenhum, FadaFaisca, FadaCurandeira }

    public static class AllyRules
    {
        public const double CageChance = 0.6;   // por andar, se ainda houver alguém para resgatar
        public const int PetPrice = 80, MercenaryPrice = 100, MaxExtraPotions = 2;
        public static int ElixirPrice(int extraSlotsNow) => 120 * (extraSlotsNow + 1);

        // Fada Faísca: raio pequeno no inimigo mais perto. Fada Curandeira: cura devagar.
        public const float FairyDamage = 7f, FairyInterval = 1.4f, FairyRange = 10f, HealerPerSecond = 1.5f;
        // Mercenário: segue você, luta corpo a corpo com quem estiver perto. Não é alvo dos inimigos (simplificação).
        public const float MercDamage = 14f, MercInterval = 1.1f, MercReach = 1.5f, MercAggro = 9f, MercLeash = 14f, MercSpeed = 3.6f;

        public static string Name(NpcId n) => n switch
        {
            NpcId.Domadora => "Domadora",
            NpcId.Capitao => "Capitão",
            _ => "Alquimista",
        };

        public static string Role(NpcId n) => n switch
        {
            NpcId.Domadora => "pets",
            NpcId.Capitao => "mercenários",
            _ => "elixir (+1 poção)",
        };

        public static string PetName(PetKind p) => p switch
        {
            PetKind.FadaFaisca => "Fada Faísca",
            PetKind.FadaCurandeira => "Fada Curandeira",
            _ => "nenhum",
        };

        // Quem está na próxima jaula: o primeiro ainda não resgatado (ou nenhum).
        public static NpcId? NextToRescue(Func<NpcId, bool> rescued)
        {
            foreach (NpcId n in Enum.GetValues(typeof(NpcId))) if (!rescued(n)) return n;
            return null;
        }
    }
}
