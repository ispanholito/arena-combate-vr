using UnityEngine;

namespace Arena
{
    // "Representante" do goblin com corpo físico na Arena e na Dungeon (opção "Goblins físicos (beta)" na pausa).
    // É um inimigo comum (ArenaEnemy) INVISÍVEL e sem colisão que segue o corpo físico (RagdollGoblin) e divide a mesma
    // vida com ele. Assim magias, raio, habilidades das armas, espinhos, XP, ouro, loot, ondas e "sala limpa" funcionam
    // sem mudar nada; os golpes da arma, perfurar, agarrar e desmembrar acontecem no corpo físico.
    public sealed class PhysicalGoblinEnemy : ArenaEnemy
    {
        RagdollGoblin body;
        public RagdollGoblin Body => body;

        protected override bool LeavesCorpse => false;   // o corpo físico já fica no chão

        protected override void BuildModel()
        {
            float s = Arena.Core.RagdollSpec.For(Def.Kind).Scale;
            bodyBottom = 0.25f * s; bodyTop = 1.05f * s;
            new GameObject("Modelo").transform.SetParent(transform, false);
        }

        void Start()
        {
            if (BodyCollider) BodyCollider.enabled = false;   // a arma bate nos ossos, não aqui
            var p = Player;
            body = RagdollGoblin.Spawn(transform.position, p ? p.FeetPosition : transform.position + transform.forward, State, this);
        }

        protected override void Think(float dt, Vector3 toPlayer, float distance) { }   // quem pensa é o corpo físico

        protected override void Update()
        {
            base.Update();
            if (body && !Dead) transform.position = body.FeetPosition;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            // Removido pelo jogo (troca de sala/modo, morte do jogador): leva o corpo junto. Morreu lutando: o corpo fica.
            if (body && (State == null || !State.Dead)) Destroy(body.gameObject);
        }
    }
}
