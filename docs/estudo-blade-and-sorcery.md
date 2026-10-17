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
1. **Fichas de ataque + dificuldade por agressividade** (IA) — maior ganho de sensação, pouco risco.
2. **Janela de combo, interromper com dano e revide** (IA).
3. **Partes do corpo**: membros ×0,5–0,6, cabeça ×2, golpe fatal na cabeça/pescoço com estocada funda.
4. **Material/armadura** nos inimigos + tabela tipo × material (corte/perfuração/contusão), com nocaute da maça.
5. **Zonas de dano** na arma (lâmina, ponta, pomo).
6. Depois: meia-espada, mão deslizando na lança, penetração com resistência.
