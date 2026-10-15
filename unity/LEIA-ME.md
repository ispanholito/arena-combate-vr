# Arena VR — projeto Unity

Primeiro passo na Unity: testar se o Quest 2 aceita **controle na mão direita + mão esquerda rastreada** (modo multimodal). Se funcionar, o jogo é construído em cima disso.

## 1. Instalar (só uma vez)
1. **Unity Hub**: https://unity.com/download (precisa de uma conta Unity, que é grátis).
2. No Hub, em **Installs > Install Editor**, instale a versão **Unity 6 LTS** mais recente. O SDK da Meta exige no mínimo 6000.0.66f2.
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
1. **Arena > 1 - Configurar projeto para Quest**.
2. **Meta > Tools > Project Setup Tool** → aba **Android** → **Fix All** e depois **Apply All**. Repita até não sobrar nada vermelho.
3. **Edit > Project Settings > XR Plug-in Management** → aba **Android** (ícone do robô) → marque **OpenXR** e, se aparecer, **Meta XR Feature Group**.
4. **Arena > 2 - Criar cena de teste multimodal**.

## 4. Testar no Quest
- Ligue o Quest no PC pelo cabo USB-C e aceite "Permitir depuração USB" dentro do óculos.
- **File > Build And Run**. A primeira compilação demora; as seguintes são mais rápidas.
- Dentro do óculos aparece um painel. Segure o controle **direito** e **solte o esquerdo** (deixe-o parado na mesa).
  - **MULTIMODAL: FUNCIONANDO** → deu certo.
  - Teste as poses da mão esquerda: **apontar** (aparece um laser), **punho** batendo para baixo, **mão aberta** em movimento rápido.

## Se der erro
Tire uma foto ou print do erro (Console do Unity, em vermelho) e mande na conversa.
