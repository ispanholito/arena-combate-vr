using System;

namespace Arena.Core
{
    // Habilidade própria de cada tipo de arma (vale para qualquer classe). Ativa pelo gesto de ERGUER a arma
    // acima da cabeça (ponta para cima) por um instante: ela "carrega" e o próximo golpe rápido solta a habilidade.
    public enum WeaponAbilityKind { CorteAmplo, FendaNoChao, Terremoto, ArremessoPerfurante, PassoSombrio, PulsoArcano }

    public sealed class WeaponAbilityDef
    {
        public WeaponAbilityKind Kind { get; }
        public string Name { get; }
        public string Description { get; }
        public float Cooldown { get; }
        public float Cost { get; }
        public ResourceKind Resource { get; }

        public WeaponAbilityDef(WeaponAbilityKind kind, string name, string description, float cooldown, float cost, ResourceKind resource)
        {
            Kind = kind; Name = name; Description = description; Cooldown = cooldown; Cost = cost; Resource = resource;
        }
    }

    // Passiva da arma (só Rara ou melhor).
    public enum WeaponPassive { Nenhuma, SedeDeSangue, Impeto, EcoArcano, RecargaRapida, Atordoante }

    public static class WeaponAbilities
    {
        public const string CooldownId = "arma";

        public static readonly WeaponAbilityDef CorteAmplo = new WeaponAbilityDef(WeaponAbilityKind.CorteAmplo, "Corte Amplo",
            "o golpe acerta todos à frente (2,8 m)", 8f, 25f, ResourceKind.Stamina);
        public static readonly WeaponAbilityDef FendaNoChao = new WeaponAbilityDef(WeaponAbilityKind.FendaNoChao, "Fenda no Chão",
            "onda em linha de 6 m que derruba a postura", 10f, 30f, ResourceKind.Stamina);
        public static readonly WeaponAbilityDef Terremoto = new WeaponAbilityDef(WeaponAbilityKind.Terremoto, "Terremoto",
            "atordoa todos num raio de 3 m e esmaga a postura", 12f, 30f, ResourceKind.Stamina);
        public static readonly WeaponAbilityDef ArremessoPerfurante = new WeaponAbilityDef(WeaponAbilityKind.ArremessoPerfurante, "Lança Espectral",
            "dispara uma lança fantasma que atravessa até 5", 8f, 20f, ResourceKind.Stamina);
        public static readonly WeaponAbilityDef PassoSombrio = new WeaponAbilityDef(WeaponAbilityKind.PassoSombrio, "Passo Sombrio",
            "surge atrás do inimigo mais próximo; o próximo golpe é crítico", 9f, 20f, ResourceKind.Stamina);
        public static readonly WeaponAbilityDef PulsoArcano = new WeaponAbilityDef(WeaponAbilityKind.PulsoArcano, "Pulso Arcano",
            "explosão em volta que afasta todos (3 m)", 10f, 20f, ResourceKind.Mana);

        public static WeaponAbilityDef For(WeaponKind k) => k switch
        {
            WeaponKind.Espada => CorteAmplo,
            WeaponKind.Machado => FendaNoChao,
            WeaponKind.Maca => Terremoto,
            WeaponKind.Lanca => ArremessoPerfurante,
            WeaponKind.Adaga => PassoSombrio,
            _ => PulsoArcano,
        };

        // Lendária = versão desperta: efeito ×1,5 e recarga −40%. (Simplificação: habilidades únicas ficam para depois.)
        public static float Power(Rarity r) => r == Rarity.Lendario ? 1.5f : 1f;

        public static float CooldownFor(ItemInstance item)
        {
            float cd = For(item.Base.Kind).Cooldown;
            if (item.Rarity == Rarity.Lendario) cd *= 0.6f;
            if (item.Passive == WeaponPassive.RecargaRapida) cd *= 0.7f;
            return cd;
        }

        // Dano base da habilidade (antes de raridade/ferreiro, que entram pelo DamageMultiplier da arma).
        public static float BaseDamage(WeaponAbilityKind k) => k switch
        {
            WeaponAbilityKind.CorteAmplo => 26f,
            WeaponAbilityKind.FendaNoChao => 32f,
            WeaponAbilityKind.Terremoto => 18f,
            WeaponAbilityKind.ArremessoPerfurante => 28f,
            WeaponAbilityKind.PassoSombrio => 0f,     // o dano vem do golpe crítico seguinte
            _ => 20f,
        };

        public const float BackstabMultiplier = 2f, BackstabWindow = 2.5f;
        public const float StunSeconds = 1.5f;

        public static string PassiveName(WeaponPassive p) => p switch
        {
            WeaponPassive.SedeDeSangue => "Sede de Sangue: abate cura 8",
            WeaponPassive.Impeto => "Ímpeto: golpe devolve 4 de estamina",
            WeaponPassive.EcoArcano => "Eco Arcano: golpe devolve 3 de mana",
            WeaponPassive.RecargaRapida => "Recarga Rápida: habilidade -30% recarga",
            WeaponPassive.Atordoante => "Atordoante: 12% de chance de atordoar 1 s",
            _ => "",
        };

        public const float KillHeal = 8f, HitStamina = 4f, HitMana = 3f, StunChance = 0.12f, PassiveStunSeconds = 1f;
    }

    // Gesto de carregar: arma erguida (ponta acima da cabeça, apontando para cima) por RaiseSeconds → carregada por
    // ChargedSeconds; um golpe rápido (ponta > ReleaseSpeed) nesse tempo solta a habilidade.
    public sealed class WeaponCharge
    {
        public const float RaiseSeconds = 0.45f, ChargedSeconds = 4f, ReleaseSpeed = 4f;
        public enum Event { None, Charged, Released, Expired }

        float raiseT, chargedT;
        public bool Charged => chargedT > 0f;
        public float RaiseProgress => Math.Clamp(raiseT / RaiseSeconds, 0f, 1f);

        public Event Tick(float dt, bool raised, float tipSpeed, bool canCharge)
        {
            if (chargedT > 0f)
            {
                if (tipSpeed > ReleaseSpeed && !raised) { chargedT = 0f; return Event.Released; }
                chargedT -= dt;
                return chargedT <= 0f ? Event.Expired : Event.None;
            }
            raiseT = raised && canCharge ? raiseT + dt : 0f;
            if (raiseT >= RaiseSeconds) { raiseT = 0f; chargedT = ChargedSeconds; return Event.Charged; }
            return Event.None;
        }

        public void Cancel() { raiseT = 0f; chargedT = 0f; }
    }
}
