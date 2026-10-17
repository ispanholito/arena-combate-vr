# Estudo do Blade & Sorcery (cópia oficial da Steam) — 2026-10-16

**O que foi estudado**: só os dados que a Warpfrog deixa abertos para mods (`BladeAndSorcery_Data/StreamingAssets/Default/bas.jsondb`, um ZIP com 2.351 JSONs de configuração). Nada de código descompilado, modelos, sons ou animações. A cópia extraída fica fora do repositório (`E:\UnityCache\temp\bas_study`). **Regra**: daqui saem ideias e ordens de grandeza; nada de copiar arquivos ou valores em bloco para o nosso jogo — reimplementar do nosso jeito.

Categorias do banco: Damagers (42), DamageModifiers (23), Creatures (6), Brains (13 — IA), Items (587), Interactables (81 — pegadas), Skills (89), SkillTrees (6), Waves (145), LootTables (249), EnemyConfigs, Areas/AreaCollectionDungeons (dungeons por blocos), Stances, HandPoses…

---

## 1. Como o dano funciona lá

### Zonas de dano por arma
Cada arma tem **várias zonas**, cada uma com um tipo de dano:
- espada: **lâmina = corte**, **ponta = perfuração**, **cabo/pomo = contusão** (pancada com o cabo);
- machado: gume = corte de machado, cabo = contusão;
- maça: cabeça = contusão, cabo = contusão leve.

**Nós**: um segmento por arma (`WeaponStrike`) e um tipo só. → **Ideia**: zonas (lâmina/ponta/pomo), cada uma com seu tipo.

### Velocidade → dano
- Corte: 1× a 2 m/s até 2× a 20 m/s (curva suave), mínimo ~1,5–3 m/s para contar.
- Perfuração: 1× até **10×** a 20 m/s — estocada rápida é muito mais letal que corte rápido.
- Ângulo ruim (de lado/de chapa): vira **pancada** com 50% do dano e recuo (`badAngleBluntFallback`).
- Arremessado: perfuração ×1,5.

**Nós**: (8 + 2,5·v) até 9 m/s, fio ruim = 60%. Mesma ideia; o deles dá mais peso à estocada e o "de chapa" vira contusão.

### Tipo de dano × material (armadura)
| Tipo | Carne | Couro | Malha | Placa/metal |
|---|---|---|---|---|
| Corte | 1,0 | 0,25–0,5 | 0,5 (rápido demais: 0,1) | 0,2–0,5 |
| Corte de machado | 1,0 | 0,8 | 0,8 | 0,8 |
| Perfuração | 1,0 | 1,0 | 1,0 (exceto tier baixo) | 1,0 → 0,05 se muito rápido |
| Contusão | 1,0–1,2 | 0,5–0,6 | 0,5–0,6 | 0,5–0,75 |

Contusão **nocauteia** (atordoa) acima de ~4 m/s na carne, ~13–14 m/s com armadura.
**Isso dá papel para cada arma**: espada é ótima contra quem não tem armadura, machado ignora armadura, lança/adaga furam malha, maça nocauteia.

**Nós**: não temos armadura. → **Ideia forte**: dar "material" aos inimigos (goblin = carne/couro, esqueleto = osso, orc = placa, chefe = couro grosso) e uma tabela tipo × material. A maça e a lança ganham identidade de verdade.

### Partes do corpo
| Parte | Dano | Perfuração funda | Espetar |
|---|---|---|---|
| Cabeça | ×3 | ×32 (mata) | ×4 |
| Pescoço | ×1 (corte ×32 = degola) | ×32 | ×4 |
| Tronco | ×1 | ×2 | ×3 |
| Braços/pernas | ×0,5 | ×0,5 | ×0,5 |
| Mãos/pés | ×0,25 | ×0,25 | ×0,25 |

Humanos têm só **50 de vida**: o combate é letal e curto; o que dá duração é a defesa da IA, não a vida.

**Nós**: só cabeça ×1,5; resto ×1. → **Ideia**: membros ×0,5–0,6, cabeça ×2 e **golpe fatal** (estocada funda na cabeça/pescoço mata inimigo comum na hora). Vida dos inimigos comuns pode cair se a IA defender melhor (ver abaixo).

### Penetração
Lâmina entra com **amortecimento** (resiste mais ao entrar segurada, menos ao sair), pode **deslizar** no corte, detecta **espetar** (atravessou o corpo) com dano extra, e pressão (empurrar devagar também entra, com força que cai com a profundidade). **Nós**: espetar/cravar simplificado — dá para evoluir para "entra devagar com resistência" depois.

---

## 2. IA dos inimigos (Brain) — o mais útil

Fácil × Médio × Difícil **não mudam a vida**. Mudam:
| Parâmetro | Fácil | Médio | Difícil |
|---|---|---|---|
| **Inimigos atacando ao mesmo tempo** (`meleeMax`) | 1 | 2 | 3 |
| Tempo entre ataques | 2–3 s | 0,5–1 s | 0–0,5 s |
| Tempo para revidar depois de defender | 1,5–2 s | 0,5–1,2 s | 0,5–1 s |
| Dano que **cancela o ataque** dele (% da vida) | 5% | 8% | 10% |
| Andar de lado (troca a cada) | 2–6 s | 1–4 s | 0–2 s |
| Recuperação de reação | 0,9 | 1,0 | 1,1 |

Outros comportamentos (iguais em todas):
- **Defesa reativa**: o inimigo vê o golpe chegando (até 0,5 s antes, raio 5 m) e põe a arma no caminho (aparo físico); escudo dá revide 30% das vezes.
- **Janela depois de levar dano**: 0,75 s sem conseguir defender → dá para combar depois de acertar.
- **Revide (riposte)** depois de bloquear o jogador.
- **Esquiva** com recarga de 0,5–1 s.
- **Cambaleio**: leve e médio conforme o impacto.

**Nós**: todo goblin ataca quando quer (com o cerco pelos lados, os dois atacam juntos — já tinha anotado isso como risco); bloqueio por sorte (30%); cancela ataque com 30% de chance ao levar golpe; sem revide; sem janela pós-dano.

→ **Ideias, em ordem de impacto**:
1. **Fichas de ataque**: só N inimigos atacam ao mesmo tempo (fácil 1, normal 2, difícil 3); os outros cercam e esperam. Resolve o "cerco injusto".
2. **Dificuldade por agressividade**: fichas + intervalo entre ataques + tempo de aviso (já começamos com o tempo de aviso) em vez de só vida/dano.
3. **Interromper com dano**: golpe que tira ≥ 8% da vida cancela o ataque dele (troca o 30% aleatório).
4. **Janela de combo**: 0,75 s depois de levar dano o inimigo não bloqueia.
5. **Revide**: depois de bloquear você, ele contra-ataca rápido (0,5–1,2 s) — obriga a respeitar a defesa dele.
6. Bloqueio **reativo** (ele vê o golpe chegando e só bloqueia se der tempo) em vez de sorteio.

---

## 3. Armas e mãos

- Massas reais: adaga 0,65 kg, espada curta 1,25, machado curto 1,2, maça curta 1,5–2, machado de guerra 3,75, maça de guerra 6. **As nossas estão na mesma faixa** (espada 1,4, machado 3,5, maça 2,6, lança 2,2, adaga 0,5).
- Espada tem 3 pegadas: cabo, segunda mão e **meia-espada** (segurar a lâmina para estocar). Mão pode **deslizar** no cabo (lança/bastão).
- Pegada "pesada" e "leve" usam a mesma física; quem pesa é a massa e a força do personagem (força máxima de 3.000 N na mão).
- **Tier** da arma (0–4) escala o dano (0,5 → 1,1) — parecido com a nossa raridade.

→ Ideias: meia-espada e deslizar a mão na lança (fase de física das mãos); manter massas.

---

## 4. Progressão e mundo

- 5 árvores (Corpo, Fogo, Gravidade, Raio, Mente), tiers 0–3, liberadas com **cristais achados nas dungeons** (não por XP).
- Dungeons montadas por **blocos de salas prontos** (`AreaCollectionDungeons`, `Areas`) — parecido com o nosso gerador, mas com salas desenhadas à mão.
- Ondas da arena por arquivo (`Waves`, 145) e tabelas de inimigos por tier (`EnemyConfig_T0..T3`) — como a nossa profundidade do arcade.

**Crítica**: cristais por loot combinam com o modo arcade (achar cristal raro numa profundidade alta), mas o nosso pilar é "árvore por nível". Sugestão: manter pontos por nível e usar cristais só para **habilidades raras/supremas**.

---

## 5. Proposta de próxima leva (ordem sugerida)
Itens 1 e 2 FEITOS (7ª leva, `CombatAI`); itens 3 e 4 FEITOS (8ª leva, `DamageTypes` + `WeakPoints`). Bloqueio ainda é por sorte (falta o bloqueio reativo do item 6 da seção 2).
1. **Fichas de ataque + dificuldade por agressividade** (IA) — maior ganho de sensação, pouco risco.
2. **Janela de combo, interromper com dano e revide** (IA).
3. **Partes do corpo**: membros ×0,5–0,6, cabeça ×2, golpe fatal na cabeça/pescoço com estocada funda.
4. **Material/armadura** nos inimigos + tabela tipo × material (corte/perfuração/contusão), com nocaute da maça.
5. **Zonas de dano** na arma (lâmina, ponta, pomo).
6. Depois: meia-espada, mão deslizando na lança, penetração com resistência.

---

# Parte 2 — estudo a fundo (2026-10-17)

Categorias lidas além da parte 1: Statuss, Spells, Skills (89), Stances (repertório de ataques), HitReaction (tabela de reações), EnemyConfigs e LootConfigs por tier, Waves, AreaCollectionDungeons, GameModes, Shops, Liquids (poções) e todos os Damagers de arma. Ferramenta: `E:\UnityCache\temp\bas_tools\dump.js` (Node), porque alguns JSONs têm chaves repetidas que o PowerShell não lê.

## 6. Dano: o que faltou na parte 1
| Arma (dano) | Ganho com velocidade (2 → 20 m/s) | Base | Arremessada | Empurrão |
|---|---|---|---|---|
| Espada corte | ×1 → ×2 | 0,5–1,1 por tier | ×1 | 0 |
| Machado corte | ×1 → **×3** | 0,5–1,3 | ×1 | 50–100 |
| Maça | ×1 → **×5** | **1,5–3** | ×1 | 105–200 |
| Estocada espada | ×1 → **×10** | 1 | ×1,5 | 0 |
| Adaga/rapieira estocada | ×1 → ×5 | 1 | **×3** | 0 |
| Picareta de guerra | ×1 → ×10 | 1,25 | ×1,5 | 0 (desmembra fácil) |
| Escudo (pancada) | ×1 → ×2 | 0,5–0,85 | — | 100 |
| Chute | ×1 → ×5 | 1 | — | **400** |

**O que isso ensina**: cada arma ganha com a velocidade de um jeito — pesadas e estocadas premiam muito o golpe rápido, a espada é constante. **Adaga arremessada ×3** (arremesso vira estilo de jogo). Maça e chute **empurram** muito (derrubam). **Escudo é arma** (golpe de escudo).
**Nós**: mesma fórmula linear para todas, sem dano ao arremessar, sem golpe de escudo.

## 7. Reações a golpes (HitReaction) — tabela
Nível do impacto (0–4) × **o que o inimigo está fazendo** (parado, em guarda, cambaleando, atacando, pulando) × **parte do corpo** → reação: nada / cambaleio leve / médio / total / **derrubado**.
- Golpe **durante o ataque dele** derruba mais (contra-golpe).
- Inimigo **em guarda** resiste (cambaleia menos).
- **Cabeça e pernas** derrubam mais que o tronco; mão atingida solta a arma.
- Magia na cabeça/pescoço quase sempre derruba.

**Nós**: cambaleio só pelo impulso (`ImpactReaction`), sem considerar estado nem parte. → Ideia: tabela simples igual (3 níveis × 3 estados × 2 partes).

## 8. Repertório de ataques dos inimigos (Stances)
Cada estilo (uma mão, duas armas, escudo) tem listas: **ataques de abertura** (horizontal dos dois lados, de cima, estocada, ataque com **passo à frente** de 2–2,5 m), **ataques em sequência** (combo), **revides** (rápidos: o golpe chega a ~0,1–0,2 do tempo da animação) e **empurrão com escudo** (perto/médio/longe). Cada ataque tem **dificuldade** (estocadas só aparecem para IA difícil) e peso de sorteio.

**Nós**: goblin tem 1 ataque (de cima). → Ideia: 4–5 ataques por inimigo (lado esquerdo, direito, de cima, estocada, passo à frente) com direção de aparo diferente; estocada e passo só no normal/difícil; inimigo com escudo que empurra.

## 9. Magias (Spells, Skills, Status)
- **Encantar a arma** (segurar a magia sobre a lâmina): a arma ganha o elemento; cada golpe aplica o efeito e gasta a carga do encanto. Fogo passa **calor**; raio **eletrocuta** (dobro do tempo em quem usa **metal**); gravidade deixa sem peso.
- **Calor acumulado**: fogo soma calor até 100 → **queimando** (2 de dano a cada 0,5 s); o calor cai com o tempo.
- **Eletrocutado**: mais lento (jogador a 60%), não consegue agir direito; **raio em cadeia** pula entre inimigos (5 m).
- **Lança-chamas** (segurar carregado = jato), **bola de fogo**, **meteoro** (juntar as duas mãos = magia combinada), **bater o cajado no chão** (raio: atordoa 5 s num raio de 10 m), **cajado como mosquete** (atira).
- **Telecinese**: puxar/empurrar objetos e inimigos, girar, estrangular. **Tempo lento** (foco) como recurso.

**Nós**: bola de fogo, lança de gelo, raízes, raio da mão. → Ideias fortes: **encantar arma** (Elementalista + Lâmina = a futura Lâmina Arcana), **calor/queimando** e **eletrocutar** como status, **magia de duas mãos juntas**, **bater o cajado no chão**.

## 10. Habilidades (89 em 5 árvores, tiers 0–2) — nomes que viram ideia
Corpo: investida aérea, chute forte, segundo fôlego (cura ao quase morrer), intimidação, Juggernaut, furtivo (dano em quem não te viu), arremesso bumerangue (a arma volta). Fogo: ricochete, detonação remota, **soco foguete**, supernova, raio de plasma. Gravidade: amortecer queda, **martelo de gravidade**, pulo impulsionado, soco de força. Raio: **aparo elétrico** (o aparo eletrocuta), **roubo de arma** (o raio faz soltar a arma), arame elétrico. Mente: **foco em combate** (tempo lento), **revide temporal** (aparo deixa o tempo lento), estrangular, marionete.
Habilidades marcadas como "podem aparecer como recompensa" e um modo **roleta** (arena que sorteia habilidades) → combina com o nosso arcade.

## 11. Chefe Golem (só pelos nomes dos efeitos)
Pisão no chão, **pisão de fogo**, **lança-chamas** (dano a cada 0,3 s, empurra), explosão de gravidade, arame elétrico, **cristais brilhando no corpo** e no **chão da arena**, **tiro na cabeça**. Padrão: quebrar os cristais expõe o ponto fraco; a arena participa.
→ Ideia para um 2º chefe (ou fase do Rei Goblin): pontos fracos que brilham e precisam ser quebrados.

## 12. Progressão, loja, morte
- Presets de dificuldade: **penalidade de morte** = nenhuma / **perde o ouro** / **uma vida** (morte permanente); multiplicadores do dano que você toma e do dano que você causa.
- Campanha: **mapa com 3–5 dungeons novas por dia**, tiers de inimigo e de loot por local; **escolha do kit inicial**; **notas de história** escondidas; loja com **lojista**, estoque que **renova a cada 2 dias** e melhora conforme você avança, compra seus itens por **25%** do valor; **poção de cura** (+20 de 50 de vida).
- Loot separado por origem: **sala lateral**, **tesouro**, **inimigo**, **recompensa** do fim.
- Armadura do jogador (capacete, peito…) como loot.

## 13. Dungeon e ondas
- Dungeon = caminho de blocos: início → transição → sala grande → transição → fim (chefe), com % de inimigos e de loot por bloco e **alerta compartilhado** (a sala inteira acorda junto).
- Inimigos **patrulhando** até serem alertados (há habilidade de furtividade).
- Ondas: grupos com **pré-requisito** (o grupo B entra quando o A tem ≤ N vivos), **máximo vivo** (4), multiplicador de vida por grupo, temas (corpo a corpo, à distância, misto).

---

## 14. Backlog completo do que dá para usar (prioridade sugerida)
**A — alto impacto, cabe agora**
1. Curva de dano por arma (pesadas ×3–5, estocada ×10 com a velocidade) + **dano ao arremessar** (adaga ×3).
2. **Repertório de ataques** do goblin/esqueleto/orc (lados, de cima, estocada, passo à frente) com direção de aparo.
3. **Tabela de reações** (impacto × estado × parte): derrubar quem é atingido no meio do ataque; mão atingida solta a arma.
4. **Status**: calor → queimando, eletrocutado (mais forte em armadura), lento — base para as magias novas.
5. **Penalidade de morte** como opção: nenhuma / perde ouro / uma vida (combina com o arcade).
6. **Poção de cura** (loot/loja) e **lojista** no santuário (compra por 25%, vende armas e poções, estoque renova).

**B — próximas**
7. Encantar a arma (fogo/gelo/raio) — semente da Lâmina Arcana.
8. Magia de duas mãos juntas (meteoro / raio contínuo) e bater o cajado no chão.
9. Golpe de escudo (empurra, quebra postura) e inimigo com escudo.
10. Patrulha + alerta da sala + bônus de ataque furtivo.
11. Ondas com grupos e pré-requisito na arena; recompensa de habilidade (roleta) no arcade.
12. Chefe com pontos fracos que brilham (cristais).

**C — depois**
13. Armadura do jogador como loot. 14. Mapa com dungeons do dia. 15. Notas de história. 16. Telecinese/tempo lento. 17. Meia-espada e mão deslizando na lança.
