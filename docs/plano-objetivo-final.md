# Plano até o objetivo final (2026-10-17)

**Objetivo final**: RPG de combate físico em VR no Quest com a sensação do Blade & Sorcery (golpes, perfurar, agarrar e
desmembrar com física de verdade), mais classes, árvore, loot e dungeons (ver `CLAUDE.md` → Visão).

**Por que este plano**: hoje os inimigos são um bloco único movido por código. Agarrar, perfurar e desmembrar ficam
"fingidos" e não dá para chegar no B&S só ajustando números. O B&S usa **ragdoll ativo**: cada inimigo é um esqueleto
físico (ossos com juntas) que a animação puxa como músculos. A base do plano é trocar os nossos inimigos por isso.

Cada fase tem um **critério de saída**: só passamos para a próxima quando ele estiver valendo no Quest.

---

## Fase A — Inimigos com corpo físico (ragdoll ativo)  ← começando agora
| Passo | O que | Critério de saída |
|---|---|---|
| A1 | **Goblin físico de teste no Treino** (`RagdollGoblin`): 11 ossos com juntas e "músculos" (molas que seguem a pose), preso em pé por uma junta à posição da IA; anda, ataca com uma clava física; golpes empurram os membros de verdade; perfurar = junta da lâmina no osso; agarrar = junta da mão no osso; desmembrar = quebrar a junta do membro. | No Quest: dá vontade de bater nele; perfurar e agarrar parecem B&S; **FPS ≥ 72 com 1 goblin**. |
| A2 | Medir desempenho: 3 e 6 goblins físicos. **(opção 1/3/6 no Treino feita em 2026-10-19 — falta medir no Quest)** | ≥ 72 FPS com 6; se não, reduzir ossos (sem antebraço/canela) ou trocar a física para 2 iterações. |
| A3 | **(1ª parte feita em 2026-10-19: repertório, fichas, cerco, guarda, clava desarmável)** IA no corpo físico: repertório de ataques (já existe), aparo físico (a clava bate na espada e para), fichas de ataque, reações (perde o equilíbrio, cai, levanta), bloquear com a arma. | Combate contra 3 goblins físicos tão bom quanto o atual, sem bugs de corpo voando. |
| A4 | Portar esqueleto, arqueiro, orc e o Rei Goblin para o corpo físico (slime continua como está). **(2026-10-19: goblin, esqueleto e orc físicos feitos (beta na Arena/Dungeon); faltam arqueiro e Rei Goblin)** | Todos os humanoides físicos na arena e na dungeon. |
| A5 | Desmembramento completo: cabeça, braços, pernas (sem perna rasteja, sem braço da arma não ataca), sangue, peças com física. | Igual ou melhor que o esqueleto atual. |
| A6 | Remover o inimigo "bloco" antigo e os atalhos que fingiam física (espetar por posição, agarrar movendo o inimigo inteiro). | Código antigo removido, testes passando. |

**Portão de decisão (depois de A1)**: se o nosso ragdoll ativo ficar instável ou pesado no Quest, testar o
**PuppetMaster** (RootMotion, ~US$ 90) antes de seguir.

## Fase B — Mãos e armas do jogador
| Passo | O que | Critério |
|---|---|---|
| B1 | **Mãos físicas**: a mão do jogo para em paredes e inimigos (hoje só a arma para). | Mão nunca atravessa nada; o controle real aparece como fantasma. |
| B2 | Dedos que se fecham em volta do que foi agarrado (pose por objeto). | Agarrar arma, osso e cabo parece natural. |
| B3 | Agarrar qualquer coisa: armas no chão, peças desmembradas, cadáveres, objetos do cenário. | Tudo que é físico pode ser pego e arremessado. |
| B4 | Inimigo atingido na mão solta a arma; duas mãos na lança/machado melhoradas. | — |

**Portão de decisão (depois de A3)**: se mãos físicas próprias não ficarem boas, comprar o **Hurricane VR** (US$ 70)
só para as mãos. Ele não resolve o inimigo, por isso vem depois da Fase A.

## Fase C — Gráficos (junto com A4)
- Modelos low-poly **em peças por membro** (KayKit gratuito/CC0 ou Synty POLYGON) já montados no esqueleto da Fase A.
  Peças separadas deixam o desmembramento simples, sem cortar malha.
- Iluminação "baked" + tochas, sangue e faíscas, pós-processamento leve. Alvo fixo: 72 FPS no Quest 2.

### Marcação: IA local para assets (decidido em 2026-10-18 — fazer quando a Fase C começar)
- **O que**: IA rodando no PC do Pablo para gerar **texturas e ícones** (Stable Diffusion/Flux), **modelos 3D base** (Hunyuan3D/TRELLIS → limpar e separar por membro no Blender), **vozes** (TTS) e **efeitos sonoros**. Opcional: autocompletar código offline (Continue + Ollama).
- **O que NÃO é**: não substitui o Claude no código do projeto; não roda no Quest (IA dentro do jogo fica fora do MVP).
- **Antes de instalar**: conferir placa de vídeo/VRAM e RAM do PC; instalar tudo no **E:** (modelos de 5-40 GB cada; C: está lotado) com caches apontando para `E:UnityCache`.
- **Comparar com kits prontos** (KayKit, Quaternius — CC0) antes: modelos gerados por IA vêm pesados e sem esqueleto para o ragdoll.

## Fase D — Conteúdo do MVP
Santuário, dungeon de 2-3 andares, 4-5 inimigos físicos, chefe estilo Souls, Lâmina + Elementalista, **luvas de poder + inventário**, loot e ferreiro, nível 10. A maior parte já existe em código e só precisa migrar para os corpos físicos.

## Fase E — Balanceamento e polimento
Roteiro de teste no Quest a cada leva, dificuldade, conforto (vinheta, sentado), desempenho, salvar.

## Fase F — Depois do MVP
Classe só de mãos (jutsus por sinais, teleporte com recarga), Generalista, Guardião, Clérigo, coop 1-4.

---

## Ordem prática das próximas levas
1. **A1** — goblin físico no Treino (esta leva).
2. Teste seu no Quest → ajustes de A1 → **A2** (desempenho).
3. **A3** (IA física) → portão do PuppetMaster.
4. **A4 + C** (outros inimigos já com modelos novos).
5. **A5, A6**, depois **B1-B4** (portão do Hurricane).
6. **D** (luvas, inventário, conteúdo) e **E**.
