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

## Ficha — Goblin (inimigo comum)
- **Papel**: o inimigo mais comum (arena e dungeon). Ataca com clava; pode ser desarmado, agarrado, espetado, desmembrado.
- **Silhueta**: baixo (~1,3 m), magro e nervoso, cabeça um pouco grande, **orelhas longas e pontudas** para os lados, nariz comprido e torto, dentes tortos para fora.
- **Paleta**: pele verde-oliva (#5E8A3A), olhos amarelos brilhantes (#FFE04A), roupa marrom-escura de couro gasto e trapos (#5A4028), detalhes de corda e osso.
- **Roupa**: tanga/saiote de couro, cinto de corda, uma ombreira de couro só num ombro, faixas de pano nos pulsos e canelas; pés descalços com unhas compridas. Nada solto e comprido (capa, cabelo longo) — atrapalha o corte por osso.
- **Arma (separada)**: clava de madeira nodosa com pregos/anel de ferro na ponta, ~50 cm.

### Prompt do goblin para o Gemini (frente)
```
stylized low-poly dark fantasy game character, hand-painted texture, flat neutral lighting, no cast shadows, plain white background, full body, front view, T-pose, arms straight out to the sides, legs slightly apart, symmetrical, no weapon in hands, no text.
A small wiry goblin warrior, about 1.3 meters tall, slightly large head, long pointed ears sticking out sideways, long crooked nose, crooked teeth showing, glowing yellow eyes, olive green skin. Wearing a worn dark brown leather loincloth, rope belt, a single leather shoulder pad on the left shoulder, cloth wraps on wrists and shins, barefoot with long toenails. Menacing but slightly comical, readable silhouette.
```

### Prompt do goblin (folha de giro)
```
character turnaround sheet of the same goblin: front view, side view and back view side by side, T-pose, same proportions and colors in all views, stylized low-poly dark fantasy, hand-painted texture, flat neutral lighting, plain white background, no weapon, no text.
```

### Prompt da clava (separada)
```
stylized low-poly dark fantasy game prop, hand-painted texture, flat neutral lighting, plain white background, single object centered, side view: a crude knotted wooden club about 50 cm long, thick head wrapped with an iron ring and a few rusty nails, leather-wrapped handle, no text.
```
