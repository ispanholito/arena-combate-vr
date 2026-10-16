using System;
using System.Collections.Generic;

namespace Arena.Core
{
    public enum Limb { Cabeca, BracoDireito, BracoEsquerdo, PernaDireita, PernaEsquerda }
    public enum LimbState { Preso, Caido, Destruido }

    // Esqueleto: perde partes com cortes fortes e se remonta se as peças caídas não forem quebradas.
    public sealed class SkeletonBody
    {
        public const float SeverSpeed = 4.5f;          // m/s na lâmina para arrancar uma parte
        public const float SeverMinEdge = 0.5f;        // precisa ser corte de fio (não pancada de lado)
        public const float ReassembleSeconds = 4f;     // tempo até a peça caída voltar
        public const float BreakSpeed = 2f;            // golpe numa peça caída (m/s) quebra a peça

        readonly Dictionary<Limb, LimbState> state = new Dictionary<Limb, LimbState>();
        readonly Dictionary<Limb, float> fallenTime = new Dictionary<Limb, float>();

        public SkeletonBody()
        {
            foreach (Limb l in Enum.GetValues(typeof(Limb))) state[l] = LimbState.Preso;
        }

        public LimbState this[Limb l] => state[l];
        public bool Has(Limb l) => state[l] == LimbState.Preso;

        // Qual parte um golpe atinge: altura (0 = pés, 1 = topo) e lado (positivo = direita do esqueleto).
        public static Limb LimbAt(float heightFraction, float side)
        {
            if (heightFraction >= 0.8f) return Limb.Cabeca;
            if (heightFraction >= 0.45f) return side >= 0f ? Limb.BracoDireito : Limb.BracoEsquerdo;
            if (heightFraction <= 0.35f) return side >= 0f ? Limb.PernaDireita : Limb.PernaEsquerda;
            return Limb.BracoDireito;   // tronco: o braço da arma é o alvo mais exposto
        }

        public static bool CanSever(WeaponKind weapon, float speed, float edge) =>
            weapon != WeaponKind.Cajado && speed >= SeverSpeed && edge >= SeverMinEdge;

        // Tenta arrancar a parte. Retorna true se caiu agora.
        public bool Sever(Limb l)
        {
            if (state[l] != LimbState.Preso) return false;
            state[l] = LimbState.Caido;
            fallenTime[l] = 0f;
            return true;
        }

        public bool Break(Limb l)
        {
            if (state[l] != LimbState.Caido) return false;
            state[l] = LimbState.Destruido;
            return true;
        }

        // Avança o tempo; devolve as partes que voltam a se encaixar agora.
        public List<Limb> Tick(float dt)
        {
            var back = new List<Limb>();
            foreach (Limb l in Enum.GetValues(typeof(Limb)))
            {
                if (state[l] != LimbState.Caido) continue;
                fallenTime[l] += dt;
                if (fallenTime[l] >= ReassembleSeconds) { state[l] = LimbState.Preso; back.Add(l); }
            }
            return back;
        }

        // Efeitos de jogo
        public bool CanAttack => Has(Limb.BracoDireito);
        public float SpeedMultiplier
        {
            get
            {
                int legs = (Has(Limb.PernaDireita) ? 1 : 0) + (Has(Limb.PernaEsquerda) ? 1 : 0);
                return legs == 2 ? 1f : legs == 1 ? 0.55f : 0.25f;   // sem pernas: rasteja
            }
        }
        public float TurnMultiplier => Has(Limb.Cabeca) ? 1f : 0.3f;     // sem cabeça: gira devagar
        public float WindupMultiplier => Has(Limb.Cabeca) ? 1f : 1.5f;   // e demora mais para atacar
    }
}
