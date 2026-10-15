# Arena VR — projeto Unity

Primeiro passo na Unity: testar se o Quest 2 aceita **controle na mão direita + mão esquerda rastreada** (modo multimodal). Se funcionar, o jogo é construído em cima disso.

## 1. Instalar (só uma vez)
1. **Unity Hub**: https://unity.com/download (precisa de uma conta Unity, que é grátis).
2. No Hub, em **Installs > Install Editor**, instale o **Unity 6.3 LTS (6000.3.26f1)**. O SDK da Meta exige no mínimo 6000.0.66f2; evite a 6.6 (não é LTS).
   - Marque o módulo **Android Build Support**, junto com **OpenJDK** e **Android SDK & NDK Tools**.
3. **Modo desenvolvedor no Quest**:
   - Crie uma organização em https://developer.oculus.com/manage/organizations/create/ (é grátis).
   - No app **Meta Horizon** do celular, vá em Dispositivos > seu Quest > Configurações do headset > **Modo de desenvolvedor**: ligado.
4. (Opcional, recomendado) Instale o app **Meta Quest Link** para PC (no site da Meta, procure "Meta Quest Link app"). Com ele você testa direto do Unity apertando Play, sem gerar build.

## 2. Abrir o projeto
1. No Unity Hub: **Add > Add project from disk** → escolha a pasta `unity` dentro do repositório clonado (`arena-combate-vr/unity`).
2. Abra com a versão Unity 6 instalada. Na primeira vez demora (de 5 a 15 minutos), porque ele baixa o SDK da Meta e os pacotes.
3. Se aparecer um aviso sobre o *Input System* ou para reiniciar o editor: aceite (**Yes**).

## 3. Configurar (menu Arena no topo)
> Já feito neste repositório em 2026-10-15. Só repita se clonar do zero.

1. **Arena > 1 - Configurar projeto para Quest**: troca para Android, IL2CPP/ARM64, cor Linear, Input System, hand tracking "mãos + controles" e liga o OpenXR com os recursos da Meta e do XR Hands (Android e PC/Link). Se ele pedir, reinicie o editor.
2. **Meta > Tools > Project Setup Tool** → aba **Android** → **Fix All** (o Meta XR Simulator é opcional e pode falhar sem problema).
3. **Arena > 2 - Criar cena de teste multimodal**.

## 4. Testar no Quest
- Ligue o Quest no PC pelo cabo USB-C e aceite "Permitir depuração USB" dentro do óculos.
- **File > Build And Run**. A primeira compilação demora; as seguintes são mais rápidas.
- Dentro do óculos aparece um painel. Segure o controle **direito** e **solte o esquerdo** (deixe-o parado na mesa).
  - **MULTIMODAL: FUNCIONANDO** → deu certo.
  - Teste as poses da mão esquerda: **apontar** (aparece um laser), **punho** batendo para baixo, **mão aberta** em movimento rápido.

## Problemas já resolvidos
- **Safe Mode com erro CS1069 "AssetBundle" no Meta SDK**: na Unity 6.3 o AssetBundle virou módulo. O `manifest.json` já inclui `com.unity.modules.assetbundle`.
- **Instalador da Unity/Android termina sem fazer nada (código 2)**: falta espaço no disco do TEMP. Aponte TEMP/TMP para um disco com espaço e rode o instalador de novo.

## Se der erro
Tire uma foto ou print do erro (Console do Unity, em vermelho) e mande na conversa.
