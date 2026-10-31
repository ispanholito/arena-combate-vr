# Pipeline de personagens 3D (custo zero) — passo a passo

Decidido em 2026-10-28. Ordem: testar 1 personagem (goblin) pela demo online ANTES de instalar qualquer coisa.

## Regras do nosso jogo (valem para todo personagem)
- Engine: Unity 6.3. Headset: Meta Quest 2.
- **Orçamento**: inimigo comum 3–6 mil triângulos; chefe até 15 mil. **1 textura** por personagem (até 1024×1024; chefe 2048).
- **Escala**: 1 unidade = 1 metro; de pé no eixo Y, olhando para +Z, pés no chão (Y = 0). Goblin ≈ 1,3 m.
- **Inimigos são corpos físicos** (11 partes: cabeça, tronco, bacia, braço/antebraço e coxa/canela de cada lado), movidos por poses — **não usam animação**. Do Mixamo só precisamos do **rig** (o Claude corta o modelo por osso).
- **Arma em arquivo separado** (o goblin solta a clava quando é desarmado).
- **Estilo único**: sempre o mesmo trecho de estilo no prompt; iluminação neutra (sem sombra pintada na textura).
- **Disco C: lotado**: instalar e guardar tudo no **E:**.
- Arquivos para o Claude: `E:\projects\arena-combate-vr\arte\<personagem>\` (crie a pasta).

## Passo 1 — Imagem de referência (Gemini)
Trecho de estilo fixo (repetir em todo personagem):
`stylized low-poly dark fantasy game character, hand-painted texture, flat neutral lighting, no cast shadows, plain white background, full body, front view, T-pose, arms straight out to the sides, legs slightly apart, symmetrical, no weapon in hands, no text`

Peça 2 imagens: a de frente (principal) e uma "character turnaround sheet" (frente, lado, costas). Gere várias e escolha a mais limpa (corpo inteiro, mãos abertas, nada cortado).

## Passo 2 — Modelo 3D (demo do Hunyuan3D 2.1, sem instalar)
1. Abra https://huggingface.co/spaces/tencent/Hunyuan3D-2.1 (pode ter fila).
2. Suba a imagem de frente; gere a forma e depois a textura.
3. Gere 3–4 variações; escolha a melhor e **baixe em .glb**.
4. Salve em `E:\projects\arena-combate-vr\arte\goblin\` e avise o Claude. **Mande cru**, sem Blender: o Claude encaixa no corpo físico e você vê no Quest se o estilo funciona de perto.

## O que usamos de verdade (2026-10-28, goblin)
- Hunyuan3D estourou a cota grátis; **TRELLIS.2** (https://huggingface.co/spaces/microsoft/TRELLIS.2) gerou bem: Resolution 1024, Decimation Target 100000, Texture Size 2048 → Extract GLB.
- Sem Blender: `tools/modelos/` (rodar `npm install` uma vez) → `node tools/modelos/glb2arena.js arte/goblin/goblin_trellis.glb arte/goblin/goblin 6000 1024` → na Unity, menu **Arena > 5** (ou `AiModelImport.Goblin()`). O corte por osso é por posição (pose T), sem rig/Mixamo.
- Blender e Mixamo ficam só para retoque manual.

## Passo 3 — Limpeza (Blender) — só se precisar de retoque
1. Instale o Blender (grátis; instalador → pasta no E:).
2. O Claude escreve o script em lote: decimate até o orçamento, escala em metros, pés em Y = 0, frente em +Z, nomes padronizados, exporta FBX.
3. Ajuste à mão o que a IA errar (mãos, rosto).

## Passo 4 — Rig (Mixamo)
1. https://www.mixamo.com (conta Adobe grátis) → Upload Character → o FBX limpo.
2. Posicione os marcadores (queixo, pulsos, cotovelos, joelhos, virilha) → Next.
3. Baixe **só o personagem riggado** (FBX for Unity, "T-pose", com skin). Animações: não precisa.
4. Salve em `arte\<personagem>\` e avise o Claude (ele adapta o cortador por osso ao rig do Mixamo).

## Passo 5 — Hunyuan3D local na RX 7700 XT (opcional, só se a demo for lenta demais)
Projeto da comunidade com AMD (experimental na 7700 XT): https://github.com/VladimirTalyzin/hunyuan3d-2.1-mac-rocm
1. Driver AMD Adrenalin atualizado. Python 3.12 (instalação personalizada em `E:\Python312`, marcar "Add to PATH") e Git.
2. Caches no E: (PowerShell normal, uma vez; depois feche e abra o terminal):
   ```
   setx HF_HOME E:\UnityCache\hf
   setx PIP_CACHE_DIR E:\UnityCache\pip
   ```
3. PowerShell **como admin** (dá mais tempo à GPU antes do Windows resetar; mexe no registro; reiniciar depois):
   ```
   New-ItemProperty -Path 'HKLM:\SYSTEM\CurrentControlSet\Control\GraphicsDrivers' -Name TdrDelay -PropertyType DWord -Value 60 -Force
   ```
4. Baixar e instalar (no E:):
   ```
   cd E:\
   git clone https://github.com/VladimirTalyzin/hunyuan3d-2.1-mac-rocm.git
   cd hunyuan3d-2.1-mac-rocm
   powershell -ExecutionPolicy Bypass -File scripts\install_rocm_windows.ps1
   ```
   Se pedir o PyTorch (gfx1101), use o índice da AMD (não o do pytorch.org):
   ```
   python -m pip install --index-url https://nightly.repo.amd.com/rocm/whl-next/ "torch[device-gfx1101]" "torchvision[device-gfx1101]" torchaudio
   ```
5. `python scripts\doctor.py` (se "WRONG RESULTS": reiniciar). Rodar: `powershell -ExecutionPolicy Bypass -File launch.ps1` → http://127.0.0.1:7860. Na textura, preset "Safe"; não definir `MIOPEN_FIND_MODE=FAST`.

Plano B: Google Colab grátis (NVIDIA), créditos diários da Meshy/Tripo para os principais, ou Ubuntu em dual boot.

## Avisos
- Licença do Hunyuan3D (Tencent) tem restrição por região e porte de empresa: ler antes de vender.
- IA 3D erra mãos, rosto e topologia; inimigos vistos de perto em VR podem precisar de ajuste.

---

## Fichas v2 (2026-10-31) — fantasia sombria com toque de anime
Decisões do Pablo: goblins **cruéis** (Goblin Slayer: pequenos, feios, sujos, espertos); roupa **mínima** (só tanga e trapos), no limite do que um público de 13+ aceita — **sem nudez**: a tanga sempre cobre; Rei Goblin **gordo e forte**, inspirado no conceito do 1º chefe de SAO (Illfang, o Senhor Kobold). **Só o conceito**: não usar o nome nem copiar o design (direito autoral). As fichas antigas (2026-10-28) foram substituídas.

Trecho de estilo fixo (repetir em todo personagem):
`stylized low-poly dark fantasy game character with a subtle anime influence, hand-painted texture with dirt and wear, flat neutral lighting, no cast shadows, plain white background, full body, front view, T-pose, arms straight out to the sides, legs slightly apart, symmetrical, no weapon in hands, no text`

Dica: o Gemini às vezes recusa "quase nu"; "loincloth and leather scraps" passa. Gere várias e escolha a mais limpa (corpo inteiro, mãos abertas, nada cortado, tanga cobrindo).

## Ficha — Goblin (inimigo comum)
- **Papel**: o inimigo mais comum. Clava; pode ser desarmado, agarrado, desmembrado.
- **Silhueta**: baixo (~1,2 m), magro, costas curvadas, **braços compridos**, cabeça grande, orelhas pontudas para trás, nariz adunco.
- **Rosto cruel**: sorriso largo de dentes pontudos e tortos, olhos amarelos fundos e pequenos (brilham no escuro), testa franzida, cicatrizes.
- **Paleta**: pele verde-oliva suja (#6F8A3A), trapos marrons (#6B4A33), olhos #FFD23F, sujeira e manchas escuras.
- **Roupa (mínima)**: só uma tanga de couro rasgado com cinto de corda, um colar de dentes e ossos pequenos, faixas sujas nos pulsos. Descalço, unhas compridas e pretas. Nada comprido e solto (atrapalha o corte por osso).
- **Arma (separada)**: clava torta com pregos enferrujados, ~50 cm.

### Prompt do goblin (frente)
```
stylized low-poly dark fantasy game character with a subtle anime influence, hand-painted texture with dirt and wear, flat neutral lighting, no cast shadows, plain white background, full body, front view, T-pose, arms straight out to the sides, legs slightly apart, symmetrical, no weapon in hands, no text.
A small cruel goblin, about 1.2 meters tall, skinny with a hunched back and long arms, large head, pointed ears swept backwards, hooked nose, wide sadistic grin full of sharp crooked teeth, small sunken glowing yellow eyes, furrowed brow, scars on the face and body, dirty olive green skin with dark grime. Wearing only a ragged dark leather loincloth with a rope belt, a necklace of small teeth and bones, dirty cloth wraps on the wrists, barefoot with long black toenails. Menacing and nasty, not comical, readable silhouette.
```

### Prompt do goblin (folha de giro)
```
character turnaround sheet of the same goblin: front view, side view and back view side by side, T-pose, same proportions and colors in all views, stylized low-poly dark fantasy game character with a subtle anime influence, hand-painted texture with dirt and wear, flat neutral lighting, no cast shadows, plain white background, no weapon in hands, no text.
```

### Prompt da clava (separada)
```
stylized low-poly dark fantasy game prop, hand-painted texture with dirt and wear, flat neutral lighting, plain white background, single object centered, side view: a crude crooked wooden club about 50 cm long, thick head studded with rusty nails, dark stains, leather-wrapped handle, no text.
```

## Ficha — Rei Goblin (chefe da demo)
- **Papel**: chefe da demo. Fase 1 = clava grande + porta velha com chapas de ferro como escudo; fase 2 = duas espadas. Entra curvado pela porta.
- **Silhueta**: **2,6-3 m** (≈ 2× o goblin), **gordo e forte**: barriga enorme e dura, peito e ombros largos, braços muito grossos, pernas curtas e grossas. Lê como "lutador de sumô brutal".
- **Rosto**: mesmo DNA do goblin (orelhas para trás, nariz adunco, dentes pontudos) com mandíbula larga, presas inferiores, cicatriz atravessando um olho, olhos amarelos que ficam vermelhos na fúria (já no jogo).
- **Paleta**: pele verde-oliva mais escura (#5A7430), coroa de ouro gasto (#E0B040), capa vermelho-escura (#7A1E1E), couro escuro, ferro enferrujado.
- **Roupa**: coroa torta de ouro com pontas irregulares, tanga grande de couro com cinto largo de fivela de crânio, ombreiras de ferro amassadas, uma pele de animal curta no ombro (capa curta — capa longa atrapalha o corte por osso; se o Gemini fizer longa, cortamos depois), braceletes de ferro, cicatrizes de batalha.
- **Armas (separadas)**: clava grande (~1,2 m), porta de madeira com chapas de ferro (escudo), duas espadas largas de goblin (cimitarras toscas).

### Prompt do Rei Goblin (frente)
```
stylized low-poly dark fantasy game character with a subtle anime influence, hand-painted texture with dirt and wear, flat neutral lighting, no cast shadows, plain white background, full body, front view, T-pose, arms straight out to the sides, legs slightly apart, symmetrical, no weapon in hands, no text.
A huge goblin king boss, about 2.8 meters tall, fat and very strong like a brutal sumo wrestler: enormous hard belly, broad chest and shoulders, very thick muscular arms, short thick legs. Goblin face with pointed ears swept backwards, hooked nose, wide jaw with lower tusks and sharp crooked teeth, a scar across one eye, glowing yellow eyes, cruel arrogant expression, darker olive green skin. Wearing a crooked worn gold crown with irregular spikes, a large dark leather loincloth with a wide belt and a skull buckle, dented iron shoulder plates, a short animal fur mantle over the shoulders, iron bracers, battle scars all over. Intimidating final boss, readable silhouette.
```

### Prompt das armas do Rei (separadas, uma por imagem)
```
stylized low-poly dark fantasy game prop, hand-painted texture with dirt and wear, flat neutral lighting, plain white background, single object centered, no text: (1) a huge crude wooden club about 1.2 meters long with iron spikes; (2) an old wooden door used as a shield, reinforced with dented iron plates and rivets, a crude leather handle; (3) a crude wide goblin scimitar about 1 meter long, chipped rusty blade, bone handle.
```

## Depois de escolher as imagens
Salve em `arte/goblin/` e `arte/rei-goblin/` e passe no TRELLIS.2 (passo 2). Mande as imagens para o Claude antes de gerar o 3D se quiser uma segunda opinião.
