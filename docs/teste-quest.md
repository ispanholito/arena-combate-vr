# Roteiro de teste no Quest (novidades desde o último teste)

Último teste no Quest: fases 1–4 da arma física (2026-10-16, manhã). Tudo abaixo foi feito depois e **só foi testado no editor do PC**.
Instalar: Quest no USB → `adb install -r unity/Builds/ArenaVR.apk` (ou peça para eu instalar).

Dica: na pausa (botão ☰), ligue **Mostrar FPS** antes de começar — anote o número em cada parte (alvo: 72; verde = ok, amarelo = 70–90% do alvo, vermelho = abaixo).

Para cada item: ✅ ok · ⚠️ funciona mas precisa ajuste (diga o quê) · ❌ não funciona.

---

## 0000000000000000000000000000000000000000000000000000. NOVO — Rei Goblin com escudo de porta de verdade e fase 2 com duas espadas
- **Escudo de porta** (protege a FRENTE): golpe normal para nele e racha pouco (~10 golpes); **com a aura** racha forte e quebra em ~3 ("RACHOU!" → "VAI QUEBRAR!" → "ESCUDO QUEBROU!", ele fica atordoado 1,2 s). Corte de vento de frente = "CLANG!" e some; bola de fogo de frente = "GROSSA DEMAIS!"; espada em chamas solta faísca e fumaça. Bater no escudo também enche a postura dele.
- **Janelas**: depois do pisão, da investida e do golpe no chão ele fica aberto ~1-2 s (aí o golpe pega no corpo). Pelas costas sempre pega.
- **Fase 2** (metade da vida): larga a clava (e o que sobrou da porta) e saca **duas espadas** (os dois braços golpeiam juntos).
- **Me diga**: se dá para entender que precisa da aura, se as janelas são curtas demais, se a luta ficou longa/difícil demais.

## 000000000000000000000000000000000000000000000000000. NOVO — porta alcançável, mãos sempre visíveis, bola de fogo no controle
- **Porta pesada**: a parede invisível sumiu (o corredor até a porta estava fora da área). Guarde as armas (inclusive o escudo) e empurre com as duas mãos.
- **Mãos sempre**: com o controle na mão aparece a mão em cima dele — grip fecha os dedos, gatilho o indicador (dedo fora do gatilho = apontando), polegar desce ao encostar nos botões. Segurando arma, a mão é a luva no cabo. Sem controle: a mão rastreada, como antes. **Conferir se a mão fica no lugar certo do controle** (posição medida "de olho" — me diga se fica torta, à frente ou atrás).
- **Bola de fogo pelo controle** (depois da estátua): SEGURE o clique do analógico esquerdo → a bola cresce na frente do controle → jogue a mão (ou solte o botão: vai para onde o controle aponta). Dá para mergulhar a espada nela. O clique esquerdo não troca mais de arma (troca é pelos coldres).

## 00000000000000000000000000000000000000000000000000. NOVO — A DEMO (o jogo abre direto nela)
Jogue de ponta a ponta e anote o que travou, confundiu ou ficou chato. Pausa → "Recomeçar a demo".
- **Cela**: amarrado — puxe os braços com força (separar as mãos rápido) até a corda romper.
- **Cadáver**: puxe a espada; pegue o escudo do lado.
- **Esqueleto** fraco → **estátua**: ponha a mão na mão dela = Bola de Fogo (mão livre: punho esticado → abrir → jogar). Mergulhe a espada na bola.
- **3 goblins** → **orc com escudo + arqueiro + xamã** → orbe da Lâmina de Vento (B).
- **Fogueira** → **porta pesada**: guarde as armas nos coldres e empurre com as DUAS mãos.
- **Sala do chefe**: toque o orbe da aura (gatilho) → o Rei entra curvado pela porta, pega a porta (escudo só visual por enquanto), ruge, a estante tomba na saída → luta.
- Matou: pegue a **coroa** e ponha na cabeça; **queime a estante** (espada em chamas ou bola de fogo); entre na saída = FIM.
- Morrer: volta à última fogueira e a sala recomeça.
- **Me diga**: tempo total, onde ficou perdido, se a porta pesada funciona com as mãos/controles, se o Rei é difícil demais.

## 0000000000000000000000000000000000000000000000000. NOVO — andar não trava, sem teia, aura e vento nas duas armas, aura quebra capacete
- **Andar com o analógico direito** (controle esquerdo largado): não pode mais parar do nada. Causa provável: o controle esquerdo parado no sofá/mesa, visto pelas câmeras, era contado como "na mão" e o andar passava para o analógico esquerdo. Agora os dois analógicos só valem com o esquerdo tocado há menos de 0,6 s. **Se ainda parar, me diga onde estava o controle esquerdo.**
- **Teia fora**: as bolinhas de gancho sumiram (santuário, arena, dungeon). O sinal do chifre 2× (dash para trás) continua. Tutorial pula o passo da teia.
- **Aura vermelha nas duas mãos**: com uma arma na esquerda, as duas ficam vermelhas e as duas dão +25%, quebram guarda e racham escudo.
- **Lâmina de Vento nas duas**: botão B dá 2 cortes para a direita e 2 para a esquerda (se tiver arma nela).
- **Aura quebra capacete**: golpe ≥ 3,5 m/s com a aura na cabeça de quem tem elmo = "CAPACETE QUEBROU!", as peças voam e a cabeça fica sem proteção (Sala de Criação → CAPACETES).

## 000000000000000000000000000000000000000000000000. NOVO — escudo do orc corrigido e puxar arma mais longe com contorno
- **Escudo do orc** (Sala de Criação → ESCUDO DO ORC): antes a batida no escudo era ignorada (nunca rachava). Agora: sem aura racha a cada ~3 golpes e quebra em ~9-13; **com a aura vermelha quebra em 3** (no editor). Conferir "RACHOU!"/"VAI QUEBRAR!"/"ESCUDO QUEBROU!".
- **Puxar arma do chão**: agora até **4 m** dos pés (era 2,5) e a mira é um pouco mais larga (18°, abre até 24° longe). **Marca de "dá para puxar"**: contorno ciano em volta da própria arma + linha fina da mão até ela + vibração leve quando a mira pega a arma. Grip = fica **amarelo** (travada) → puxe a mão para você.

## 00000000000000000000000000000000000000000000000. NOVO — aura vermelha, escudo do orc, aviso do coldre e puxar arma do chão
- **Aura vermelha** (gatilho da Lâmina, no lugar da Postura de Contra): a espada fica vermelha 8 s (brilho, luz, brasas; pisca no fim). +25% de dano. Bater na guarda de um goblin/esqueleto = "DEFESA QUEBRADA!" (atordoa). Recarga 14 s.
- **Escudo do orc** (Sala de Criação → ESCUDO DO ORC; orcs normais têm 50% de chance): a arma bate e PARA no escudo; ele racha ("RACHOU!", rachaduras escuras) e quebra em pedaços. Sem aura ~13 golpes, com aura 4. Quebrou = orc atordoado 0,8 s. Conferir se o escudo fica na frente do peito e se o braço acompanha.
- **Aviso do coldre**: segurando uma arma perto da cintura/ombro aparece "CINTURA DIREITA"/"OMBRO ESQUERDO"... e uma bolinha no lugar (vibra ao entrar). Mão vazia perto de um coldre com arma: mostra o nome dela.
- **Puxar a arma do chão**: mão vazia, aponte o controle para uma arma no chão perto dos pés (até 2,5 m) → anel azul + nome. Grip e puxe a mão para você → ela voa para a mão. Testar com as duas mãos.

## 0000000000000000000000000000000000000000000000. NOVO — coldres, mão esquerda com arma e escudo como item
- Começo (Lâmina): espada na direita, escudo na esquerda, machado no coldre do OMBRO direito (aparece nas costas).
- **Guardar**: leve a arma até a cintura (ou por cima do ombro) e solte o grip → ela fica no coldre (aparece pendurada). Arma grande (escudo, machado, lança, martelo, cajado) só entra nos ombros.
- **Sacar**: mão vazia perto do coldre + grip → a arma vem para a mão (qualquer mão).
- **Mão esquerda**: segure o grip esquerdo para manter o escudo/arma; soltar longe de um coldre = cai no chão. Solte o controle esquerdo (para os poderes da mão): o escudo vai sozinho para as costas.
- **Duas armas**: pegue uma espada do chão com a mão esquerda (grip perto) — as duas batem nos inimigos.
- Escudo na mão esquerda ainda bloqueia e apara. Duas mãos na mesma arma só com a esquerda vazia.
- Sala de Criação → ESPADA CRAVADA: o escudo do aventureiro está do lado.
- **Ajustar no Quest**: posição dos coldres (cintura um pouco à frente do quadril; ombros atrás da cabeça) e se a arma pendurada atrapalha a visão.

## 000000000000000000000000000000000000000000000. NOVO — armas no chão param, corpos não voam
- Armas caídas param em ~1,5 s e não rolam mais pelo chão (antes saíam rodando).
- Goblin morrendo não pode sair voando nem ficar tremendo no chão (mate vários, inclusive com a espada encostada no corpo).

## 00000000000000000000000000000000000000000000. NOVO — golpe na cabeça por região e arma
- **Espada/lâmina no crânio**: não derruba mais (só dano). Com capacete: às vezes (30%) "ATORDOADO!".
- **Lâmina no rosto/boca**: "CORTOU!" e bastante dano, sem derrubar.
- **Lâmina no pescoço**: corte forte e de fio arranca a cabeça; mais fraco pega a veia ("JUGULAR!") e mata. Esqueleto não tem veia (só decapita); orc não morre pela veia.
- **Maça/martelo/frigideira na cabeça**: goblin/esqueleto caem em ~85%; orc fica "ATORDOADO!" em ~65%.
- Golpe que não derruba não faz mais o inimigo cair pelo tranco (antes qualquer golpe na cabeça derrubava).

## 0000000000000000000000000000000000000000000. NOVO — perna arrancada mata
- Arranque a perna de um goblin ou orc: ele morre na hora (antes ficava vivo e paralisado no chão). Esqueleto não: a perna volta em 4 s e ele levanta.

## 000000000000000000000000000000000000000000. NOVO — armas físicas no chão e espada cravada (Sala de Criação)
- **ARMAS**: as armas caem e ficam deitadas no chão, sem feixe e sem girar; a sua espada empurra/vira elas. O nome aparece só com a mão perto (≈ 70 cm). Para pegar é preciso abaixar e levar a mão até a arma (grip, gatilho perto ou pinça). Dá para alcançar sem cansar? (Modo sentado ainda pega pelos pés.)
- **ESPADA CRAVADA**: segure o cabo (grip do controle esquerdo, mão livre fechando, ou gatilho do direito perto do cabo) e puxe para cima ao longo da lâmina: ela tem que SAIR AOS POUCOS (mais presa no começo, vibra e range), não voltar para dentro, e ir para a sua mão quando a ponta sair. Mão longe do cabo (> 30 cm) = escorregou. Volta outra em 3 s.

## 00000000000000000000000000000000000000000. NOVO — esquiva e reação humanas
- [ ] Os inimigos desviam (bola de fogo, Kamehameha, golpe) andando, sem sair disparados?
- [ ] Demoram um instante (~0,3-0,5 s) para reagir? Ficou humano ou lento demais?
- [ ] Erguer a guarda: ainda bloqueiam às vezes ou nunca dá tempo?

## 0000000000000000000000000000000000000000. NOVO — inimigos físicos em todo lugar
- [ ] Arena e Dungeon: goblins, esqueletos e orcs agora são físicos (andar, levantar, desmembrar). **Ligue "Mostrar FPS" na pausa** e veja com 8-9 inimigos: o FPS aguenta?

## 000000000000000000000000000000000000000. NOVO — andar e correr 3ª versão (Sala de Criação → ANDAR)
- [ ] Andando: passos no ritmo do corpo, sem escorregar? Correndo: calcanhar sobe, cotovelos dobrados, tronco inclinado?

## 00000000000000000000000000000000000000. NOVO — juntas corrigidas e levantar 3ª versão (Sala de Criação)
- [ ] **Andar**: agora o joelho dobra de verdade. O goblin dá passos?
- [ ] **Levantar**: caiu de barriga = mãos no chão → de quatro → meio-ajoelhado → de pé; de costas = rola de lado antes. Convence?
- [ ] **Ataques**: os cotovelos agora dobram — o golpe do goblin ficou melhor ou estranho?

## 0000000000000000000000000000000000000. NOVO — animações do goblin e modelo nos goblins simples (ver na Sala de Criação)
- [ ] **Levantar**: de quatro com as mãos no chão → ajoelhado → de pé. Convence?
- [ ] **Pescoço**: a cabeça segue você e olha em volta?
- [ ] **Andar**: joelhos, braços balançando, corpo subindo e descendo. Melhorou?
- [ ] **Goblin comum, arqueiro e xamã** com o modelo novo (estação INIMIGOS). O braço com a clava/arco/cajado ficou certo?

## 000000000000000000000000000000000000. NOVO — Sala de Criação e IA
- [ ] **Sala de Criação** (portal CRIAÇÃO no santuário, canto sudoeste; também dá para ver no PC): as estações aparecem e funcionam (queda, andar, capacetes, Rei Goblin, inimigos, armas, paleta)?
- [ ] **Inimigos chegando**: vêm reto de longe e só rondam/cercam quando estão perto?

## 00000000000000000000000000000000000. NOVO — polimento da demo
- [ ] **Teletransporte** (pausa → Locomoção): analógico para frente mostra o arco; soltar teleporta. Confortável? O arco sai do controle certo?
- [ ] **Baú**: põe a mão embaixo da frente da tampa e levanta — abre?
- [ ] **Rei Goblin**: gostou do visual novo (coroa, capa, clava com cravos)? A entrada com rugido, a FÚRIA na metade da vida e a câmera lenta quando morre funcionam bem no VR (a câmera lenta enjoa?)
- [ ] Ler `docs/biblia-de-arte.md` e responder as 3 perguntas do fim.

## 0000000000000000000000000000000000. NOVO — correções do teste de 30/10
- [ ] **Estocada**: não perfura mais (golpe comum). Dá dano normal?
- [ ] **Armas de fogo**: atiram sem recarregar; a cadência ficou boa? Fortes demais/fracas?
- [ ] **Bênçãos**: não aparece mais altar nem estátua.
- [ ] **Espinhos**: dá para ver a placa (moldura clara + pontinhas) antes de pisar?
- [ ] **Xamã**: ainda atira no chão?
- [ ] **Poção no peito**: aparecem frascos no peito? Pegar (grip esquerdo ou fechar a mão livre), levar à boca → bebe. A posição do peito está boa?
- [ ] **Inimigos andando de lado**: ainda parecem arrastados? Os passos laterais convencem?
- [ ] **Levantar**: ajoelha e sobe — ficou melhor?
- [ ] **Capacete novo**: goblin (bronze + vermelho), esqueleto (crista vermelha), orc (chifres). Gostou?
- [ ] **Dano das armas**: machado/martelo mais fracos por golpe, katana = espada, adaga mais forte. Alguma ainda fora?

## 000000000000000000000000000000000. NOVO — quedas, capacete, martelo
- [ ] Com **espada de fio**, os inimigos não caem mais à toa? Com **maça/martelo** ou batendo de chapa, caem?
- [ ] Inimigos de **capacete** (elmo cinza): golpe forte na cabeça derruba; a cabeça não sai.
- [ ] Quem cai **levanta** depois de ~2 s?
- [ ] **Martelo de Guerra**: acerta quando bate com a face da cabeça?

## 00000000000000000000000000000000. NOVO — corrida 2ª versão e pinça para interagir
- [ ] **Correr**: sem controles, as DUAS mãos ABERTAS, balançando como corrida. Ficou melhor? Com a mão fechada não corre (de propósito).
- [ ] **Corte da palma**: só com o braço ESTICADO na frente. Perto do corpo não sai corte?
- [ ] **Pinça** (indicador + polegar) perto do baú, da estátua, dos pedestais dos desafios, dos NPCs, da arma no chão: funciona sem controle?

## 0000000000000000000000000000000. (substituído pela 2ª versão) correr balançando os braços
- [ ] Larga os DOIS controles e balança os braços como quem corre: anda para onde você olha? A velocidade acompanha o balanço? Para logo quando você para?
- [ ] Correndo, sai corte de vento, soco ou bola de fogo sem querer? (não deveria)
- [ ] O **corte da palma** agora é varrida para o LADO: ainda é fácil de soltar quando você quer?
- [ ] Enjoa? (a vinheta liga junto; dá para desligar a corrida na pausa)

## 000000000000000000000000000000. NOVO — movimento dos inimigos, armas da mão, ganchos da dungeon
- [ ] **Inimigos** (Arena/Dungeon): chegam em curva e ziguezagueando? Aceleram/freiam em vez de deslizar? Dão um tranco para trás quando você acerta (maior com golpe forte)? Às vezes desviam do seu golpe? Ficou melhor ou exagerado?
- [ ] **Pegar arma com o GATILHO**: encoste a mão na arma do chão/estante e aperte o gatilho. A nova vai para a mão?
- [ ] **Vender/guardar**: o mercador e a estante permanente levam a arma da MÃO.
- [ ] **Dungeon**: ganchos no alto das portas e nos corredores — dá para ir de sala em sala de teia?

## 00000000000000000000000000000. NOVO — ajustes da teia, pistola, fusão, fada
- [ ] **Teia**: olhe para um ponto (acende), aponte a mão FECHADA para ele e ABRA → teia (sem bola de fogo). Feche e puxe para voar; abra para soltar. Fora de um ponto aceso, a bola de fogo continua igual?
- [ ] **Santuário**: os ganchos acima dos portais levam você até eles?
- [ ] **Pistola de Repetição**: 8 tiros rápidos. Gostou do ritmo?
- [ ] **Forja**: o texto mostra a arma nova que sai; as duas viram ela?
- [ ] **Trocar de arma** com a mão livre: mão por cima do ombro, atrás da cabeça, e fechar. Dispara sem querer?
- [ ] **Fada**: longe o bastante da visão? Vai até o inimigo e bate?

## 0000000000000000000000000000. NOVO — chefes sorteados, estátuas, biomas e desafios
- [ ] **Quadro de desafios** (santuário, 3 pedestais na frente dos portais; grip): Desafio diário, Horda infinita, Boss rush. O HUD mostra tempo/onda?
- [ ] **Rainha Slime**: o círculo do pulo dá tempo de fugir? Dá para bater depois que ela cai? Os slimes que ela solta atrapalham demais?
- [ ] **Chefe de Guerra Orc**: investida (desviar → ele cai), giro (sair do círculo), grito (postura dura). Difícil demais?
- [ ] **Lich**: rebater os orbes nele é a graça — dá para mirar? Teleporta demais? Os esqueletos atrapalham?
- [ ] **Estátua** (dungeon e canto da arena): ler o texto e comprar com grip.
- [ ] **Biomas**: a luz muda por andar? Estalactites (Gelo) e lava (Fornalha) são justas ou irritantes?
- [ ] **FPS** na Fornalha (lava com brilho) e no boss rush.

## 000000000000000000000000000. NOVO — armas de fogo, forja, Xamã, NPCs, pets e mercenário
- [ ] **Pistola / Bacamarte** (aparecem no loot; para testar rápido, compre na loja quando aparecer): o cano aponta para onde o controle aponta? (se ficar torto, diga quanto: é o `GunPitch`). Gatilho atira? O coice é gostoso? **Recarregar**: aponte para cima e dê um tranco — é natural? Muito fácil ou difícil?
- [ ] **Forja de fusão** (santuário, ao lado da bigorna): com 2 armas, grip na forja. A raridade sobe?
- [ ] **Goblin Xamã** (chapéu roxo, cajado com caveira): rebata o orbe verde com a espada. Vai para os inimigos? Explode?
- [ ] **Jaula** na dungeon: limpe a sala, grip na jaula. O NPC aparece no santuário (parede sul)?
- [ ] **Domadora** (fada) / **Capitão** (mercenário) / **Alquimista** (elixir). A fada atrapalha a visão? O mercenário é útil ou só enfeite?

## 00000000000000000000000000. NOVO — leva Soul Knight (bênçãos, destrutíveis, elites, salas especiais, armas)
- [ ] **Bênçãos**: na dungeon, limpe a 1ª sala de combate → altar com 3 orbes. Dá para ler os nomes? Tocar o orbe com a mão escolhe (com a espada na mão também)? Na arena aparece no centro a cada 6 abates.
- [ ] **Caixas e barris**: quebram com um golpe normal? Solta ouro às vezes? **Barril vermelho** explode e leva os vizinhos? Bola de fogo também quebra?
- [ ] **Elites**: anel colorido nos pés e "ELITE ..." em cima. O Explosivo dá tempo de fugir (1 s)? O Blindado está chato demais?
- [ ] **Sala secreta**: procure uma parede com rachaduras brilhando numa sala de combate e bata forte. Abre? Tem baú dentro?
- [ ] **Desafio**: "DESAFIO! Limpe a sala em X s". O tempo é justo?
- [ ] **Mercador perdido** na fogueira do andar 2 (nem sempre aparece): comprar com grip.
- [ ] **Armas novas** (aparecem no loot): Katana, Rapieira, Martelo de Guerra, Machadinha, Bacalhau Seco, Frigideira ("BONK!"), Tocha (põe fogo) e Vassoura de Bruxa (cajado). Peso e empunhadura parecem certos? Alguma está torta na mão?

## 0000000000000000000000000. NOVO — Muralha Impenetrável, Raio do céu, lâmina que atravessa
- [ ] **Muralha**: soque o punho na palma aberta da outra mão → aura vermelha (anel nos pés, mãos vermelhas). Soco joga goblin longe? Orc atordoa rápido? Ponha a mão no caminho da clava (bloqueio) e vá rápido contra ela (aparo). Palma batida = onda para frente; punho na palma = onda em volta.
- [ ] **Raio do céu**: olhe para cima, mão fechada acima da cabeça, abra → relâmpago desce na mão. Estique o braço para frente → raio pula entre os inimigos. Algum conflito com a teia (olhando para um gancho aceso)?
- [ ] **Lâmina em chamas atravessa**: com a espada acesa, o golpe passa pelos inimigos sem parar e ainda corta/queima? Arranca membro?

## 000000000000000000000000. NOVO — Kamehameha como no anime e arma em chamas
- [ ] **Kamehameha** (voltou ao gesto anterior em 2026-10-29): palma → pulsos juntos na frente (carrega) → empurrar. Voltou a funcionar como antes?
- [ ] **Arma em chamas** (Lâmina com a mão esquerda livre): carregue a bola de fogo na mão esquerda e passe a espada nela. A espada pega fogo 12 s? Os inimigos pegam fogo em 2 golpes? Também funciona passando a espada numa bola do cajado voando (difícil, mas possível).

## 00000000000000000000000. NOVO — tutorial e arte na arena/dungeon
- [ ] **Tutorial**: na 1ª entrada no santuário ele começa sozinho (ou portal TUTORIAL / menu inicial). O quadro dá para ler? Cada passo avança quando você faz o gesto? Algum passo trava (use pausa → Pular)?
- [ ] **Arena** e **menu inicial** com a sala do Treino (muralha, pilares, tochas).
- [ ] **Dungeon** com piso, paredes e tochas do KayKit: alguma parede faltando, peça flutuando ou buraco nas emendas? **FPS** numa sala grande com 6–9 inimigos.

## 0000000000000000000000. NOVO — IA em grupo
- [ ] Treino com 6 (ou Arena com inimigos físicos): eles se espalham na sua FRENTE em vez de empilhar? Algum ataca pelas costas?
- [ ] Perto dos pilares: eles contornam em vez de tropeçar e cair sozinhos?
- [ ] Raio/lança de gelo/bola do cajado: às vezes eles dão um passo para o lado (a bola teleguiada da mão não).
- [ ] Kamehameha carregando: quem está na frente às vezes sai do caminho.
- [ ] Finta (normal/difícil): às vezes ergue a clava e desiste. Fica justo ou irritante?

## 000000000000000000000. NOVO — goblin com modelo gerado por IA
- [ ] **Goblin** (Treino e Arena com inimigos físicos): modelo novo (orelhas, rosto, ombreira, faixas). Os pedaços acompanham o corpo? Algum buraco ou peça flutuando nas juntas (ombro, quadril, pescoço)?
- [ ] Arrancar a cabeça/braço: sai o pedaço certo do modelo?
- [ ] De perto: rosto e mãos aguentam ou precisam de retoque?
- [ ] FPS com 6 goblins físicos.

## 00000000000000000000. NOVO — esqueleto-goblin e trava de mão
- [ ] **Esqueleto** (Treino com 3 ou 6 inimigos): crânio de goblin de osso, costelas, ossos finos. Os braços, pernas e cabeça balançam soltos? Ficou bom ou exagerado?
- [ ] **Trava**: Kamehameha e bola de fogo NÃO soltam mais o corte da palma junto. Raízes também não.
- [ ] Segure a teia numa mão e use raio/soco/bola de fogo na outra: tem que funcionar.

## 0000000000000000000. 1º teste do visual (KayKit no Treino) — FPS aprovado, esqueleto KayKit reprovado
- [ ] Entre no **Treino**: sala nova (piso de pedra, muralha, pilares decorados, tochas, estandartes). Gostou do estilo? Fofinho demais?
- [ ] **FPS** (pausa → Mostrar FPS) parado olhando a sala inteira e com 6 inimigos físicos: anote média e pior.
- [ ] **Esqueleto** com o modelo do KayKit (capacete com chifres): os pedaços acompanham o corpo físico? Arranque um braço/cabeça: o pedaço sai certo? Remonta?
- [ ] Algo flutuando, atravessado ou com buraco (paredes, cantos, pilares)?

## 000000000000000000. NOVO — menus com o dedo, livro de magias, palma batida, bola teleguiada
Largue os dois controles.
- [ ] **Menus com o dedo**: pause (palma esquerda virada para você + pinça). Sai um raio de cada mão; a linha mirada acende; encoste o indicador no polegar para clicar. A pinça que abriu a pausa NÃO pode clicar sozinha em "Continuar".
- [ ] Menu inicial e árvore (pausa → "Árvore de habilidades"; "Fechar" no fim) também pelo dedo. O raio é estável ou treme demais?
- [ ] **Livro de magias** (pausa → "Livro de magias"): passe o dedo nas linhas e leia o passo a passo. Falta algo? Algum texto errado?
- [ ] **Raízes**: bata uma palma, separe e jogue as duas mãos rápido para baixo (sem agachar).
- [ ] **Kamehameha**: bata uma palma, mantenha os pulsos juntos (a esfera cresce), empurre.
- [ ] **Bola de fogo teleguiada**: jogue na direção de um inimigo longe com outro perto do lado: tem que ir no de longe.
- [ ] Bater palma sem querer (ex.: agarrar com as duas mãos) dispara algo?
- [ ] **Treino**: o goblin morre com magia/raio/soco (não só espada) e outro nasce em 3 s; estamina e mana nunca acabam.

## 00000000000000000. NOVO — teia por ganchos, embalo e mago sem controle
Largue os dois controles (mãos livres).
- [ ] **Ganchos**: olhe para o topo de um pilar/alto da parede: o ponto acende em ciano. Estique a mão aberta para ele e feche: sai a teia.
- [ ] Puxe a mão fechada para o peito: você voa até o ponto. **Pare de puxar ou abra a mão: continua voando em arco e cai** (antes parava no lugar). Enjoa?
- [ ] Dungeon: ganchos nos cantos e no centro de cada sala.
- [ ] **Raízes**: junte as duas mãos e encoste no chão (agache): raízes prendem os inimigos à sua frente (círculo de 4 m).
- [ ] **Kamehameha**: pulsos juntos, mãos abertas na altura da barriga — a esfera azul cresce; empurre as duas mãos para frente com força: raio azul grosso.
- [ ] **Bola de Fogo**: estique o punho fechado para frente, abra a mão: a bola cresce na palma (cheia em 1,5 s); jogue a mão para frente.
- [ ] Algum gesto dispara sem querer (ex.: Kamehameha quando junta as mãos para outra coisa)? O corte da palma dispara junto com o arremesso?

## 0000000000000000. NOVO — jogo mais dinâmico (arena maior, mais inimigos, recargas curtas)
- [ ] Arena bem maior (26 m) com 8 pilares; até 9 inimigos ao mesmo tempo; o chefe vem com 18 abates. Ficou divertido ou virou bagunça?
- [ ] Estamina/mana duram mais e voltam rápido; habilidades recarregam bem mais rápido. Dá para spammar demais (fácil demais)?
- [ ] Andar mais rápido (3,2 m/s): enjoa?
- [ ] **FPS** com 9 inimigos (pausa → Mostrar FPS), com e sem "Inimigos físicos (beta)".
- [ ] Dungeon: salas maiores e com mais inimigos.

## 000000000000000. NOVO — andar sem controle (sinal do Homem-Aranha)
Largue um ou os dois controles (a mão precisa estar livre).
- [ ] **Dash para trás**: faça o sinal do Homem-Aranha (indicador + mindinho esticados, médio e anelar dobrados) duas vezes seguidas, rápido.
- [ ] **Teia**: faça o sinal apontando para cima (teto, pilar ou parede): sai uma linha branca. Feche a mão e puxe em direção ao peito: você sobe/avança pela teia. Abra a mão: solta e cai.
- [ ] Na arena (céu aberto) a teia prende no ar a 12 m. Na dungeon prende no teto/paredes.
- [ ] O sinal é bem reconhecido? Dispara sem querer? Cansa? Dá enjoo ao subir/cair (diga se precisa de vinheta)?

## 00000000000000. NOVO — perfurar corrigido (goblin "grudado como fantasma")
- [ ] Estocada no goblin físico: entra e fica preso; puxando a espada para trás ele solta limpo (sem ficar grudado à distância).
- [ ] Mate-o espetado (ou estocada na cabeça): o corpo fica pendurado na lâmina; apontar para baixo ou puxar faz ele cair.
- [ ] Depois de soltar, bater de novo nele funciona normal (a lâmina volta a colidir em 0,5 s).

## 0000000000000. NOVO — esqueleto e orc físicos (A4)
- [ ] Treino → pausa → "Inimigos físicos no Treino" = 3: nascem goblin, **esqueleto** (branco, espada) e **orc** (grande, machado).
- [ ] **Esqueleto**: corte forte arranca braço/cabeça/perna; em 4 s a peça volta ("SE MONTOU"). Bater na peça caída no chão quebra ("QUEBROU!") e ela não volta. Sem cabeça ele continua lutando; cabeça quebrada mata. Maça faz muito dano nele.
- [ ] **Orc**: aguenta mais; só cai com golpe forte (ou machado/maça); espada quase não corta (armadura) — machado e estocada funcionam; não dá para agarrar; membros só saem com machado.
- [ ] Com a opção beta ligada, esqueletos e orcs da Dungeon/Arena também ficam físicos.

## 000000000000. NOVO — goblins físicos jogando de verdade (beta)
- [ ] Pausa → **"Goblins físicos na Arena/Dungeon (beta)" = sim**. Jogue uma Arena e uma sala da Dungeon: os goblins nascem com corpo físico (os outros inimigos continuam como antes).
- [ ] **Escudo**: bloquear o golpe da clava com o escudo parado = BLOQUEIO (sem dano); empurrar o escudo contra o golpe = APARADO e ele cambaleia ~1 s (aproveite para bater). Com a Postura de Contra ativa = CONTRA-ATAQUE.
- [ ] **Revide**: depois de bloqueado, às vezes ele ataca de novo rápido.
- [ ] Com pouca vida, às vezes ele recua.
- [ ] Bola de Fogo, raio, Lança de Gelo e habilidades das armas acertam o goblin físico; matar dá XP/ouro e a sala da Dungeon limpa normalmente.
- [ ] Comparando com o goblin antigo (opção desligada): **qual é mais divertido?** Essa resposta decide se seguimos para o A4 (todos os inimigos físicos).

## 00000000000. NOVO — goblin físico: IA e desempenho (passos A2/A3)
- [ ] **FPS (A2)**: pausa → "Goblins físicos no Treino" → 3 e depois 6; ligue "Mostrar FPS". Anote o número com 1, 3 e 6 (precisa 72).
- [ ] **Ataques variados**: além de cima, eles atacam da direita, da esquerda e com passo à frente. Dá para ler pela pose e aparar do lado certo?
- [ ] **Cerco**: no normal 3 atacam e os outros rondam a ~2 m andando de lado (como você pediu, menos parados).
- [ ] **Guarda**: às vezes, quando você golpeia rápido, ele ergue a clava na frente ("BLOQUEOU").
- [ ] **Desarmar**: golpe forte no braço da clava (ou na clava) faz ela cair ("DESARMOU!"); desarmado ele vem no soco.

## 0000000000. NOVO — correções do seu teste de 2026-10-18
- [ ] **Perfurar o goblin físico** (Treino): estocada com a ponta no tronco/cabeça agora crava?
- [ ] **Balanço rápido** (espada e lança): a arma não sai mais da mão? (também quando o controle sai da vista das câmeras)
- [ ] **Agarrar a cabeça/braço do goblin físico**: a mão fica desenhada na superfície, não dentro.
- [ ] **Arremessar**: agarre, levante e jogue com força — ele voa para onde você jogou e cai no chão.
- [ ] **IA**: com 4-5 goblins, 3 atacam juntos no normal (antes 2). Ficou melhor ou pesado demais?

## 000000000. NOVO — passo A1: goblin com CORPO FÍSICO (Treino)
No Treino, ao lado do boneco, nasce um goblin novo (corpo de 11 peças). Quando ele morre, outro nasce em 3 s.
- [ ] **Bater**: os membros reagem de verdade ao golpe (braço vai para trás, cabeça balança). Golpe forte no tronco/cabeça derruba ("CAIU!") e ele se levanta sozinho.
- [ ] **Aparar**: ele ergue a clava ~1 s e desce; bata/encoste a espada na clava = "APARADO".
- [ ] **Perfurar**: estocada no tronco: a lâmina entra e fica presa; ele perde a firmeza e você consegue arrastá-lo pela espada; puxe para trás para soltar. Na cabeça mata. Morto, o corpo fica pendurado na lâmina. **Parece o B&S?** (o corpo é pesado: a espada desce — use as duas mãos)
- [ ] **Agarrar**: feche a mão (ou grip) em qualquer parte dele (braço, cabeça, perna): sua mão fica na superfície e a parte vem junto; puxe forte para derrubá-lo; solte jogando para arremessar.
- [ ] **Arrancar**: corte rápido com o fio no braço, perna ou pescoço arranca a parte (sem o braço da clava ele não ataca; sem perna fica no chão).
- [ ] **FPS** com ele na tela (pausa → Mostrar FPS): precisa ficar em 72.

## 00000000. NOVO — 12ª leva (ajustes)
- [ ] **Recarga por mão**: soque com a esquerda e logo em seguida com a direita — as duas saem (cada mão tem a sua recarga).
- [ ] **Lança/espada no balanço mais rápido possível**: a arma não pode mais se afastar da mão. Ainda para na parede quando encosta?
- [ ] **Agarrar a cabeça**: sua mão fica por fora, encostada na cabeça/corpo, não dentro.

## 0000000. NOVO — 11ª leva (estilo Blade & Sorcery)
- [ ] **Arma no grip**: a espada fica na mão enquanto você segura o grip. Balance o mais forte que conseguir: ela NÃO pode sair. Solte o grip devagar: cai no chão. Solte com a mão rápida: arremessa. Mão vazia + grip: ela volta voando (soltar no caminho faz cair).
- [ ] **Agarrar**: com a mão livre, feche a mão encostando num goblin/slime/esqueleto: ele fica preso na mão, sem atacar. Abra a mão jogando o braço: ele voa e bate. Com controle: escudo nas costas + grip esquerdo perto do inimigo. Orc e chefe: "pesado demais".
- [ ] **Mão direita**: largue o controle direito — a espada cai e a mão direita ganha os mesmos poderes (raio apontando, soco, corte da palma, agarrar).
- [ ] **Raio**: atira 2x mais rápido e cada tiro tira metade. O ritmo ficou bom? Eletrocutar precisa de 5 seguidos.
- [ ] **Perfurar**: estocada leve já entra; forte entra fundo ("FUNDO!") e pode atravessar ("ATRAVESSOU!"). Encostar a ponta e empurrar devagar também fura. Estocada na cabeça do goblin mata. **O goblin morto fica pendurado na espada** e escorrega ao longo da lâmina; sacudir forte ou apontar para baixo até ele sair pela ponta solta. Parece o B&S?
- [ ] **Decapitar**: corte forte com o fio na cabeça do goblin/arqueiro arranca a cabeça (rola no chão). No orc só com machado.

## 000000. NOVO — mãos rastreadas visíveis
- [ ] Solte o controle esquerdo: a mão aparece (bolinhas nas articulações e ossos dos dedos) e acompanha os dedos. Faça punho, aponte, abra a mão.
- [ ] Solte o controle direito: a mão direita aparece também. Pegue de volta: some e a arma volta com a luva.
- [ ] A mão some/pisca quando sai da vista das câmeras? Atraso perceptível?

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
