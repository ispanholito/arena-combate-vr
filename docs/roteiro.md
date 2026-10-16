# Roteiro: o que falta fazer (atualizado em 2026-10-16)

Prioridade: **A** = agora/próximo · **B** = rumo ao MVP · **C** = depois do MVP · **D** = futuro distante.

## Validar no Quest (pendente de teste seu)
- **A** **Siga `docs/teste-quest.md`** (lista completa de tudo desde o último teste, na ordem de uma sessão).
- **A** Novidades de 2026-10-16 (feitas sem headset): som, esqueleto, armas novas (maça, lança, adaga) e o loot no chão (grip direito pega), ferreiro, salvar/continuar, dungeon de 2 andares. Conferir desempenho da dungeon no Quest 2 (1 luz por sala).
- **A** 2ª leva de 2026-10-16: habilidade da arma (erguer a arma acima da cabeça e golpear — ver se dispara sem querer ao aparar golpe de cima), portas da emboscada e baús, santuário com bigorna e portais, menu de opções na pausa (giro suave, sentado), armas esquecidas voltando no santuário.
- **A** 3ª leva de 2026-10-16: espinhos (o aviso dá tempo de sair?), mímico (o bote é justo?), Goblin Arqueiro (rebater a flecha com a espada é possível no VR?), goblins cercando, HUD no pulso (posição/legibilidade).
- **A** Espetar/cravar refeito (slime/goblin espetados e arremessados; chefe/boneco: segurar o gatilho para arrancar).
- **A** Espada sem tremer ao andar; escudo no gatilho esquerdo; esquiva no clique do analógico direito.
- **A** Cabeça ×1,5; machado acertando pelo gume; duas mãos (grip esquerdo no cabo).
- **A** Ângulo da espada na mão (`VrGripPitch`) e do escudo; se o peso da arma está bom ou "mole".
- **A** Peso que "some" ao trocar de arma: não reproduzido no editor; descobrir o passo exato (lembrar que o Treino é nível 10).

## Rebalanceamento (pedido do Pablo em 2026-10-16 — próxima prioridade)
Problema relatado: **chegar colado no Rei Goblin e ficar spamando golpes mata o chefe antes de ele matar você.** O chefe não pune quem fica parado batendo.
- **A** **Direção para os chefes (Pablo, 2026-10-16): estilo Souls** — luta **só contra o chefe** (sem invocar ajudantes nem ondas junto), chefe **bem difícil**, com padrões de ataque que se aprendem, avisos claros e janelas de punição. Encaixa nos pilares (aparo + barra de postura já existem). Consequências: o Rei Goblin deixa de invocar slimes; a sala do chefe tem a fogueira logo antes (já tem) para tentar de novo rápido; morrer no chefe não deve custar caminhada longa. Cuidado de VR: lutas longas cansam o braço — preferir lutas intensas e curtas (2-4 min) a esponjas de vida.
- **A** **Rei Goblin — mecânicas novas**:
  - Rugido (hoje: 4 golpes em 2,5 s sem postura quebrada → empurra 5 m/s e causa 6) precisa **jogar o jogador longe** (vários metros, com dano de verdade e um instante sem controle) e ser anunciado (o chefe enche o peito antes).
  - Punir quem fica colado: golpe de área curto e rápido em volta dele (pisão) quando o jogador passa X s a menos de 1,5 m; agarrão/arremesso.
  - Fases com padrões diferentes (fase 2 mais agressiva, combos de 2-3 golpes, investida), janelas claras para atacar (depois de errar um golpe pesado, postura quebrada).
  - Mais vida efetiva ou redução de dano quando não está atordoado; dano alto só na janela de postura quebrada.
  - Medir: tempo para matar spamando vs. jogando direito (aparo/postura) — o segundo tem que ser bem mais rápido e seguro.
- **A** **Dificuldade**: rever os multiplicadores (fácil 0,6/0,75 · normal 1/1 · difícil 1,4/1,3) depois de jogar; talvez a dificuldade também mude a agressividade (tempo de aviso, intervalo entre ataques), não só números.
- **A** **Dano por mana**: rever o custo-benefício das magias e poderes da mão (Bola de Fogo 20 de mana / 18-30 de dano, Lança de Gelo, Raízes 25, metralhadora 2,5 por tiro / 3 de dano, soco 20, palma 15, Pulso Arcano). Montar uma tabela dano÷mana e igualar por papel (área vs. alvo único vs. controle).
- **A** **Habilidades**: rever números e recargas das habilidades das classes e das armas (Investida, Contra 90 de dano, Lâmina de Vento, habilidades das 6 armas e efeitos das lendárias — ex.: Fenda no Chão da lendária deu 98 e mata goblin de um golpe).
- **A** Ferramenta: usar o **Treino** (DPS do boneco) e o medidor de FPS; se ajudar, criar um "modo laboratório" no Treino com chefe parado/ativo para medir tempo de luta.

## Combate (núcleo do jogo)
- **A** Calibrar com o boneco de treino: dano de cada arma e habilidade, estamina por golpe, força do arremesso.
- ~~Som, passos e música ambiente~~ feitos (sintetizados). **C** Sons gravados de verdade.
- **B** Efeitos de impacto melhores (faíscas no aparo, sangue/gosma, rastro da lâmina).
- **B** Mãos com dedos fechando na arma (poses de empunhadura) e mão esquerda visível.
- ~~Desmembrar~~ feito no esqueleto. **C** Estender a goblins/orcs (sem remontar).
- **C** Fase 5 da física: ragdoll ativo com modelos de verdade (projeto EggyStudio, MPL-2.0).
- **C** Arremessar a espada com rotação e cravar à distância.
- **C** Hurricane VR como plano B se a física própria não chegar no nível desejado.

## Classes e habilidades
- **B** Lâmina e Elementalista: revisar números com o boneco; 3 habilidades + 3 passivas cada hoje.
- **B** Loadout planejado: 4 da classe + 1 da arma + 1 suprema (hoje são 3 ativas).
- ~~Habilidades das armas e lendárias únicas~~ feitas (1 por tipo; passivas em Rara+; lendárias com nome e efeito extra).
- **B** Sinais de mão (já prontos e testados no código, `HandSigns`) para habilidades novas.
- **C** Classe só de mãos (provavelmente o Monge).
- **C** Guardião (tanque com escudo pesado; diferenciar do escudo da Lâmina), Clérigo, Generalista.
- **D** Lanceiro, Berserker, Assassino, Arqueiro, Invocador, Lâmina Arcana.

## Inimigos
- ~~Esqueleto~~ feito (desmembra e se remonta).
- ~~Goblin recuar e cercar pelos lados~~ feito; ~~Goblin Arqueiro~~ feito. ~~Goblin bloquear~~ feito. **B** Chefe com mais ataques.
- ~~Mímico e Orc~~ feitos. **C** Lobos, morcegos, aranhas.

## Conteúdo e progressão (MVP)
- **B** Modelos 3D de verdade (personagens, armas, cenário) no lugar das primitivas.
- ~~Santuário~~ feito (fogueira, bigorna, 3 portais). **C** NPCs, loja, baú de guardar armas.
- ~~Dungeon de 2 andares + chefe~~ feita (gerada por semente). ~~Portas que trancam e baús~~ feitos. ~~Armadilhas, tochas e mímico~~ feitos. ~~Minimapa~~ feito (na pausa). **B** Salas com formas diferentes.
- ~~6 armas e loot (5 raridades, 0–3 atributos), ouro, ferreiro +1 a +5~~ feito. ~~Guardar armas~~ feito (suporte com 12 no santuário). **B** Vender/desmontar armas.
- ~~Salvar o progresso~~ feito (nível, árvore, ouro, armas, chefes vencidos).
- ~~Equipamento esquecido volta~~ feito (no santuário). Chamar a arma: já existe (gatilho com a arma arremessada).
- ~~Escala de dificuldade~~ feita (opção na pausa). **C** Companheiros IA opcionais.

## Interface e conforto
- ~~HUD no pulso~~ como opção (ajustar posição no Quest). **B** Árvore de habilidades visual.
- ~~Opções de conforto~~ feitas na pausa (volume, giro rápido/suave, vinheta, sentado). **B** Mão dominante (canhoto) e sensibilidade.

## Técnico
- **A** Desempenho no Quest 2: medir com a opção "Mostrar FPS" (feita) na dungeon com vários inimigos e ragdolls.
- **B** Rodar no PC via Quest Link (fluxo de teste rápido).
- **C** Coop de 1 a 4 jogadores (o mais difícil: física em rede).
- **D** PvP.

## Já feito (resumo)
Projeto Unity no Quest; arena jogável; Lâmina e Elementalista com árvore; poderes da mão livre; Slime, Goblin, Rei Goblin; menu (continuar/novo; Arena/Dungeon/Treino) com boneco de DPS; pausa; escudo; espada física (fases 1–4); espetar/cravar; ponto fraco; som sintetizado; Esqueleto que desmembra; 6 armas com raridades, atributos, ouro e ferreiro; salvar; dungeon de 2 andares com o Rei Goblin, portas e baús; habilidades das armas; santuário; opções de conforto; armas esquecidas; espinhos, mímico, Goblin Arqueiro, goblins que cercam; HUD no pulso; dificuldade; medidor de FPS; goblins que bloqueiam; lendárias únicas; minimapa; Orc; música e passos; suporte de armas; 149 testes automáticos.
