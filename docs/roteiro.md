# Roteiro: o que falta fazer (atualizado em 2026-10-16)

Prioridade: **A** = agora/próximo · **B** = rumo ao MVP · **C** = depois do MVP · **D** = futuro distante.

## Direção até vender (2026-10-28, combinado com o Pablo)
1. **Congelar funcionalidades**: nada de classe/sistema/poder novo até o "pedaço vendável". Só correção, polimento, arte e IA.
2. **Pedaço vendável**: ciclo completo e bonito de 20–30 min (Santuário → dungeon de 2 andares → Rei Goblin) com arte em tudo do MVP (slime, esqueleto, orc, arqueiro, mímico, Rei Goblin, 6 armas, mãos, dungeon no estilo da sala do Treino), inimigos físicos como padrão e uma **sala-tutorial** dos gestos.
3. **Teste com o público**: 5–10 adolescentes jogando sem explicação; só observar.
4. **Diferencial para vender**: a magia sem controle (raio, bola teleguiada, onda de duas mãos, teia) — vídeos curtos.
5. **Demo grátis** (itch.io/SideQuest) com o pedaço vendável → **acesso antecipado pago** (~US$ 10–15, Meta Horizon Store; Steam depois) com 2–3 h de conteúdo, arte coerente, sem bug que quebre a partida e conforto.
6. **Obrigatório antes de vender**: renomear "Kamehameha" e "Freeza" (marcas de Dragon Ball) e não citar "Homem-Aranha"; conferir a licença do TRELLIS.2 (e do Hunyuan, se usar); KayKit é CC0; teto de receita do Unity Personal; conta de desenvolvedor, W-8BEN e de preferência MEI.

## Próxima prioridade (decisão do Pablo, 2026-10-28): VISUAL, depois balanceamento
- Habilidades "completinhas por agora"; a mecânica da teia com gravidade aprovada ("ficou bem maneiro").
- Proposta: 1º uma **fatia vertical** (1 sala da dungeon + goblin com modelo real + mãos com pele) medindo FPS no Quest 2 antes de trocar tudo; escolher UM estilo coerente (pacote low-poly dark fantasy) e um orçamento (triângulos, draw calls, 1 luz real por sala, luz assada). Inimigos físicos pedem modelos separados por membro (plano, fase C).
- **Feito**: sala do Treino com KayKit (grátis, CC0) e esqueleto físico com modelo cortado por osso. Próximo: goblin/orc (o KayKit grátis não tem; opções: KayKit Adventurers adaptado, outro pacote CC0, ou pago), arma e mãos do jogador, levar o estilo para Arena/Dungeon se o FPS aguentar.
- Balanceamento depois do visual, jogando.

## Validar no Quest (pendente de teste seu)
- **A** **Siga `docs/teste-quest.md`** (lista completa de tudo desde o último teste, na ordem de uma sessão).
- **A** 2026-10-28: teia por ganchos (olhar acende, mão aberta → fechar), embalo no ar, mago sem controle (raízes com as duas mãos no chão, Kamehameha, bola de fogo na palma). **Decidir**: esses poderes ficam para todas as classes ou viram a classe de mãos (Monge/luvas)?
- **A** Novidades de 2026-10-16 (feitas sem headset): som, esqueleto, armas novas (maça, lança, adaga) e o loot no chão (grip direito pega), ferreiro, salvar/continuar, dungeon de 2 andares. Conferir desempenho da dungeon no Quest 2 (1 luz por sala).
- **A** 2ª leva de 2026-10-16: habilidade da arma (erguer a arma acima da cabeça e golpear — ver se dispara sem querer ao aparar golpe de cima), portas da emboscada e baús, santuário com bigorna e portais, menu de opções na pausa (giro suave, sentado), armas esquecidas voltando no santuário.
- **A** 3ª leva de 2026-10-16: espinhos (o aviso dá tempo de sair?), mímico (o bote é justo?), Goblin Arqueiro (rebater a flecha com a espada é possível no VR?), goblins cercando, HUD no pulso (posição/legibilidade).
- **A** Espetar/cravar refeito (slime/goblin espetados e arremessados; chefe/boneco: segurar o gatilho para arrancar).
- **A** Espada sem tremer ao andar; escudo no gatilho esquerdo; esquiva no clique do analógico direito.
- **A** Cabeça ×1,5; machado acertando pelo gume; duas mãos (grip esquerdo no cabo).
- **A** Ângulo da espada na mão (`VrGripPitch`) e do escudo; se o peso da arma está bom ou "mole".
- **A** Peso que "some" ao trocar de arma: não reproduzido no editor; descobrir o passo exato (lembrar que o Treino é nível 10).

## Pedidos do Pablo depois de jogar no PC (2026-10-16, tarde) — FEITOS (testar no Quest)
- ✅ **Bug**: guardou uma das 2 armas (ex.: o machado no suporte) e não dá mais para pôr outra no lugar — pegar arma sempre troca a da mão. Pegar deve **preencher o slot vazio** primeiro.
- ✅ **Armas só nos baús** (inimigos deixam só ouro, para a dungeon não ficar "suja" de armas). Talvez no futuro alguns inimigos voltem a soltar arma.
- ✅ **Lança Espectral**: hoje sai na hora em que abaixa a arma e não dá para mirar (ex.: num slime). Dar ~0,5 s para mirar antes de soltar.
- ✅ **Metralhadora da mão**: dano não compensa a mana. Redesenhar como os **raios do Freeza** (Dragon Ball Z): raios finos, rápidos e fortes.
- ✅ **Golpe no chão com a mão (atordoar em área)**: guardar no código para usar depois e trocar por um **soco** na direção dos inimigos — alcance um pouco maior que o braço, dano bem forte, alvo único (não em área), recarga baixa.
- ✅ **Santuário**: duas estantes — **permanente** (o que você escolheu guardar) e **temporária** (o que ficou para trás na dungeon; some se você não tirar antes da próxima entrada). **Vender** armas ali para ter ouro para o ferreiro.
- ✅ **Modo "arcade" da dungeon (e da arena)**: fica mais difícil a cada vez que entra, e quanto mais fundo, mais raros os itens; voltar à base para vender e melhorar antes de entrar de novo; na entrada escolher **continuar ou recomeçar**; **morrer reseta do zero** (por enquanto).
- **C** **Modo só com as mãos** (sem controles) — fazer depois.

## Rebalanceamento (pedido do Pablo em 2026-10-16) — 1ª passada FEITA; ajustar depois de jogar
- ✅ 1ª passada: Rei Goblin estilo Souls (defesa fora da postura, pisão, rugido que joga longe, investida, combos na fase 2, sem ajudantes), dificuldade muda também o tempo de aviso, raio do Freeza 2,7 de dano por mana (metralhadora era 1,2), Lança de Gelo 22, lendárias ×1,2 no dano. Tabela atual de dano por mana (por alvo): Bola de Fogo 1,5 (área), Lança de Gelo 1,47 (atravessa 3), corte da palma 1,3, raio 2,7 (atravessa 2), soco 3 (corpo a corpo). Próximo: medir no Quest e ajustar.
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

**9ª leva (2026-10-17, prioridade A do estudo do B&S):** dano por velocidade por tipo de arma e arremesso; repertório de ataques dos inimigos; tabela de reações a golpes; status (queimando, eletrocutado); penalidade de morte opcional; poção de cura e loja no santuário. Falta: mão atingida solta a arma.

## Ideias do Pablo (2026-10-17) — a decidir
- **Luvas de poder**: os poderes da mão livre deixam de ser de todas as classes; cada luva (loot) dá um poder (raio, soco, corte da palma...). Crítica: a luva ocupa a mão esquerda — a Lâmina com escudo já larga o escudo para usar; decidir se a luva é um "slot" de equipamento (vale com o escudo nas costas) ou se substitui o escudo. Sugestão: slot de equipamento, 1 luva por vez, raridade como as armas.
- **Inventário / tela do personagem**: equipamento (arma 1 e 2, luva, depois armadura), mochila com o que carrega, atributos e poções. No VR: painel no pulso ou "olhar para o cinto". Crítica: inventário com muitos itens pede limite (peso ou vagas) senão vira acumulador; a estante permanente já faz o papel de baú.
- **Gráficos**: hoje tudo é primitiva. Caminho barato: pacote de modelos low-poly dark fantasy (ex.: Synty/POLYGON Dungeon, KayKit Dungeon — este grátis, CC0), iluminação baked + luz das tochas, pós-processamento leve (o Quest 2 aguenta pouco). Fazer depois que o combate estiver aprovado no Quest, senão o retrabalho de colisões/medidas é grande.

## Ideias do Pablo (2026-10-17, tarde) — para depois
- **Andar sem controle (só mãos)**: mão direita puxada para trás = virar para trás / para frente = andar para frente, ou **teleporte com recarga**. Base para uma **classe só de mãos** estilo jutsu do Naruto (sequência de sinais de mão = técnica; `HandSigns` já reconhece 6 sinais). Crítica: locomoção por gesto cansa e conflita com os poderes da mesma mão — sugestão: teleporte apontando com a mão direita em formato de sinal específico (ex.: "V") + confirmação ao fechar a mão, com recarga; andar contínuo fica no analógico enquanto houver controle.
- **Desmembrar braços dos goblins** (hoje só cabeça; o esqueleto já perde braços/pernas) e **mão atingida solta a arma** (item pendente do estudo).

- **IA local para assets** (marcação de 2026-10-18): montar quando a Fase C (gráficos) começar — detalhes em `docs/plano-objetivo-final.md`.
