# Arena VR — contexto do projeto (leia primeiro)

Jogo autoral do **Pablo** (Joinville/SC, engenheiro de soluções, background em software e química). Ele conversa em português.
Este arquivo resume tudo que foi decidido até 2026-10-15 para quem (humano ou IA) continuar o projeto do zero.

## Como trabalhar com o Pablo
- Respostas **diretas e concisas**, com resultado pronto para usar.
- **Questione e critique todas as decisões dele**: se algo não for inteligente, for arriscado ou você discordar, diga com argumentos e proponha alternativa. Ele pediu isso explicitamente.
- Ele testa no **Meta Quest 2** (real). Ele joga Blade & Sorcery e se inspira em The King's Avatar, SAO, Dark and Darker, Souls.
- Antes de construir algo grande, confirme escopo; depois entregue algo testável.
- Mantenha este arquivo e `docs/` atualizados quando houver decisões novas.

## Visão do jogo
RPG de **combate físico em VR** (estilo Blade & Sorcery) com **classes e árvore de habilidades** (inspiração The King's Avatar/SAO), dungeons de fantasia RPG clássica (goblins, esqueletos, slimes), estética dark fantasy (Dark and Darker).
- Plataforma: **Meta Quest** (principal) + PC.
- **Solo primeiro**; coop de 1 a 4 jogadores depois; PvP muito depois. MMO de mundo aberto descartado.
- Tema: **fantasia RPG padrão**. Mitologia foi arquivada (pode voltar na história no futuro).
- Mecânicas primeiro; história depois.

## Pilares congelados (só mudar depois de testar jogando)
1. Combate físico: dano pela velocidade/ângulo do golpe, armas com peso.
2. Classes com árvore de habilidades desbloqueada por nível.
3. Classe **Generalista** (futura): acessa as árvores de todas as classes, também por nível (balanceamento a definir). Inspirada no protagonista de The King's Avatar.
4. Solo primeiro, bestiário RPG clássico.

## Sistemas decididos
- **Atributos automáticos** (sem distribuir pontos), crescem conforme o perfil da classe (Lâmina ganha muita Força; Elementalista pouca). **Peso da arma**: com pouca Força, arma pesada "atrasa" na mão (lag no follow da arma).
- Nível também aumenta vida, estamina e mana. Nível máximo 30 (MVP: 10).
- **Árvore**: 1 ponto por nível; nós ativos (habilidades) e passivos. Respec grátis na fogueira (planejado).
- **Loadout planejado**: 4 habilidades da classe + 1 da arma + 1 suprema.
- **Armas têm habilidades próprias** (comum = 1 ativa; raro/épico = ativa + passiva; lendário = únicas). Qualquer classe usa qualquer arma; proficiência da classe ativa os bônus da árvore.
- **Recursos**: estamina para todos (golpes, esquiva, bloqueio); mana para magia e poderes da mão.
- **Souls removido**, exceto: **aparo (parry)**, **barra de postura nos inimigos** (quebrou = janela de crítico; o jogador NÃO tem postura, bloqueio gasta estamina), **esquiva = dash** com vinheta (visual de rolamento só para quem vê de fora; câmera em 1ª pessoa nunca gira).
- **Morte**: perde só o XP do nível atual, nunca cai de nível. Equipamento largado/esquecido reaparece na fogueira ou na próxima fase. Gesto de chamar a arma de volta para a mão.
- **Fogueira** = checkpoint/cura.
- **Loot**: 5 raridades, 0-3 atributos aleatórios, ferreiro +1 a +5 (planejado).
- **Loop**: Santuário (hub) → dungeon de 2-3 andares → chefe → portal. Sessões de 20-40 min.
- **Solo**: escala de dificuldade + companheiros IA opcionais (planejado). Loot individual (prepara coop).

## Classes (estado atual)
### Lâmina (espada / machado pesado)
- **Investida Perfurante** (ideia do Pablo): aponte a espada para frente + botão A → avança ~4,5 m atravessando vários inimigos. Aprovada no Quest ("satisfatória, melhorar depois").
- **Escudo** (decisão de 2026-10-15): a Lâmina usa espada + escudo na mão esquerda (controle). Escudo parado = bloqueio sem dano (12 de estamina, +18 de postura no inimigo); empurrar o escudo contra o golpe = aparo. **Largar o controle esquerdo** guarda o escudo nas costas e libera a mão para os sinais. Crítica registrada: aproxima a Lâmina do Guardião planejado (escudo pesado, provocar, proteger aliados) — diferenciar quando o Guardião for feito.
- **Postura de Contra** (substituiu o Turbilhão, que ficou sem graça): gatilho → por 2 s o próximo aparo/bloqueio (espada ou escudo) vira contra-ataque de **dano alto (90)**. Desde 2026-10-15 não quebra mais a postura.
- **Lâmina de Vento**: corte quase 2x maior desde 2026-10-15 (2,2 m de largura, raio de acerto 1 m).
- **Lâmina de Vento** (ideia do Pablo): botão B → os próximos 2 golpes de espada lançam cortes de vento na direção do golpe.
- Passivas: Fio Afiado (+25% dano corpo a corpo), Passo Leve (esquiva -40% estamina), Investida em Cadeia (recarga 2x, +40% distância).

### Elementalista (cajado) — "o mago ficou muito bom" (Pablo)
- **Bola de Fogo**: segurar gatilho e arremessar (explode em área). 20 de mana, 18-30 de dano.
- **Lança de Gelo**: apontar o cajado + A (perfura até 3, deixa lento).
- **Raízes** (ideia do Pablo, substituiu a Rajada de Vento): segurar B, mirar no chão, soltar → raízes prendem inimigos 3 s. Fogo em enraizados = QUEIMA (+60%). 25 de mana.
- Passivas: Combustão (explosão +60%), Fonte Arcana (mana 2x), Gelo Profundo (gelo congela totalmente).

### Futuras
Guardião (tanque, escudo), Clérigo (cura/suporte), Generalista, e outras (Lanceiro, Berserker, Assassino, Arqueiro, Invocador, Monge, Lâmina Arcana). Ver `docs/ideias-e-historico.md`.

## Poderes da mão esquerda livre (hand tracking) — decisão do Pablo
Mão direita segura a arma com o **controle**; mão esquerda **sem controle**, rastreada. **Só funcionam com a mão livre** (nunca com o controle). A Lâmina larga o controle esquerdo (escudo vai para as costas) para usá-los. Os poderes saem direto pelo gesto (sem sinal — decisão de 2026-10-15 depois de testar):
- **Palma aberta parada** (menos de 1,2 m/s por 0,3 s) → **metralhadora** reta na direção dos dedos, saindo da ponta deles (substituiu o laser contínuo): 8 tiros/s, 3 de dano e 2,5 de mana por tiro. Fica bloqueada 0,5 s depois de um corte da palma, para não disparar no meio do corte. Apontar o indicador está livre para um poder futuro.
- **Punho golpeando para baixo** → atordoa inimigos num raio de 3 m (20 de mana).
- **Palma aberta em movimento rápido** (mais de 3,2 m/s) → corte de vento (15 de mana). A velocidade da mão é medida em relação ao corpo (andar/esquivar não conta).
- **Sinais de mão ficam isolados para habilidades futuras**: `Arena.Core/HandSigns.cs` (`HandSigns.Classify` reconhece aberta, punho, apontar, V, três dedos, chifres; `HandSignArming` = segurar o sinal 0,25 s arma um poder por 2,5 s). Testados, ainda não usados no jogo.
- Planejado: uma **classe só de mãos** (sem arma), depois — provavelmente o Monge.
Por enquanto disponíveis para qualquer classe (crítica pendente: no jogo final devem ter árvore própria ou pertencer a uma classe).

## Inimigos
Slime (se divide ao morrer), Goblin (ataque anunciado com clava, pode ser aparado), Rei Goblin (chefe: 2 fases, golpe em área com círculo de aviso, invoca slimes). Planejados: esqueletos (perdem membros e se remontam), orcs, lobos, morcegos, aranhas, mímicos.

## Estado atual
### Protótipo web (validado) — `index.html` na raiz deste repositório
- Jogar: https://ispanholito.github.io/arena-combate-vr/ (GitHub Pages, branch main, raiz).
- Three.js 0.160 + WebXR, arquivo único. Comandos no `README.md`.
- v0.1 e v0.2 testadas no Quest 2 e aprovadas. v0.3 adicionou mão esquerda, contra, raízes, vento por carga.
- **Problemas conhecidos da v0.3 (web)**:
  - No Quest 2, o navegador **não lê mão + controle ao mesmo tempo** (limitação do WebXR no Quest 2).
  - Com **dois controles**, o "corte da palma" dispara sem parar (no modo controle, "mão aberta" = nenhum botão apertado). Correção sugerida: exigir grip + gatilho, ou desativar no modo controle.
  - A arma atravessa os inimigos (sem colisão física).
- O protótipo web cumpriu o papel; o desenvolvimento segue na Unity.

### Unity — pasta `unity/`
- **Unity 6.3 LTS (6000.3.26f1)** — versão escolhida (mínimo 6000.0.66f2, exigido pelo Meta XR Core SDK 207.0.0). Evitar 6.6: não é LTS.
- Pacotes (`unity/Packages/manifest.json`): `com.meta.xr.sdk.core` 207.0.0 (registro `https://npm.developer.oculus.com`), OpenXR 1.17.1, XR Management 4.6.1, XR Hands 1.8.1, Input System 1.19.0, `com.unity.pipeline` (controle pelo CLI).
- **Unity 6.3 separou o AssetBundle num módulo**: sem `com.unity.modules.assetbundle` no manifest o Meta SDK não compila (CS1069 em OVRResources/OVRSceneLoader) e o projeto abre em Safe Mode.
- `Assets/Arena/Editor/ArenaSetup.cs`: menu **Arena > 1 - Configurar projeto para Quest** (Android, IL2CPP, ARM64, API 32, Linear, hand tracking "ControllersAndHands" via reflexão, features OpenXR) e **Arena > 2 - Criar cena de teste multimodal** (OVRCameraRig, liga "concurrent hands and controllers" no OVRManager via SerializedObject).
- `Assets/Arena/Scripts/ArenaMultimodalTest.cs`: painel de diagnóstico (controles, mãos, pose da mão esquerda, laser de teste) usando OVRInput + XR Hands.
- **Compila e roda no editor (2026-10-15).** Arena 1 e Arena 2 executados: target Android, IL2CPP/ARM64, API 32, Linear, OpenXR como loader (Android e Standalone) com Meta XR Feature, Meta Quest Support, XR Hands (HandTracking + MetaHandTrackingAim) e perfil Oculus Touch; `handTrackingSupport = ControllersAndHands`; cena `Assets/Arena/Scenes/TesteMultimodal.unity` com OVRManager "simultaneous hands and controllers" ligado. Project Setup Tool da Meta sem pendência obrigatória.
- `ArenaSetup.ConfigureProjectNoDialog()` e `ArenaSetup.CreateTestSceneNoDialog()` fazem o mesmo que os menus sem abrir diálogos — use-os pelo CLI (`unity command eval --code 'return ArenaSetup.ConfigureProjectNoDialog();'`). Diálogos modais travam a main thread e fazem os comandos do CLI expirarem ("Main thread operation timed out").
- Multimodal (mão + controle simultâneos): suportado oficialmente pela Meta no Quest 2/Pro/3/3S com Core SDK v74+. Ressalvas: o sistema pode achar que o controle foi largado quando o rastreamento falha; incompatível com body tracking.

### Arena jogável na Unity (2026-10-15) — porte do protótipo web
Três camadas em `unity/Assets/Arena/`:
- **`Core/` (asmdef `Arena.Core`, C# puro, sem UnityEngine)**: toda a regra do jogo, testável. `PlayerState` (classe, nível/XP, vida/estamina/mana, árvore, recargas, contra, cargas de vento, poderes da mão), `Progression` (XP = 40 + 30·nível, morte perde só o XP do nível, MVP nível 10), `ClassProfile` (força/vida/estamina/mana por nível e classe), `SkillTree` (habilidade inicial grátis, pré-requisitos, nível mínimo, respec), `WeaponDef` (peso → `FollowRate`), `Combat`/`Guard` (dano por velocidade da ponta e alinhamento do fio, aparo/bloqueio/contra), `Posture`, `EnemyDef`/`EnemyState` (Slime, Goblin, Rei Goblin), `WaveDirector`, `Geometry` (varredura lâmina × corpo). Números do protótipo v0.3, com duas mudanças propostas (revisar com o Pablo): vida/estamina/mana por nível agora dependem da classe (Lâmina +14 vida/nível, Elementalista +10, antes +12 para todos), e o dano corpo a corpo cai até 40% quando o golpe não é de fio (pilar "dano pelo ângulo do golpe"; estocada conta como fio).
- **`Tests/Editor/` (asmdef `Arena.Core.Tests`)**: 76 testes EditMode. Rodar: `unity command run_tests --mode EditMode --filter Arena.Core.Tests --filter_type assembly`.
- **`Runtime/` (asmdef `Arena.Runtime`)**: componentes da Unity. `ArenaPlayer` (OVRCameraRig, locomoção, esquiva, Investida), `PlayerWeapon` (segue a mão com atraso pelo peso, golpe por varredura, arremessar/chamar de volta; `VrGripPitch` = inclinação da lâmina no controle, **ajustar no Quest**), `SkillCaster` (habilidades das 2 classes), `LeftHandPowers` (laser/soco/palma pela mão rastreada), inimigos em `Enemies/`, `ArenaGame` (ondas, chefe aos 12 abates, fogueira cura, morte), `ArenaHud`, `SkillTreeMenu`. Modelos e efeitos são primitivas criadas em código (placeholder).
- **Menu Arena > 3 - Criar cena da arena** (`ArenaSceneBuilder`, ou `ArenaSceneBuilder.CreateArenaSceneNoDialog()` pelo CLI) gera `Assets/Arena/Scenes/Arena.unity` (primeira cena do build).
- **Modo desktop** (Play sem headset): WASD anda, mouse olha (clique trava o cursor, Esc solta), botão esq = golpe, botão dir = levanta o escudo (apertar logo antes do golpe = aparo; sem escudo = aparo com a arma), F = escudo nas costas/na mão, Shift/Espaço = esquiva, 1/2/3 = habilidades (2 e 3 seguram para Bola de Fogo/Raízes), G arremessa a arma, T estocada (segurar mantém cravada), Q troca arma, Tab = árvore (Enter/E libera, R respec na fogueira, L = +1 nível de teste), Z/X/C = metralhadora/soco/palma da mão esquerda (só com o escudo nas costas; no desktop não precisa de sinal).
- **VR** (mão esquerda livre): analógico direito move, clique curto = esquiva, segurar = árvore; **A + B juntos = árvore** (qualquer modo; por isso A e B sozinhos valem 0,12 s depois de apertar); A/B/gatilho/grip = habilidades da classe (como no protótipo). Com dois controles: analógico esquerdo move, X esquiva, Y árvore, clique esquerdo troca arma, direito gira 30°.
- **Controle "na mão"** usa `OVRInput.GetControllerIsInHandState` (sensor da Meta), com 0,2 s de estabilidade. Antes usava o rastreamento de mão do XR Hands, que no modo mão + controle às vezes marcava a mão como livre e o escudo sumia.
- **Altura no VR**: na partida e a cada recentralização (botão Meta segurado), se a cabeça estiver abaixo de 1,2 m o jogador sobe para a altura em pé medida (ou 1,65 m). Evita "entrar na terra" com o chão do Guardian mal calibrado.
- **Arma física (Fase 1, 2026-10-16)**: a arma é um Rigidbody (massa real: espada 1,4 kg, machado 3,5 kg) preso a um "pulso" cinemático que segue a mão por uma `ConfigurableJoint` com mola (técnica do Blade & Sorcery + guia de Evan Fletcher). Bate e para em inimigos (cápsula cinemática), chão e paredes; colisão contínua; física a 90 Hz. Rigidez = Força: frequência da mola = `FollowRate`, rotação 0,6x da posição (gira e desliza no obstáculo). Rigidez mínima para segurar o próprio peso (≤ 20° de queda, ≤ 6 cm). **Calibração medida**: a mola de rotação da junta segura ~4x menos que a conta (`WeaponPhysics.AngularDriveCalibration`). Presa a mais de 0,9 m por 0,6 s volta para a mão. Vibração proporcional ao impulso do impacto. Mão fantasma translúcida no controle quando a arma fica a mais de 8 cm dele. O dano continua pela varredura (`Geometry`). Próximas fases: 4 duas mãos, 4 duas mãos, 5 ragdoll ativo com modelos reais (projeto EggyStudio, MPL-2.0).
- **Inimigos reagindo à física (Fase 2, 2026-10-16)**: o impacto da arma (impulso da colisão; `ImpactReaction` no Core) empurra o inimigo (máx. 4 m/s; 0,15 s entre impactos, porque a lâmina deslizando gera vários eventos), inclina o corpo (pivô "Visual" com mola que endireita, até 34°), e golpe forte faz cambalear 0,3 s (1,2 s entre cambaleios; o chefe aguenta mais). Preso por raízes ou congelado: só inclina. Ao morrer vira ragdoll (`RagdollCorpse`): cada primitiva vira um corpo físico ligado à parte maior mais próxima por CharacterJoint, recebe o impulso do golpe final, pode ser empurrado pela arma e some em 5 s (no máximo 6 corpos). Slime e boneco não deixam corpo.
- **Cravar (Fase 3, 2026-10-16)**: só a espada, numa estocada (ponta a ≤ 30 cm do contato, ≥ 2 m/s, lâmina alinhada com o movimento ≥ 0,8; `StabRules` no Core). A velocidade é medida no passo de física antes do contato (`physTipVel`; a do quadro anterior mistura a rotação da arma e dava alinhamento errado). A lâmina ignora o colisor do inimigo e fica presa por uma ConfigurableJoint que só deixa deslizar ao longo dela (até 35 cm, atrito 80). Dano ×1,2 e sangramento de 4/s. O inimigo empalado não anda e é arrastado pela mão (até 1,5 m/s). Puxar para trás solta; mão a mais de 45 cm arranca (+8 de dano); se o inimigo morrer ou sumir, solta sozinha. 0,2 s de carência no início. Desktop: **T** = estocada (segurar T mantém cravada). `PlayerWeapon.DebugStab = true` registra no console os valores de cada contato.
- **Menu inicial** (`StartMenu`, 2026-10-15): escolhe a classe e o modo. **Arena** = ondas + Rei Goblin, personagem novo no nível 1. **Treino** = boneco parado no centro (`TrainingDummy`, `EnemyDef.Boneco`) que mostra DPS dos últimos 5 s, dano total e último golpe (`Core/DpsMeter`) e volta com a vida cheia após 3 s sem apanhar; jogador no nível 10 com 9 pontos livres, sem tomar dano e com estamina/mana voltando rápido. Usar o Treino para balancear. `ArenaGame.Blocked` (pausa ou menu) bloqueia habilidades e árvore.
- **Pausa**: botão Menu (☰) do controle esquerdo ou o gesto de menu da Meta com a mão esquerda (palma virada para você + pinça), ambos chegam como `OVRInput.Button.Start`; desktop P/Esc. Pausa sozinho ao tirar o headset ou abrir o menu do sistema (`OVRManager.HMDUnmounted`/`InputFocusLost`). Na pausa: A (ou Enter) continua, B (ou R) volta ao menu inicial. `ArenaGame.Paused` (timeScale 0) bloqueia habilidades, poderes e árvore.
- **Correção do aparo (2026-10-15)**: no Quest a clava andava 20-35 cm por quadro e atravessava a espada sem contato. Agora o contato é testado pelo caminho inteiro entre quadros (`Geometry.SweptSegmentsHit`), na ordem escudo → espada → corpo, e o goblin só causa dano se a clava encostar no corpo (antes era automático a menos de 1,15 m). O golpe foi inclinado para o centro (a clava saía 28 cm para o lado).
- **Verificado no editor (desktop)**: golpe tira vida e postura; goblin ataca, bloqueio/aparo/contra-ataque funcionam; Investida atravessa vários; slime se divide; Rei Goblin chega, entra em fúria (<50%), invoca slimes e faz o golpe em área; Bola de Fogo com QUEIMA em enraizados; Lança de Gelo perfura; Raízes prendem; morte volta à fogueira. **Ainda não testado no Quest.**
- Testar no editor pelo CLI: antes do Play rode `unity command set_autotick` e, já no Play, `eval "UnityEngine.Application.runInBackground = true;"` (senão o jogo congela no frame 1 com o editor sem foco). Teclas simuladas com `simulate_key` só funcionam para teclas seguradas (WASD); toques únicos (1/2/3) não chegam com o editor sem foco — teste habilidades chamando os métodos por reflexão.

## Ferramentas instaladas no PC do Pablo (Windows)
- **Disco C: está lotado — use só o E:.** Repositório em `E:\projects\arena-combate-vr`; Unity e Hub em `E:\Nova pasta\`. Variáveis de usuário apontam tudo para `E:\UnityCache`: `TEMP`/`TMP` (`temp`), `UPM_CACHE_ROOT` (`upm`), `UPM_NPM_CACHE_PATH` (`npm`), `GRADLE_USER_HOME` (`gradle`). Instaladores NSIS elevados leem o TEMP do registro; com C: cheio eles abortam com código 2 sem mensagem.
- **Unity CLI v1.0.0-beta.13** (comando `unity`), com a skill instalada no Claude Code (`unity skill install claude-code`). Use `unity skill show` para o guia completo.
- Comandos úteis: `unity status` (editores abertos), `unity open unity` (abre o projeto), `unity recompile` (recompila e lista erros de compilação), `unity command ...` (executa comandos no editor aberto), `unity test`, `unity build`.
- Para o agente controlar o editor aberto é preciso o pacote experimental **com.unity.pipeline** no projeto (ver `unity pipeline --help`). Instale-o assim que o projeto abrir pela primeira vez.

## Próximos passos
1. ~~Instalar Unity e abrir `unity/`~~ (feito).
2. Rodar o teste multimodal no Quest 2 → precisa aparecer **MULTIMODAL: FUNCIONANDO**.
3. ~~Portar classes, árvore, poderes da mão, inimigos e chefe~~ (feito na arena, ver acima). **Testar a arena no Quest** e ajustar: `VrGripPitch`, posição do HUD (hoje preso à cabeça; o protótipo usava o pulso), alcance/dano.
4. Física das armas: **decidido fazer a nossa** (2026-10-16), em 5 fases (ver "Arma física"). Fase 1 feita. Hurricane VR (US$ 69,99) fica como plano B se o cravar ou as poses de mão não ficarem bons; risco: só testado oficialmente na Unity 2022.3 e a doc ainda usa o plugin Oculus descontinuado.
5. Trocar primitivas por modelos/animações e adicionar som (o protótipo web tinha sons sintetizados).
6. Rumo ao MVP: Lâmina + Elementalista, santuário simples, 1 dungeon com 2 andares + chefe, 4 inimigos, 6 armas, nível até 10, sem IA companheira e sem online.

## Fluxo de testes
- Rápido: **Quest Link** (cabo ou Air Link) + Play no editor, sem build.
- Real: **File > Build And Run** com o Quest no USB (modo desenvolvedor ligado).
