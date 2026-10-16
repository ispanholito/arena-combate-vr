using System.Text;
using Arena.Core;
using UnityEngine;

namespace Arena
{
    // Menu inicial: escolher a classe e o modo (Arena com ondas e chefe, ou Treino com boneco parado).
    // VR: analógico direito para cima/baixo, A confirma. Desktop: setas ou W/S, Enter ou E confirma.
    [RequireComponent(typeof(ArenaPlayer))]
    public sealed class StartMenu : MonoBehaviour
    {
        ArenaPlayer player;
        Transform panel;
        TextMesh text;
        int cursor = 1;
        ClassId cls = ClassId.Lamina;
        readonly StringBuilder sb = new StringBuilder();

        const int Rows = 3;

        void Start()
        {
            player = GetComponent<ArenaPlayer>();
            panel = new GameObject("MenuInicial").transform;
            panel.SetParent(player.Head, false);
            panel.localPosition = new Vector3(0, 0, 1.3f);
            ArenaUtil.Prim(PrimitiveType.Quad, panel, new Vector3(0, 0, 0.01f), new Vector3(1.2f, 0.8f, 1f), ArenaUtil.Unlit(new Color(0.06f, 0.05f, 0.08f, 0.94f)));
            text = ArenaFx.MakeText(panel, "", 0.0058f, Color.white, TextAnchor.MiddleLeft);
            text.transform.localPosition = new Vector3(-0.54f, 0, 0);
        }

        void Update()
        {
            if (!panel || !player || !ArenaGame.Instance) return;
            bool show = ArenaGame.InMenu;
            if (panel.gameObject.activeSelf != show) panel.gameObject.SetActive(show);
            if (!show) return;
            var input = player.Input;
            if (input.MenuUp || input.MenuDown) ArenaAudio.Play2D(Sfx.Click, 0.5f);
            if (input.MenuUp) cursor = (cursor + Rows - 1) % Rows;
            if (input.MenuDown) cursor = (cursor + 1) % Rows;
            if (input.MenuConfirm)
            {
                ArenaAudio.Play2D(Sfx.Click, 0.8f, 1.3f);
                if (cursor == 0) cls = cls == ClassId.Lamina ? ClassId.Elementalista : ClassId.Lamina;
                else ArenaGame.Instance.StartMode(cursor == 1 ? ArenaGame.GameMode.Arena : ArenaGame.GameMode.Treino, cls);
            }
            Draw();
        }

        void Draw()
        {
            sb.Clear();
            sb.Append("<b><size=90>ARENA VR</size></b>\n\n");
            Row(0, "Classe: <color=#ffd36b>" + ClassProfile.Get(cls).Name + "</color>",
                cls == ClassId.Lamina ? "espada + escudo (largue o controle esquerdo para usar a mão)" : "cajado + mão esquerda livre");
            Row(1, "Arena", "ondas de inimigos e o Rei Goblin; começa no nível 1");
            Row(2, "Treino", "boneco parado com medidor de dano; nível 10, sem tomar dano");
            sb.Append("\n<size=40><color=#999>")
              .Append(player.IsVr ? "Analógico direito: cima/baixo · A: escolher" : "Setas ou W/S: cima/baixo · Enter ou E: escolher")
              .Append("\nNa pausa: B (ou R) volta para este menu</color></size>");
            text.text = sb.ToString();
        }

        void Row(int i, string title, string desc)
        {
            bool sel = cursor == i;
            sb.Append(sel ? "<color=#9fd0ff>> " : "   ").Append(title).Append(sel ? "</color>" : "")
              .Append("\n      <size=50><color=#bbb>").Append(desc).Append("</color></size>\n\n");
        }
    }
}
