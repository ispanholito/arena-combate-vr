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
- **Unity 6 LTS** (mínimo **6000.0.66f2**, exigido pelo Meta XR Core SDK 207.0.0).
- Pacotes (`unity/Packages/manifest.json`): `com.meta.xr.sdk.core` 207.0.0 (registro `https://npm.developer.oculus.com`), OpenXR 1.17.1, XR Management 4.6.1, XR Hands 1.8.1, Input System 1.19.0.
- `Assets/Arena/Editor/ArenaSetup.cs`: menu **Arena > 1 - Configurar projeto para Quest** (Android, IL2CPP, ARM64, API 32, Linear, hand tracking "ControllersAndHands" via reflexão, features OpenXR) e **Arena > 2 - Criar cena de teste multimodal** (OVRCameraRig, liga "concurrent hands and controllers" no OVRManager via SerializedObject).
- `Assets/Arena/Scripts/ArenaMultimodalTest.cs`: painel de diagnóstico (controles, mãos, pose da mão esquerda, laser de teste) usando OVRInput + XR Hands.
- **Nada disso foi compilado ainda** (escrito sem acesso à Unity). Esperar ajustes na primeira abertura.
- Multimodal (mão + controle simultâneos): suportado oficialmente pela Meta no Quest 2/Pro/3/3S com Core SDK v74+. Ressalvas: o sistema pode achar que o controle foi largado quando o rastreamento falha; incompatível com body tracking.

## Próximos passos
1. Instalar Unity e abrir `unity/` (passo a passo em `unity/LEIA-ME.md`).
2. Rodar o teste multimodal no Quest 2 → precisa aparecer **MULTIMODAL: FUNCIONANDO**.
3. Portar o combate do protótipo web para Unity com física real (sugestão: avaliar Auto Hand / Hurricane VR para física de mãos e armas).
4. Portar classes, árvore, poderes da mão, inimigos e chefe.
5. Rumo ao MVP: Lâmina + Elementalista, santuário simples, 1 dungeon com 2 andares + chefe, 4 inimigos, 6 armas, nível até 10, sem IA companheira e sem online.

## Fluxo de testes
- Rápido: **Quest Link** (cabo ou Air Link) + Play no editor, sem build.
- Real: **File > Build And Run** com o Quest no USB (modo desenvolvedor ligado).
