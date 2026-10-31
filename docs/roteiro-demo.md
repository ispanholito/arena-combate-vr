# Roteiro da demo (rascunho — Pablo, 2026-10-30)

Demo **linear e montada à mão** (não usa o gerador de dungeon), numa caverna. Classe: Lâmina. Duração-alvo: 12-20 min.
Inspiração de monstros: SAO temporada 1 (conceito e clima, nunca copiar desenho/nome). Rei Goblin com porte de brutamonte (2-2,5 m), luta de espada de igual para igual (como Kirito × Illfang).

## Sequência FINAL (Pablo, 2026-10-31) — substitui a tabela de baixo

| # | Sala | O que acontece | Ganha |
|---|------|----------------|-------|
| 0 | Cela | Amarrado; puxar os braços com força rompe a corda. | — |
| 1 | Cadáver | Puxar a espada cravada (sai aos poucos); escudo ao lado. | Espada + escudo |
| 2 | Esqueleto | 1 esqueleto enfraquecido (60% da vida). | — |
| 3 | Estátua do fogo | Pôr a mão na mão da estátua. Fogueira (checkpoint). | **Bola de Fogo** + espada em chamas |
| 4 | 3 goblins | Testar o fogo neles. | — |
| 5 | Orc + arqueiro + xamã | Orc com escudo. Ao limpar: orbe. | **Lâmina de Vento** |
| 6 | Fogueira | Checkpoint antes do chefe. | — |
| 7 | Porta pesada → sala do chefe | Porta só abre empurrando com as **duas mãos** (armas guardadas). Altar com a aura → **cena**: o Rei entra curvado pela porta, arranca a porta e usa de **escudo**, ruge e a **estante tomba** fechando a saída. Luta. | **Aura Vermelha** |
| 8 | Fim | Rei morto → **coroa** (pegar e pôr na cabeça); **queimar a estante** com fogo (espada em chamas ou bola); sair = "FIM DA DEMO", recomeça em 12 s. | Coroa |

**Escudo de porta do Rei** (falta fazer): porta **com chapas de ferro** — corte de vento faz CLANG e some, espada em chamas solta faísca/"GROSSA DEMAIS!", golpe comum racha pouco, **aura** racha forte (4 golpes ≈ 2 ciclos de aura). O escudo cobre só a frente; depois de pisão/investida o Rei fica aberto 1-2 s. Fase 2: duas espadas.

**Montado (2026-10-31, versão BRUTA)**: `Runtime/DemoMode.cs` (modo `GameMode.Demo`; `Demo.OnlyDemo = true` = o build abre direto nela; pausa → "Recomeçar a demo"). `DemoGate`: na demo ficam escondidos árvore, Investida, habilidade da arma, raio/soco/palma, raízes/onda, Muralha, raio do céu; Bola de Fogo só depois da estátua; Vento e Aura pela árvore com `Tree.Grant` ao pegar o orbe. Escudo de porta funcional e fase 2 com duas espadas: FEITOS (2026-10-31). Falta: arte das salas, do cadáver e do Rei; balancear a luta no Quest.

## Sequência (versão anterior, 2026-10-30)

| # | Cena | O que acontece | Ensina | Já temos? |
|---|------|----------------|---------|-----------|
| 0 | **Despertar** | Acorda numa caverna, **amarrado** (pulsos presos). Separar as mãos com força rompe a corda. | Que o corpo/mãos importam | Não (corda + início roteirizado) |
| 1 | **A espada no cadáver** | Corpo de um aventureiro com uma espada **cravada** e um **escudo** do lado. Segurar o cabo e **puxar** arranca; o escudo vai na mão esquerda ou nas costas. | Pegar arma física | Sim (`StuckSword`, sai aos poucos) — falta o cadáver com arte |
| 2 | **Esqueleto** | 1º inimigo, sozinho. | Golpe, aparo, peso da arma; membros saem e voltam | Sim (esqueleto físico) |
| 3 | **Estátua do fogo** (depois de matar o esqueleto) | Estátua segura uma chama. Tocar com a **mão livre** passa a chama para a mão → Bola de Fogo. Depois: **mergulhar a espada** na bola = lâmina em chamas. | Soltar o controle esquerdo, magia da mão, combo arma+magia | Parcial (bola e lâmina em chamas existem; falta a estátua/desbloqueio) |
| 4 | **3 goblins** | Sala de grupo, só corpo a corpo. | Lutar contra vários (fichas, cerco), usar o fogo | Sim |
| 5 | **Orc + apoio** | Orc **com escudo** + 1 arqueiro + 1 xamã. | Prioridade de alvo, rebater orbe/flecha, escudo que racha | Sim (escudo quebrável, 2026-10-31) — falta montar a sala |
| 5b | **Recompensa do orc** | Ao matar o orc: **aura vermelha** (substitui a Postura de Contra) — quebra defesas e escudos. | Quebrar escudo | Sim (habilidade) — falta dar como recompensa |
| 6 | **Baú** | Baú com **Lâmina de Vento** (cortes de vento com a espada). | Ataque à distância | Sim (habilidade existe; falta dar pelo baú) |
| 7 | **Fogueira** | Cura e salva antes do chefe. | Checkpoint | Sim |
| 8 | **Rei Goblin (X1)** | Entra **quebrando a parede** e rugindo. Fase 1: **clava de madeira + escudo (porta velha surrada)**; o escudo racha e quebra com a aura. Fase 2 (50% da vida): larga o que sobrou e pega **duas espadas** (de onde: decidir no teste — costas ou outro lugar), repertório novo, mais rápido. | Ler padrões, aparo, postura, quebrar escudo | Parcial (chefe existe com clava/fúria; falta entrada pela parede, escudo-porta, duas espadas, modelo) |
| 9 | **Fim** | Câmera lenta, loot, portal → tela "continua no jogo completo" (+ santuário como gancho?). | — | Parcial |

## Decisões fechadas (2026-10-30)
- **Armas como no Blade & Sorcery**: arma no chão é um objeto físico deitado (cai, rola, pode ser chutada/empurrada), **sem feixe de luz e sem girar** estilo Fortnite. Raridade só por detalhe (gema/brilho fraco); nome/atributos aparecem só com a mão perto. Vale para o jogo todo.
- **Postura de Contra sai.** Nova habilidade **aura vermelha**: ativa → a espada fica vermelha por alguns segundos, dá mais dano e **quebra defesas**: o bloqueio com arma de goblin/esqueleto não segura, e **escudos racham e quebram de verdade** (rachaduras aparecendo; 3-4 golpes com a aura). Sem a aura o escudo também quebra, mas bem mais devagar (não pode travar o jogador).
- **Escudos quebráveis** nos inimigos (orc da sala 5 e Rei Goblin). O orc ensina antes do chefe.
- **Na demo ficam escondidos**: árvore de habilidades (sobe de nível só para vida/estamina), Investida, poderes da mão fora da Bola de Fogo, outros chefes, modos, biomas, pets, NPCs, armas de fogo, Elementalista.
- Desbloqueios da demo são **roteirizados** (estátua, orc, baú), não pela árvore.

- **Escudo vira item** (não fica mais preso na mão esquerda): achado junto com a espada do cadáver. A mão esquerda segura qualquer arma. **FEITO (2026-10-30)**, junto com os coldres.
- **Coldres estilo Blade & Sorcery**: 2 nos OMBROS (qualquer tamanho, inclusive grandes: escudo, arco, lança, martelo) e 2 na CINTURA (pequenas e médias: espada, adaga, maça, machadinha, pistola). Mão perto do coldre + grip = pega; soltar a arma perto do coldre = guarda.

## Críticas / pontos a decidir
1. ~~Aura × lâmina em chamas~~ — fechado: aura quebra defesas/escudos, fogo é dano ao longo do tempo.
2. **Começar amarrado**: em VR, perder o controle do corpo incomoda. Manter curto (≤ 10 s) e fazer o jogador se soltar sozinho (separar as mãos), nunca uma cena parada.
3. **Esqueleto como 1º inimigo**: bom (lento, assustador, mostra a física). Mas o "remontar" pode confundir quem nunca jogou. Proposta: esse primeiro já vem **fraco** (sem um braço / enferrujado) e uma placa/voz ensina "quebre as peças no chão".
4. **Soltar o controle para a bola de fogo**: é o momento mais estranho para um jogador novo. A estátua tem que pedir isso claramente ("solte o controle esquerdo e toque a chama"). Se o teste mostrar que confunde, dar a bola de fogo pelo botão também.
5. ~~Investida e outros poderes~~ — fechado: escondidos.
6. ~~Árvore~~ — fechado: escondida na demo.
7. **2 andares × caverna linear**: o plano anterior era a dungeon gerada de 2 andares. Este roteiro é montado à mão — melhor para demo (dá para dosar o ritmo), mas é trabalho de montar as salas uma a uma.
8. **Rei Goblin com 2 espadas**: ótimo (é o "nodachi surpresa" do Illfang). Golpes das espadas precisam ser aparáveis e com aviso claro, senão vira injusto.
9. **História mínima que amarra tudo** (sugestão): os goblins capturaram você; o cadáver é de outro prisioneiro que tentou fugir; o Rei Goblin é o captor. Sem texto longo — só o cenário contando.

## Ordem de trabalho sugerida
1. Armas físicas no chão (sem feixe/giro) + puxar arma cravada pelo grip.
2. Aura vermelha no lugar da Postura de Contra + escudos quebráveis (orc).
3. Rei Goblin: entrada pela parede, escudo-porta + clava, fase 2 com duas espadas.
4. Montar a caverna linear (salas 0-9) e os gatilhos (corda, estátua, baú, recompensa do orc).
5. Esconder o que não entra (outros chefes, modos, biomas, pets, NPCs, armas de fogo, Elementalista, poderes fora da lista).
6. Arte (bíblia de arte + modelos) e teste no Quest de ponta a ponta.
