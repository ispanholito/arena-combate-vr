using System;
using System.Collections.Generic;
using Arena.Core;
using UnityEngine;

namespace Arena
{
    // Tutorial dos gestos (2026-10-28, "pedaço vendável": ninguém descobre os poderes da mão sozinho). Roda dentro do
    // Treino (sala bonita, sem tomar dano, mana infinita) com um boneco e um quadro de instruções. Cada passo só avança
    // quando o jogador faz o gesto de verdade (TutorialEvents) ou a condição vale (mão livre). Pausa → "Pular este passo"
    // ou "Sair do tutorial". Começa sozinho na primeira vez que se entra no santuário (GameOptions.tutorialSeen); depois,
    // pelo portal TUTORIAL do santuário ou pelo menu inicial.
    public sealed class TutorialMode : MonoBehaviour
    {
        public static TutorialMode Instance { get; private set; }

        sealed class Step
        {
            public string Title, How, Event;
            public Func<ArenaPlayer, bool> Done;   // condição em vez de evento (ou além dele)
        }

        static readonly Step[] Steps =
        {
            new Step { Title = "Golpe com a arma", Event = "golpe",
                How = "Segure o controle direito e golpeie o boneco com a espada. O dano vem da VELOCIDADE e do ÂNGULO: golpes rápidos com o fio cortam mais. (No PC: botão esquerdo do mouse.)" },
            new Step { Title = "Mão livre", Done = p => !p.IsVr || p.Input.LeftHandFree,
                How = "Solte o controle ESQUERDO (pendure na alça ou ponha no colo). A sua mão de verdade aparece no jogo: os poderes saem dela. Para voltar a usar o controle, é só segurá-lo." },
            new Step { Title = "Raio", Event = "raio",
                How = "APONTE o indicador para o boneco, com os outros dedos fechados. Um ponto vermelho carrega na ponta do dedo e o raio sai sozinho. Mantenha apontado para repetir." },
            new Step { Title = "Soco", Event = "soco",
                How = "Chegue perto do boneco, feche o punho e dê um SOCO rápido para frente. Acerta um pouco além da mão." },
            new Step { Title = "Corte da palma", Event = "palma",
                How = "Mão ABERTA, passe-a rápido no ar, como um tapa forte: sai um corte de vento na direção do movimento." },
            new Step { Title = "Bola de fogo", Event = "bola",
                How = "Estique o PUNHO fechado para frente e então ABRA a mão: a bola cresce na palma (cheia em 1,5 s). Jogue a mão para frente. Ela é teleguiada: vai no alvo para onde você jogou." },
            new Step { Title = "As duas mãos livres", Done = p => !p.IsVr || (p.Input.LeftHandFree && p.Input.RightHandFree),
                How = "Agora solte também o controle DIREITO (a arma cai no chão). Para pegar a arma de volta depois: segure o controle e aperte o grip — ela volta para a mão." },
            new Step { Title = "Raízes", Event = "raizes",
                How = "Bata uma PALMA, separe as mãos e jogue as DUAS para baixo, rápido. Raízes prendem os inimigos à sua frente por 3 s. Fogo em quem está preso queima mais." },
            new Step { Title = "Onda de energia", Event = "kame",
                How = "Bata uma PALMA e mantenha os PULSOS JUNTOS, mãos abertas na altura da barriga: a esfera azul cresce. Empurre as duas mãos para frente com força: um raio grosso que atravessa todos." },
            new Step { Title = "Teia", Event = "puxar",
                How = "OLHE para um ponto no alto de um pilar ou da parede: ele acende em azul. Aponte a mão FECHADA para ele e ABRA: a teia prende. FECHE de novo e puxe para o peito para voar; abra a mão para soltar (você segue com o embalo)." },
            new Step { Title = "Esquiva para trás", Event = "dash_tras",
                How = "Faça o sinal do chifre (indicador e mindinho esticados, médio e anelar dobrados) DUAS vezes seguidas, rápido: você dá um salto para trás." },
            new Step { Title = "Pausa e menus", Event = "pausa",
                How = "Gesto de menu: palma da mão ESQUERDA virada para você e encoste o indicador no polegar (pinça). Nos menus, aponte com a mão e faça a pinça para clicar. Na pausa, o LIVRO DE MAGIAS explica tudo de novo." },
        };

        int step;
        float doneT = -1f, endT = -1f;
        Transform board;
        TextMesh title, body, footer;

        public static bool Active => Instance;

        void Awake()
        {
            Instance = this;
            TutorialEvents.Fired += OnEvent;
        }

        void OnDestroy()
        {
            TutorialEvents.Fired -= OnEvent;
            if (Instance == this) Instance = null;
            if (board) Destroy(board.gameObject);
        }

        void Start()
        {
            // Quadro entre o jogador e o boneco, de lado (a ~1,8 m: dá para ler no Quest 2), virado para o jogador.
            var pl = ArenaGame.Instance.Player;
            board = new GameObject("QuadroTutorial").transform;
            var pos = new Vector3(1.3f, 1.45f, 1.5f);
            var look = pos - pl.HeadPosition; look.y = 0f;
            board.SetPositionAndRotation(pos, Quaternion.LookRotation(look.sqrMagnitude > 0.01f ? look : Vector3.back));
            var bg = ArenaUtil.Prim(PrimitiveType.Quad, board, new Vector3(0f, 0f, 0.01f), new Vector3(1.5f, 1.15f, 1f), ArenaUtil.Unlit(new Color(0.08f, 0.06f, 0.05f, 0.9f)));
            foreach (var c in bg.GetComponentsInChildren<Collider>()) Destroy(c);
            title = ArenaFx.MakeText(board, "", 0.0085f, new Color(1f, 0.83f, 0.42f), TextAnchor.UpperLeft);
            title.transform.localPosition = new Vector3(-0.7f, 0.52f, 0f);
            body = ArenaFx.MakeText(board, "", 0.0062f, Color.white, TextAnchor.UpperLeft);
            body.transform.localPosition = new Vector3(-0.7f, 0.33f, 0f);
            footer = ArenaFx.MakeText(board, "", 0.005f, new Color(0.7f, 0.7f, 0.7f), TextAnchor.LowerLeft);
            footer.transform.localPosition = new Vector3(-0.7f, -0.52f, 0f);
            ShowStep();
            ArenaGame.Instance.Message("Tutorial: siga o quadro ao lado do boneco", 3f);
        }

        void OnEvent(string id)
        {
            if (doneT >= 0f || step >= Steps.Length) return;
            if (id == "palma_batida" && (Steps[step].Event == "raizes" || Steps[step].Event == "kame")) { Feedback("Palma! Agora o resto do gesto", 0.8f); return; }
            if (id == "teia" && Steps[step].Event == "puxar") { Feedback("Teia presa! Agora puxe", 0.8f); return; }
            if (Steps[step].Event == id) Complete();
        }

        void Update()
        {
            if (step >= Steps.Length)
            {
                if (endT >= 0f) { endT -= Time.unscaledDeltaTime; if (endT < 0f) ArenaGame.Instance.FinishTutorial(); }
                return;
            }
            if (doneT >= 0f)
            {
                doneT -= Time.deltaTime;
                if (doneT < 0f) { step++; ShowStep(); }
                return;
            }
            var s = Steps[step];
            if (s.Done != null && s.Done(ArenaGame.Instance.Player)) Complete();
            // Lembrete: passos de mão livre com o controle ainda na mão.
            var pl = ArenaGame.Instance.Player;
            bool needsLeft = step >= 2 && step <= 5, needsBoth = step >= 7 && step <= 11;
            string warn = pl.IsVr && ((needsLeft && !pl.Input.LeftHandFree && !pl.Input.RightHandFree) || (needsBoth && !(pl.Input.LeftHandFree && pl.Input.RightHandFree)))
                ? "<color=#ff9f80>Solte " + (needsBoth ? "os dois controles" : "um controle") + " para usar a mão.</color>\n" : "";
            footer.text = warn + $"Passo {step + 1} de {Steps.Length}  ·  Pausa → \"Pular este passo\" ou \"Sair do tutorial\"";
        }

        void Complete()
        {
            doneT = 1.3f;
            ArenaAudio.Play2D(Sfx.Level, 0.6f, 1.3f);
            ArenaInput.Haptic(0.5f, 0.1f);
            title.text = "✔ " + Steps[step].Title;
            Feedback("Boa!", 1f);
        }

        void Feedback(string text, float seconds)
        {
            if (board) ArenaFx.FloatText(text, board.position + Vector3.up * 0.75f, new Color(0.6f, 1f, 0.6f), seconds);
        }

        void ShowStep()
        {
            if (step >= Steps.Length)
            {
                title.text = "Pronto!";
                body.text = MenuPanel.Wrap("Você sabe o básico. Os números de cada poder estão no LIVRO DE MAGIAS (pausa). Pegue os controles de volta quando quiser. Voltando ao santuário...", 46);
                footer.text = "";
                endT = 5f;
                ArenaAudio.Play2D(Sfx.Level, 0.9f, 1f);
                return;
            }
            var s = Steps[step];
            title.text = s.Title;
            body.text = MenuPanel.Wrap(s.How, 44);
        }

        // Pausa → "Pular este passo".
        public void Skip()
        {
            if (step >= Steps.Length) return;
            doneT = -1f; step++; ShowStep();
        }
    }
}
