# Arena de Combate VR — protótipo v0.1

Protótipo de combate físico para Meta Quest (WebXR) e PC. Feito em Three.js, num único arquivo, sem build.

## Jogar
- **Quest:** abra o link do GitHub Pages no navegador do Quest e toque em **ENTER VR**.
- **PC:** abra o mesmo link no Chrome/Edge e clique em **Jogar no PC**.

## Controles
| Ação | PC | Quest |
|---|---|---|
| Andar | WASD | analógico esquerdo |
| Girar | mouse | analógico direito (giro em passos) |
| Golpe | clique esquerdo | balance o braço direito |
| Aparar | segurar clique direito | coloque a arma na frente da clava, em movimento |
| Esquiva | Shift | A ou clique no analógico esquerdo |
| Arremessar / chamar arma | E | B arremessa · gatilho direito chama de volta |
| Trocar espada/machado | Q | X |
| +1 nível (teste) | L | Y |

## O que está sendo testado
- Peso da arma (machado "atrasa" na mão com Força baixa)
- Aparo + barra de postura dos inimigos
- Esquiva rápida com vinheta
- Slime que se divide, goblin com ataque anunciado
- XP/nível até 10; morrer perde só o XP do nível atual

## Rodar localmente (opcional)
```
npx serve .
```
WebXR no Quest exige HTTPS — por isso o GitHub Pages.
