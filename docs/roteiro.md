# Roteiro: o que falta fazer (atualizado em 2026-10-16)

Prioridade: **A** = agora/próximo · **B** = rumo ao MVP · **C** = depois do MVP · **D** = futuro distante.

## Validar no Quest (pendente de teste seu)
- **A** Novidades de 2026-10-16 (feitas sem headset): som, esqueleto, armas novas (maça, lança, adaga) e o loot no chão (grip direito pega), ferreiro, salvar/continuar, dungeon de 2 andares. Conferir desempenho da dungeon no Quest 2 (1 luz por sala).
- **A** Espetar/cravar refeito (slime/goblin espetados e arremessados; chefe/boneco: segurar o gatilho para arrancar).
- **A** Espada sem tremer ao andar; escudo no gatilho esquerdo; esquiva no clique do analógico direito.
- **A** Cabeça ×1,5; machado acertando pelo gume; duas mãos (grip esquerdo no cabo).
- **A** Ângulo da espada na mão (`VrGripPitch`) e do escudo; se o peso da arma está bom ou "mole".
- **A** Peso que "some" ao trocar de arma: não reproduzido no editor; descobrir o passo exato (lembrar que o Treino é nível 10).

## Combate (núcleo do jogo)
- **A** Rei Goblin: rugido precisa **jogar o jogador longe** (hoje empurra pouco); revisar a luta inteira (você pediu para fazer depois).
- **A** Calibrar com o boneco de treino: dano de cada arma e habilidade, estamina por golpe, força do arremesso.
- ~~Som~~ feito (28 efeitos sintetizados). **C** Passos, música ambiente e sons gravados de verdade.
- **B** Efeitos de impacto melhores (faíscas no aparo, sangue/gosma, rastro da lâmina).
- **B** Mãos com dedos fechando na arma (poses de empunhadura) e mão esquerda visível.
- ~~Desmembrar~~ feito no esqueleto. **C** Estender a goblins/orcs (sem remontar).
- **C** Fase 5 da física: ragdoll ativo com modelos de verdade (projeto EggyStudio, MPL-2.0).
- **C** Arremessar a espada com rotação e cravar à distância.
- **C** Hurricane VR como plano B se a física própria não chegar no nível desejado.

## Classes e habilidades
- **B** Lâmina e Elementalista: revisar números com o boneco; 3 habilidades + 3 passivas cada hoje.
- **B** Loadout planejado: 4 da classe + 1 da arma + 1 suprema (hoje são 3 ativas).
- **A** Habilidades das armas (comum = 1 ativa; raro/épico = ativa + passiva; lendário = única). As 6 armas e o loot já existem; falta isso para elas terem identidade.
- **B** Sinais de mão (já prontos e testados no código, `HandSigns`) para habilidades novas.
- **C** Classe só de mãos (provavelmente o Monge).
- **C** Guardião (tanque com escudo pesado; diferenciar do escudo da Lâmina), Clérigo, Generalista.
- **D** Lanceiro, Berserker, Assassino, Arqueiro, Invocador, Lâmina Arcana.

## Inimigos
- ~~Esqueleto~~ feito (desmembra e se remonta).
- **B** Mais ataques e comportamentos para goblin e chefe (bloquear, recuar, flanquear).
- **C** Orcs, lobos, morcegos, aranhas, mímicos.

## Conteúdo e progressão (MVP)
- **B** Modelos 3D de verdade (personagens, armas, cenário) no lugar das primitivas.
- **A** Santuário (hub) simples com fogueira, ferreiro e portal (hoje o "santuário" é o menu inicial; o ferreiro fica na árvore, perto da fogueira).
- ~~Dungeon de 2 andares + chefe~~ feita (gerada por semente). **B** Portas que trancam na emboscada, baús, armadilhas, tochas nas paredes, salas com formas diferentes, minimapa.
- ~~6 armas e loot (5 raridades, 0–3 atributos), ouro, ferreiro +1 a +5~~ feito. **B** Inventário maior que 2 slots e vender/desmontar armas.
- ~~Salvar o progresso~~ feito (nível, árvore, ouro, armas, chefes vencidos).
- **B** Equipamento esquecido reaparece na fogueira; gesto de chamar a arma.
- **C** Escala de dificuldade e companheiros IA opcionais.

## Interface e conforto
- **B** HUD no pulso (hoje preso à cabeça) e árvore de habilidades visual.
- **B** Opções de conforto: giro suave, vinheta ao andar, altura sentado.
- **B** Menu de opções (volume, mão dominante, sensibilidade).

## Técnico
- **B** Desempenho no Quest 2 (medir quadros com vários inimigos e ragdolls).
- **B** Rodar no PC via Quest Link (fluxo de teste rápido).
- **C** Coop de 1 a 4 jogadores (o mais difícil: física em rede).
- **D** PvP.

## Já feito (resumo)
Projeto Unity no Quest; arena jogável; Lâmina e Elementalista com árvore; poderes da mão livre; Slime, Goblin, Rei Goblin; menu (continuar/novo; Arena/Dungeon/Treino) com boneco de DPS; pausa; escudo; espada física (fases 1–4); espetar/cravar; ponto fraco; som sintetizado; Esqueleto que desmembra; 6 armas com raridades, atributos, ouro e ferreiro; salvar; dungeon de 2 andares com o Rei Goblin; 122 testes automáticos.
