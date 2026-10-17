# Roteiro de teste no Quest (novidades desde o último teste)

Último teste no Quest: fases 1–4 da arma física (2026-10-16, manhã). Tudo abaixo foi feito depois e **só foi testado no editor do PC**.
Instalar: Quest no USB → `adb install -r unity/Builds/ArenaVR.apk` (ou peça para eu instalar).

Dica: na pausa (botão ☰), ligue **Mostrar FPS** antes de começar — anote o número em cada parte (alvo: 72; verde = ok, amarelo = 70–90% do alvo, vermelho = abaixo).

Para cada item: ✅ ok · ⚠️ funciona mas precisa ajuste (diga o quê) · ❌ não funciona.

---

## 00000. NOVO — correções do seu teste de 2026-10-17
- [ ] **Controle não vira mão**: segurando os controles, a arma/escudo nunca mais pulam para a pose da mão rastreada. Teste girando a mão, fechando os dedos e tirando o controle da vista das câmeras (atrás das costas, acima da cabeça): a arma fica parada no lugar em vez de saltar.
- [ ] **Mão presa na arma**: agora há uma luva no cabo. Encoste a espada na parede e empurre a mão para dentro: a luva/espada ficam do lado de cá; o controle real aparece como a mão fantasma azul. A espada só volta sozinha se ficar a mais de 1,2 m por 1 s **sem parede no meio**.
- [ ] **Estocada**: a lâmina entra aos poucos conforme você empurra (vibra enquanto desliza), o goblin fica preso, sangrando e pesando. Ele só sai se você puxar a espada até a ponta sair ou sacudir forte para o lado (arremessa). O recuo natural do braço depois da estocada NÃO deve soltar. Dá a sensação de entrar?
- [ ] **Raio do Freeza**: aponte o indicador da mão livre — um ponto vermelho carrega na ponta do dedo e sai um raio fino vermelho. Continuando a apontar, dispara a cada 0,4 s. A palma aberta parada não dispara mais.

## 0000. NOVO — 9ª leva (prioridade A do estudo do B&S)
- [ ] **Força do golpe por arma**: maça/machado rápidos batem bem mais forte que lentos; estocada rápida ganha muito; espada quase constante. **Arremessar a adaga** causa ×3 (lança ×1,5).
- [ ] **Ataques variados**: goblin/esqueleto/orc atacam de cima, da direita, da esquerda; esqueleto também estoca e dá passo à frente (de mais longe). O braço erguido mostra de onde vem — dá para ler a tempo e aparar do lado certo? (No fácil só de cima e dos lados.)
- [ ] **Reação ao golpe**: bater em quem está no meio do ataque derruba mais ("DERRUBADO!"); em guarda ele aguenta; cabeça/pernas derrubam mais; orc aguenta mais; chefe só com a postura quebrada.
- [ ] **Queimando**: ~3 Bolas de Fogo acendem o inimigo (enraizado acende na hora): fica laranja piscando, perde ~24 de vida em 4 s. Lança de Gelo apaga.
- [ ] **Eletrocutado**: 3 raios do Freeza seguidos travam o inimigo (azul piscando, quase parado, não ataca) por 1,2 s; o **orc** (armadura) fica 2,4 s. Depois tem 2,5 s sem poder travar de novo. Chefe resiste. Forte demais?
- [ ] **Penalidade de morte** (pausa → Morte): nenhuma / XP do nível (padrão) / XP + metade do ouro / **uma vida** (morrer apaga o personagem e volta ao menu inicial — teste com um personagem descartável!).
- [ ] **Poção**: começa com 1, máx. 3, cura 45%. **Beber: levar a mão esquerda (controle ou mão livre) à boca e segurar ~0,5 s** (desktop B). HUD mostra quantas. Baús têm 35% de chance de poção. O gesto dispara sem querer (escudo perto do rosto ao bloquear)?
- [ ] **Loja no santuário** (mesa ao norte do mercador, perto dos portais): 3 armas em pé com preço (grip compra; a da mão vai para a 2ª vaga ou cai no chão) e banca de poções (30 de ouro, 3 por visita). Estoque renova toda vez que você volta. O mercador paga 25% do preço da loja pela 2ª arma.

## 000. NOVO — dano por tipo e material (estudo do B&S)
- [ ] **Escolher a arma certa**: espada no esqueleto aparece "RESISTE (osso)", maça aparece "FRACO!" e mata rápido; no **orc** (armadura) a espada quase não entra e o **machado** resolve; estocada acha as frestas da armadura.
- [ ] **Golpe de chapa** (lâmina de lado) com espada vira pancada fraca. Fica claro quando o golpe é bom?
- [ ] **Partes do corpo**: cabeça ×2, pernas ×0,6.
- [ ] **Nocaute**: maça (ou pancada forte) na cabeça atordoa ("NOCAUTE!").
- [ ] **Golpe fatal**: estocada rápida na cabeça de goblin ou arqueiro mata na hora ("GOLPE FATAL").

## 00. NOVO — IA inspirada no estudo do Blade & Sorcery
- [ ] **Fichas de ataque**: com vários goblins, só 2 atacam ao mesmo tempo no normal (1 no fácil, 3 no difícil); os outros rondam a ~2 m esperando a vez. O cerco ficou justo?
- [ ] **Interromper**: golpe forte durante o aviso (clava vermelha) cancela o ataque ("INTERROMPIDO"). Golpe fraco não.
- [ ] **Janela de combo**: logo depois de levar um golpe o goblin não consegue bloquear por 0,75 s → dá para emendar golpes.
- [ ] **Revide**: depois de bloquear seu golpe ("BLOQUEOU"), ele contra-ataca rápido ("REVIDE!", aviso mais curto). Dá tempo de aparar?
- [ ] Mude a **dificuldade** na pausa e sinta a diferença na agressividade (quantos atacam, intervalo entre ataques, rapidez do revide).

## 0. NOVO — seus pedidos de 2026-10-16 (depois de jogar no PC)
- [ ] **Slot vazio**: guarde a 2ª arma na estante e pegue outra → ela vai para a 2ª vaga (antes trocava a da mão).
- [ ] **Armas só em baús** (e mímico/chefe); inimigos comuns deixam só ouro. A dungeon ficou "limpa"? Faltam armas?
- [ ] **Lança Espectral**: aparece na ponta da lança com uma linha de mira e espera 0,5 s; aponte a lança para o alvo (slime). Meio segundo está bom?
- [ ] **Raio do Freeza** (palma aberta parada, mão esquerda livre; Z no PC): raio fino e instantâneo, 16 de dano, 6 de mana, atravessa 2. Compensa agora? O visual lembra o do Freeza?
- [ ] **Soco** (punho fechado indo rápido para frente; X no PC): acerta 1 inimigo até ~1 m além da mão, 30 de dano, recarga 1 s; socar o ar não gasta mana. O alcance está bom?
- [ ] **Santuário**: estante **permanente** (leste: guarda, nunca some), estante **temporária** (oeste: o que ficou para trás; some ao entrar num portal), **mercador** (baú de moedas perto da bigorna: vende a 2ª arma).
- [ ] **Modo arcade**: vencer o Rei Goblin sobe a **profundidade** (portal mostra; HUD mostra "prof."). Mais vida/dano/inimigos e itens mais raros. **Morrer na dungeon volta ao santuário na profundidade 1.** Pedra do recomeço ao lado dos portais (grip 2x). A arena também tem profundidade.
- [ ] **Rei Goblin estilo Souls**: ficar colado spamando agora perde (no meu teste no nível 1 você morre com o chefe a 1/3 da vida). Confira: pisão em quem fica colado (círculo em volta dele), rugido com aviso que joga longe, investida quando você foge (se errar, ele tropeça), combos na fase 2 com pausa no fim, defesa alta até quebrar a postura (o aparo é o caminho). Difícil demais? Justo?
- [ ] **Dificuldade** agora muda também o tempo de aviso dos ataques (fácil mais lento, difícil mais rápido).

## 1. Menu, santuário e salvar
- [ ] Menu inicial: 1ª linha troca o personagem (Continuar / Novo Lâmina / Novo Elementalista). Começar um novo por cima do salvo pede confirmação.
- [ ] **Santuário**: sala com fogueira no meio, bigorna à esquerda e 3 portais (Arena, Dungeon, Treino). Os nomes dos portais dá para ler?
- [ ] Entrar num portal leva ao modo. Pausa → B volta ao santuário (no santuário, B volta ao menu inicial).
- [ ] **Salvar**: jogue um pouco (suba de nível, pegue ouro/arma), feche o app, abra de novo → "Continuar" mostra nível e ouro certos.
- [ ] Sair do Treino (nível 10) e voltar: o personagem de verdade volta (nível certo).

## 2. Opções na pausa (botão ☰)
- [ ] Cima/baixo no analógico direito navega, A muda a opção, B volta.
- [ ] Volume (0–100%).
- [ ] Giro com dois controles: rápido 30°, rápido 45° ou suave. O suave enjoa?
- [ ] Vinheta ao andar (bordas escurecem ao andar/girar): ajuda ou atrapalha?
- [ ] Modo sentado (+45 cm): jogue sentado e veja se a altura fica boa.
- [ ] **HUD no pulso esquerdo**: dá para ler olhando para o pulso? O escudo cobre? (posição foi chutada)
- [ ] Dificuldade (fácil/normal/difícil): no difícil os goblins aguentam mais e batem mais forte.
- [ ] Mostrar FPS.
- [ ] **Música** (ligar/desligar): calma no santuário, escura na dungeon, com tambor quando há inimigos perto. Cansa? Volume bom?

## 3. Correções do seu último teste (ainda não conferidas)
- [ ] Espada **não treme ao andar** (a arma agora anda junto com o corpo; física a 72 Hz).
- [ ] Escudo: **gatilho esquerdo** guarda/tira. Esquiva: **clique do analógico direito** também funciona com dois controles.
- [ ] **Cabeça ×1,5** ("CABEÇA!") em goblin/esqueleto/chefe.
- [ ] **Machado pelo gume**: golpe com o gume na frente dá dano cheio; de lado, menos.
- [ ] Peso da arma ao trocar (Q / clique esquerdo): o machado continua pesado depois de trocar e destrocar? (no Treino o nível 10 deixa ele leve de propósito)
- [ ] Chefe não fica travado com golpes seguidos: 4 golpes rápidos sem quebrar a postura = **RUGIDO** (empurra você). *(Você pediu para revisar o rugido depois — ainda empurra pouco.)*
- [ ] Raízes: a mira no chão não "pula" para os pés; trava no alcance máximo.
- [ ] Bola de Fogo vai para onde você arremessa (não para onde você anda).

## 4. Estocada: espetar e cravar (refeito depois do teste)
- [ ] Estocada com a ponta num **slime/goblin**: ele fica **espetado** na espada (pesa na mão, sangra).
- [ ] Golpe rápido para o lado com ele espetado: **arremessa**; bater na parede/chão causa dano; o slime normalmente explode.
- [ ] Puxar a espada para trás ou apontar para baixo: ele escorrega e cai.
- [ ] No **chefe/boneco/mímico**: a espada fica **cravada** e sai da mão; **segurar o gatilho direito** arranca (chefe demora 1,5 s; HUD mostra %).
- [ ] Lança e adaga também cravam; maça não.

## 5. Armas, loot e ferreiro
- [ ] Inimigos deixam **ouro** ("+6 ouro") e às vezes uma **arma** no chão (feixe de luz na cor da raridade; nome e atributos aparecem perto).
- [ ] **Grip direito** com a mão perto da arma no chão pega (a sua fica no chão).
- [ ] Testar as 6 armas: Espada, Machado, Cajado, **Maça** (só a cabeça fere, quebra postura), **Lança** (1,9 m, só a ponta fere), **Adaga** (leve). O peso de cada uma faz sentido?
- [ ] Arma fora da classe: aviso de −15%. Elementalista sem o cajado: "Magias precisam do cajado".
- [ ] **Bigorna** no santuário (grip perto dela): melhora a arma (+1…+5) gastando ouro.
- [ ] **Suporte de armas** (parede leste do santuário, até 12): grip numa arma pendurada troca pela sua; grip num gancho vazio guarda a sua 2ª arma. Arma esquecida na dungeon aparece pendurada lá.

## 6. Habilidade da arma (gesto novo)
- [ ] **Erguer a arma acima da cabeça com a ponta para cima** ~0,5 s: vibra e a ponta brilha. Golpe rápido em até 4 s solta.
  - Espada: Corte Amplo (todos à frente) · Machado: Fenda no Chão (linha) · Maça: Terremoto (atordoa em volta) · Lança: Lança Espectral (projétil) · Adaga: Passo Sombrio (surge atrás; próximo golpe ×2) · Cajado: Pulso Arcano (empurra).
- [ ] **Importante**: o gesto dispara sem querer quando você levanta a espada para aparar golpe de cima? (se sim, mudo para exigir grip junto)
- [ ] Armas **raras+** têm passiva (aparece no rótulo); **lendárias** têm nome próprio e efeito extra (ex.: Ceifadora do Crepúsculo lança 3 cortes de vento). Para ver uma lendária rápido: mate o Rei Goblin algumas vezes ou abra baús.

## 7. Dungeon (portal "Dungeon" no santuário)
- [ ] Andar 1 → escada → andar 2 → Rei Goblin → portal de volta ao santuário.
- [ ] Ao entrar numa sala: **grades fecham** as saídas e os inimigos aparecem do lado de longe; "Sala limpa" abre as grades.
- [ ] **Baú** aparece em algumas salas limpas: grip abre (ouro + às vezes arma).
- [ ] **Mímico**: às vezes o baú morde ("MÍMICO!"). O bote é anunciado (tampa vermelha) e dá para aparar. É justo?
- [ ] **Espinhos**: a placa pisca vermelho 0,7 s antes de subir. Dá tempo de sair? Atrair goblins para cima funciona?
- [ ] Tochas nas paredes; iluminação suficiente?
- [ ] Morrer volta à última fogueira e a sala em combate recomeça.
- [ ] **Minimapa**: pause na dungeon — aparece à direita do menu (salas visitadas coloridas, as seguintes apagadas, você = ponto amarelo com seta). Dá para ler? Está do lado certo?
- [ ] **FPS na dungeon** com 5–6 inimigos (anote o número).

## 8. Inimigos novos e IA
- [ ] **Esqueleto**: golpes fortes de fio arrancam cabeça/braço/perna; sem braço não ataca, sem pernas rasteja; as peças voltam em 4 s, a menos que você quebre a peça no chão.
- [ ] **Goblin Arqueiro**: fica longe e atira (o arco brilha ao puxar). Escudo bloqueia. **Rebater a flecha com a espada** é possível? (ela é rápida: 14 m/s)
- [ ] Goblins em grupo **cercam pelos lados**; goblin muito ferido às vezes **recua** ("RECUOU").
- [ ] **Orc** (andar 2 da dungeon; arena a partir do nível 5): grande, lento, golpe de machado muito forte e bem anunciado; só cambaleia com a postura quebrada. Aparar dá certo? Ele assusta?
- [ ] Goblin/esqueleto **bloqueiam** com a arma ("BLOQUEOU") quando você bate de frente; golpe forte, machado ou duas mãos atravessam. Fica chato ou bom?

## 9. Som
- [ ] Sons de golpe, aparo, bloqueio, cravar, magias, rugido, ossos, loot, espinhos. Algum alto/baixo demais ou irritante?
- [ ] **Passos** ao andar com o analógico: ajudam ou irritam?

## 10. Ainda pendente (não precisa testar)
- Teste multimodal antigo (MULTIMODAL: FUNCIONANDO) — se tiver tempo.
- Rei Goblin: o rugido deveria jogar você longe (você pediu para fazer depois).
