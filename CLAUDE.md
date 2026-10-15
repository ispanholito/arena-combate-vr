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
- **Postura de Contra** (substituiu o Turbilhão, que ficou sem graça): gatilho → por 2 s o próximo aparo/bloqueio vira contra-ataque devastador (quebra postura + crítico).
- **Lâmina de Vento** (ideia do Pablo): botão B → os próximos 2 golpes de espada lançam cortes de vento na direção do golpe.
- Passivas: Fio Afiado (+25% dano corpo a corpo), Passo Leve (esquiva -40% estamina), Investida em Cadeia (recarga 2x, +40% distância).

### Elementalista (cajado) — "o mago ficou muito bom" (Pablo)
- **Bola de Fogo**: segurar gatilho e arremessar (explode em área).
- **Lança de Gelo**: apontar o cajado + A (perfura até 3, deixa lento).
- **Raízes** (ideia do Pablo, substituiu a Rajada de Vento): segurar B, mirar no chão, soltar → raízes prendem inimigos 3 s. Fogo em enraizados = QUEIMA (+60%).
- Passivas: Combustão (explosão +60%), Fonte Arcana (mana 2x), Gelo Profundo (gelo congela totalmente).

### Futuras
Guardião (tanque, escudo), Clérigo (cura/suporte), Generalista, e outras (Lanceiro, Berserker, Assassino, Arqueiro, Invocador, Monge, Lâmina Arcana). Ver `docs/ideias-e-historico.md`.

## Poderes da mão esquerda livre (hand tracking) — decisão do Pablo
Mão direita segura a arma com o **controle**; mão esquerda **sem controle**, rastreada. Escudo na mão esquerda foi **descartado** ("a mão faz uma coisa, o escudo outra").
- **Apontar o indicador** → raio laser contínuo (gasta mana).
- **Punho fechado golpeando para baixo** → atordoa inimigos num raio de 3 m.
- **Mão aberta em movimento rápido** → corte de vento (combina com o da espada = corte duplo).
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
- **`Tests/Editor/` (asmdef `Arena.Core.Tests`)**: 47 testes EditMode. Rodar: `unity command run_tests --mode EditMode --filter Arena.Core.Tests --filter_type assembly`.
- **`Runtime/` (asmdef `Arena.Runtime`)**: componentes da Unity. `ArenaPlayer` (OVRCameraRig, locomoção, esquiva, Investida), `PlayerWeapon` (segue a mão com atraso pelo peso, golpe por varredura, arremessar/chamar de volta; `VrGripPitch` = inclinação da lâmina no controle, **ajustar no Quest**), `SkillCaster` (habilidades das 2 classes), `LeftHandPowers` (laser/soco/palma pela mão rastreada), inimigos em `Enemies/`, `ArenaGame` (ondas, chefe aos 12 abates, fogueira cura, morte), `ArenaHud`, `SkillTreeMenu`. Modelos e efeitos são primitivas criadas em código (placeholder).
- **Menu Arena > 3 - Criar cena da arena** (`ArenaSceneBuilder`, ou `ArenaSceneBuilder.CreateArenaSceneNoDialog()` pelo CLI) gera `Assets/Arena/Scenes/Arena.unity` (primeira cena do build).
- **Modo desktop** (Play sem headset): WASD anda, mouse olha (clique trava o cursor, Esc solta), botão esq = golpe, botão dir segurado = aparo, Shift/Espaço = esquiva, 1/2/3 = habilidades (2 e 3 seguram para Bola de Fogo/Raízes), G arremessa a arma, Q troca arma, Tab = árvore (Enter/E libera, R respec na fogueira, L = +1 nível de teste), Z/X/C = laser/soco/palma da mão esquerda.
- **VR** (mão esquerda livre): analógico direito move, clique curto = esquiva, segurar = árvore; A/B/gatilho/grip = habilidades da classe (como no protótipo). Com dois controles: analógico esquerdo move, X esquiva, Y árvore, clique esquerdo troca arma, direito gira 30°.
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
4. Física real de mãos e armas (a arma ainda atravessa os inimigos). Decisão pendente do Pablo: Auto Hand / Hurricane VR (pagos) ou Meta Interaction SDK (grátis, mais trabalho nosso). O `Arena.Core` não depende dessa escolha.
5. Trocar primitivas por modelos/animações e adicionar som (o protótipo web tinha sons sintetizados).
6. Rumo ao MVP: Lâmina + Elementalista, santuário simples, 1 dungeon com 2 andares + chefe, 4 inimigos, 6 armas, nível até 10, sem IA companheira e sem online.

## Fluxo de testes
- Rápido: **Quest Link** (cabo ou Air Link) + Play no editor, sem build.
- Real: **File > Build And Run** com o Quest no USB (modo desenvolvedor ligado).
