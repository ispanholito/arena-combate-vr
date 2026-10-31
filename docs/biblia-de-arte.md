# Bíblia de arte — Arena VR (v2, 2026-10-31)

Regra de ouro: **tudo que aparece no jogo segue esta folha**. Modelo que não cabe aqui não entra, por mais bonito que seja sozinho.

**Mudança de 2026-10-31 (decisão do Pablo)**: inspirações **Sword Art Online, Dark Souls e Goblin Slayer**. Substitui a "fantasia colorida 8-21 anos" de 2026-10-29. Público agora **13 a 21 anos** (classificação esperada 12-16).

## 1. Tom: "fantasia sombria com toque de anime"
- **Cenário sombrio** (Dark Souls): cavernas e ruínas escuras, frias, solitárias; luz quente só de tocha e fogueira. A fogueira é o único lugar que parece seguro.
- **Inimigos ameaçadores** (Goblin Slayer): feios, sujos, espertos, armas improvisadas, emboscadas em grupo. Estilizados — nunca textura realista de pele.
- **Interface e magia limpas e brilhantes** (SAO): painéis flutuantes, barras de vida, golpes e habilidades com luz forte. O contraste com o cenário escuro é o que deixa o jogo legível no Quest.
- **Chefes** (Dark Souls + SAO): grandes, entrada marcante, sala própria. O Rei Goblin segue a linha do 1º chefe de SAO (sala do chefe, troca de arma na fase 2).
- **Fica de fora** de Goblin Slayer: violência sexual e gore explícito. Nenhuma loja aprova e o jogo não precisa disso.
- Humor (bacalhau, frigideira, "BONK!") **sai da demo**; no jogo completo, só em itens secretos/raros.

## 2. Formas
- **Silhueta primeiro**: cada inimigo reconhecível pela sombra a 10 m (goblin = baixo, corcunda, orelhas e nariz grandes, braços compridos; esqueleto = magro, crânio marcado; orc = ombros enormes, mandíbula; slime = gota).
- **Proporção realista de anime**: corpo próximo do real, rosto e olhos expressivos, mãos e armas levemente maiores (lê melhor na lente). **Nada de cabeção.**
- **Low-poly com chanfro**, sem detalhe fino que vire ruído no Quest 2.
- Orçamento: inimigo ≤ 6 mil triângulos, chefe ≤ 10 mil, arma ≤ 1,5 mil, peça de cenário ≤ 2 mil; 1 material por peça sempre que der.

## 3. Cor
- **Fundo escuro, frio e dessaturado** × **luz de tocha quente** × **interface/magia saturadas e brilhantes**.
- Paleta base:
  - Pedra: `#2E313D`, `#4B4F63` · Sombra: `#1A1A24` (puxando para o roxo) · Madeira velha: `#5C3D24` · Ferro: `#5A606E` · Ferrugem: `#8A4A2A` · Luz de tocha: `#FFB25A`
  - Goblin: pele `#6F8A3A` (verde sujo, oliva), trapos `#6B4A33`, olhos `#FFD23F` brilhando · Esqueleto: osso `#D8CDB0`, brasas nos olhos `#FF7A2A` · Orc: pele `#4E6B3A`, ferro `#4A5168`, dourado gasto `#C9A040` · Rei Goblin: coroa `#E0B040`, capa `#7A1E1E`
  - Interface (SAO): ciano `#5FE3FF`, branco, dourado; vida verde → amarelo → vermelho.
  - Raridade (já no jogo): Comum branco, Incomum verde, Raro azul, Épico roxo, Lendário laranja.
- **Vermelho saturado = perigo** (aviso de ataque, espinhos, círculo do chefe, fúria). Não usar em decoração.
- **Ciano = interagível / sistema** (pontos para pegar, interface, teletransporte).

## 4. Materiais e luz
- Textura pintada à mão, cores com gradiente leve, sujeira e desgaste nas bordas; **sem** mapa normal realista.
- **Emissivo** para o que o jogador precisa ler: olhos dos inimigos, magias, gemas, avisos, interface.
- Luz: 1 luz real por sala + tochas emissivas (FPS do Quest 2). Névoa escura azulada; fogueira com luz laranja forte.

## 5. Efeitos e sangue
- **Golpes e magias exagerados** (anime): rastro de luz na lâmina em golpe forte, faíscas grandes, anel de impacto, número de dano saltando.
- Cada magia tem cor fixa: fogo laranja, gelo azul-claro, raio branco-azulado, raízes verde, arcano roxo, aura vermelha.
- **Sangue estilizado e pouco**: respingo escuro (goblin verde-escuro, orc vermelho-escuro, esqueleto = pó de osso) que some em poucos segundos; sem órgãos, sem corte detalhado. **Desmembramento fica**: a peça cai com o corte tampado em cor escura lisa e some depois de um tempo.

## 6. Personagens (inimigos)
- Olhos que brilham no escuro (dá para ver o goblin antes do corpo).
- Equipamento conta a história e muda o jogo: capacete protege a cabeça, escudo racha, armadura muda o material.
- Goblins: armas improvisadas (clava com prego, faca enferrujada, arco torto), trapos, ossos e dentes pendurados.
- Elites: mesma forma, aura e anel na cor do tipo (já no jogo).
- Chefe: 1,5-2× o tamanho, coroa ou adorno marcante, cor própria; entrada com nome em letras grandes (já no jogo).

## 7. Armas
- Peso visível: cabo e guarda grossos, lâmina com bisel, desgaste nas armas achadas (cadáver, inimigos).
- Gema na cor da raridade; lendárias com forma única.

## 8. Mãos e interface
- Mãos: **luva estilizada** de couro escuro (skins por classe depois — ideia do Pablo).
- Interface estilo SAO: painéis translúcidos com borda ciano, fonte limpa, sempre alto contraste.

## 9. Fluxo de produção (custo zero)
1. **Inimigos e chefe**: referência desenhada no estilo desta folha (Gemini) → TRELLIS → `tools/modelos/glb2arena.js` → cortador por osso (`AiModelImport`). Sempre em pose T.
2. **Cenário**: KayKit escurecido (luz e cor) por enquanto; trocar por peças próprias ou outro pacote CC0 se destoar.
3. Todo modelo novo passa por: silhueta a 10 m, contraste com o cenário, triângulos, estação na Sala de Criação, teste no Quest.

## 10. Escopo de arte da demo
- **Prioridade 1**: Rei Goblin, goblin (refazer no estilo novo), esqueleto, orc, espada + escudo, mãos, iluminação da caverna.
- **Prioridade 2**: cadáver do aventureiro, estátua do fogo, porta pesada, coroa, estante, cela e corda.
- **Prioridade 3**: arqueiro, xamã, efeitos (rastro de lâmina, faíscas, sangue estilizado, aura).
- **Depois da demo**: as outras armas, outros chefes, biomas.

## Próximas decisões
- Goblin e Rei Goblin: fichas e prompts em `docs/pipeline-personagens.md` ("Fichas v2"). Goblins cruéis, roupa mínima (só tanga, sem nudez), Rei gordo e forte inspirado no conceito do Illfang (SAO) sem copiar.
