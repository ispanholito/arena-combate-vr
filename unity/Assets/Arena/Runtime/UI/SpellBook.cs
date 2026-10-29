using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    // Livro de magias (pedido do Pablo, 2026-10-28): explica como fazer cada poder e habilidade. Abre pela pausa
    // ("Livro de magias"). A linha escolhida mostra o passo a passo. Aponte/pinça ou cima/baixo + A; B ou "Fechar" volta
    // para a pausa. Ao adicionar um poder novo, coloque a entrada aqui.
    [RequireComponent(typeof(ArenaPlayer))]
    public sealed class SpellBook : MonoBehaviour
    {
        public static bool IsOpen { get; private set; }
        static int openedFrame = -1;
        public static void Open() { IsOpen = true; openedFrame = Time.frameCount; }
        public static int ClosedFrame { get; private set; } = -1;   // a pausa ignora o B do mesmo quadro em que o livro fechou

        static readonly (string group, string title, string how)[] Entries =
        {
            ("Sem controle", "Raio do Freeza", "Aponte o indicador (os outros dedos fechados). Um ponto vermelho carrega na ponta do dedo e o raio sai; mantenha apontado para repetir. 3 de mana, atravessa 2 inimigos; 5 seguidos eletrocutam."),
            ("Sem controle", "Soco", "Feche o punho e soque rápido para frente. Acerta 1 inimigo até 1 m além da mão. 8 de mana; cada mão tem a sua recarga."),
            ("Sem controle", "Corte da palma", "Mão aberta, passe-a rápido no ar, como um tapa. Solta um corte de vento. 10 de mana."),
            ("Sem controle", "Bola de fogo", "Estique o punho fechado para frente e ABRA a mão: a bola cresce na palma (cheia em 1,5 s). Jogue a mão para frente. É teleguiada: vai no inimigo mais perto da direção em que você jogou, mesmo que ele esteja longe. 18 de mana."),
            ("Sem controle", "Raízes", "Bata uma PALMA, separe as mãos e jogue as duas rápido para baixo. Raízes prendem por 3 s os inimigos num círculo de 4 m à sua frente. Fogo em inimigo enraizado queima mais. 25 de mana, recarga de 6 s."),
            ("Sem controle", "Kamehameha", "Bata uma PALMA e mantenha os PULSOS JUNTOS, mãos abertas na altura da barriga: a esfera azul cresce. Empurre as duas mãos para frente com força: um raio grosso que atravessa todos. 35 de mana, recarga de 6 s."),
            ("Sem controle", "Trocar de arma", "Com a mão livre, leve a mão por cima do ombro (atrás da cabeça) e FECHE: puxa a 2ª arma das costas. Com o controle esquerdo na mão, o clique do analógico esquerdo também troca."),
            ("Sem controle", "Teia", "Olhe para um ponto de gancho (topo dos pilares, alto das paredes, cantos das salas): ele acende. Aponte a mão FECHADA para ele e ABRA: a teia prende (mirando um ponto aceso nunca sai bola de fogo). Feche a mão de novo e puxe para o peito para voar; abra para soltar (você segue com o embalo e cai)."),
            ("Sem controle", "Muralha Impenetrável", "Com as duas mãos livres, bata o PUNHO de uma mão na PALMA aberta da outra: aura vermelha por 20 s. Nela só sai SOCO, muito mais forte (os pequenos voam, os grandes atordoam rápido). Suas mãos defendem: ponha a mão no caminho do golpe para bloquear, ou vá com ela rápido contra o golpe para APARAR. Palma batida = onda de choque para frente; punho na palma de novo = onda em volta. 30 de mana, recarga de 15 s."),
            ("Sem controle", "Raio do céu", "Olhe para cima, erga a mão acima da cabeça FECHADA e ABRA: um raio desce e fica na sua mão (6 s). Estique o braço para frente: a descarga acerta o inimigo para onde você aponta e pula para até 4 outros por perto. Eletrocuta. 30 de mana, recarga de 8 s."),
            ("Sem controle", "Dash para trás", "Faça o sinal do Homem-Aranha (indicador e mindinho esticados, médio e anelar dobrados) duas vezes seguidas, rápido."),
            ("Sem controle", "Agarrar e arremessar", "Feche a mão perto de um inimigo para agarrar. Jogue o braço e abra a mão para arremessar; ele leva dano ao bater. Não funciona em orc nem em chefe."),
            ("Sem controle", "Poção", "Leve a mão esquerda à boca e segure meio segundo (no controle: B)."),
            ("Sem controle", "Pausa e menus", "Pausa: palma esquerda virada para você + pinça (gesto de menu da Meta). Nos menus: aponte com a mão e encoste o indicador no polegar para clicar."),
            ("Lâmina", "Investida Perfurante", "Aponte a espada para frente e aperte A: você avança uns 4,5 m atravessando os inimigos."),
            ("Lâmina", "Lâmina de Vento", "Aperte B: os próximos 2 golpes de espada soltam cortes de vento na direção do golpe."),
            ("Lâmina", "Postura de Contra", "Aperte o gatilho: por 2 s, o próximo aparo ou bloqueio vira um contra-ataque forte (90 de dano)."),
            ("Elementalista", "Bola de Fogo (cajado)", "Segure o gatilho, faça o movimento de arremesso e solte. Explode em área; mais forte em enraizados."),
            ("Elementalista", "Lança de Gelo", "Aponte o cajado e aperte A: perfura até 3 inimigos e deixa lento."),
            ("Elementalista", "Raízes (cajado)", "Segure B, mire no chão com o cajado e solte: prende os inimigos por 3 s."),
            ("Qualquer arma", "Habilidade da arma", "Erga a arma acima da cabeça com a ponta para cima por meio segundo (vibra e brilha) e dê um golpe rápido em até 4 s. Cada tipo de arma tem a sua (espada, machado, maça, lança, adaga, cajado)."),
            ("Qualquer arma", "Arma em chamas", "Mergulhe a lâmina numa bola de fogo: a que está carregando na mão livre (ela é gasta) ou uma voando. A arma pega fogo por 12 s: +30% de dano e cada golpe faz o inimigo pegar fogo. Não vale para o cajado. Em chamas, a lâmina ATRAVESSA os inimigos em vez de parar neles."),
            ("Qualquer arma", "Aparo, bloqueio e perfurar", "Escudo parado bloqueia. Empurrar o escudo ou a arma contra o golpe no último instante apara e atordoa o inimigo. Estocada com a ponta crava; puxe a arma para trás para soltar."),
        };

        ArenaPlayer player;
        MenuPanel panel;
        int cursor;
        readonly List<string> rows = new List<string>();

        void Awake() => player = GetComponent<ArenaPlayer>();
        void Start() => panel = new MenuPanel("LivroDeMagias", 1.3f, 1.05f, new Color(0.1f, 0.07f, 0.04f, 0.95f));

        void Update()
        {
            if (panel == null) return;
            if (IsOpen && !ArenaGame.Paused) IsOpen = false;
            if (!IsOpen) { panel.Hide(); return; }
            panel.Show(player);
            if (Time.frameCount == openedFrame) { cursor = 0; Draw(); return; }   // o A/pinça que abriu não conta aqui
            int count = Entries.Length + 1;
            var input = player.Input;
            if (input.MenuUp) cursor = (cursor + count - 1) % count;
            if (input.MenuDown) cursor = (cursor + 1) % count;
            if (panel.Hover >= 0) cursor = panel.Hover;
            int click = panel.TakeClick();
            if (click >= 0) cursor = click;
            bool confirm = click >= 0 || input.MenuConfirm;
            if (input.MenuRespec || confirm && cursor == count - 1) { IsOpen = false; ClosedFrame = Time.frameCount; panel.Hide(); ArenaAudio.Play2D(Sfx.Click, 0.6f); return; }
            Draw();
        }

        void Draw()
        {
            rows.Clear();
            for (int i = 0; i < Entries.Length; i++)
            {
                var e = Entries[i];
                bool sel = i == cursor;
                string line = "<size=40><color=#a98f6a>" + e.group + " · </color></size>" + (sel ? "<color=#ffd36b>" + e.title + "</color>" : e.title);
                if (sel) line += "\n<size=42><color=#e8dcc8>" + MenuPanel.Wrap(e.how, 78) + "</color></size>";
                rows.Add(line);
            }
            rows.Add("<color=#ff9f9f>Fechar</color>");
            panel.Render("<b>LIVRO DE MAGIAS</b>  <size=40><color=#a98f6a>passe o dedo (ou cima/baixo) para ver como fazer</color></size>", rows, cursor,
                "<size=40><color=#999>Pinça ou A em \"Fechar\" · B volta para a pausa</color></size>");
        }
    }
}
