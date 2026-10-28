using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Arena
{
    // Painel de menu com linhas clicáveis (2026-10-28: menus sem controle). Cada linha é um TextMesh próprio, empilhado
    // de cima para baixo, com um fundo que acende na linha escolhida. O HandPointer mira nas linhas com o raio do dedo
    // (Hover) e a pinça indicador + polegar clica (Clicked). No VR o painel fica parado no mundo, na frente de onde você
    // olhava ao abrir (acompanha o corpo, não a cabeça); no desktop continua preso à câmera.
    public sealed class MenuPanel
    {
        public static readonly List<MenuPanel> Visible = new List<MenuPanel>();
        public static int OpenSerial { get; private set; }   // muda a cada abertura: o ponteiro exige soltar a pinça antes de clicar

        public Transform Root { get; }
        public int Hover = -1;     // linha sob o ponteiro (-1 = nenhuma)
        public int Clicked = -1;   // linha clicada pela pinça neste quadro (o menu consome e zera)
        public float Width { get; }

        readonly float distance;
        readonly Transform bg;
        readonly TextMesh header, footer;
        readonly List<RowView> rows = new List<RowView>();
        int rowCount;

        sealed class RowView { public TextMesh Text; public Transform Hl; public Material HlMat; public float Top, Bottom; }

        const float Pad = 0.035f, Gap = 0.012f, CharSize = 0.0055f;
        // Altura de uma linha de TextMesh por unidade de "size" com characterSize 0,0055 (calibrado no editor).
        const float LinePerSize = 0.00063f;
        static readonly Color RowIdle = new Color(1f, 1f, 1f, 0f), RowCursor = new Color(0.3f, 0.5f, 0.9f, 0.28f), RowHover = new Color(0.45f, 0.7f, 1f, 0.42f);

        public MenuPanel(string name, float width, float distance, Color background)
        {
            Width = width; this.distance = distance;
            Root = new GameObject(name).transform;
            bg = ArenaUtil.Prim(PrimitiveType.Quad, Root, new Vector3(0, 0, 0.01f), new Vector3(width, 0.5f, 1f), ArenaUtil.Unlit(background)).transform;
            foreach (var c in bg.GetComponentsInChildren<Collider>()) Object.Destroy(c);
            header = ArenaFx.MakeText(Root, "", CharSize, Color.white, TextAnchor.UpperLeft);
            footer = ArenaFx.MakeText(Root, "", CharSize, Color.white, TextAnchor.UpperLeft);
            Root.gameObject.SetActive(false);
        }

        public bool IsVisible => Root && Root.gameObject.activeSelf;
        public int RowCount => rowCount;

        // Mostra o painel. VR: na frente do olhar, preso ao espaço do jogador. Desktop: preso à câmera.
        public void Show(ArenaPlayer player)
        {
            if (IsVisible) return;
            if (player.IsVr)
            {
                Root.SetParent(player.trackingSpace, true);
                var f = player.HeadForward;
                Root.position = player.HeadPosition + f * distance - Vector3.up * 0.12f;
                Root.rotation = Quaternion.LookRotation(f, Vector3.up);
            }
            else
            {
                Root.SetParent(player.Head, false);
                Root.localPosition = new Vector3(0, 0, distance); Root.localRotation = Quaternion.identity;
            }
            Root.gameObject.SetActive(true);
            Hover = Clicked = -1;
            OpenSerial++;
            if (!Visible.Contains(this)) Visible.Add(this);
        }

        public void Hide()
        {
            if (!Root) return;
            if (Root.gameObject.activeSelf) Root.gameObject.SetActive(false);
            Visible.Remove(this);
            Hover = Clicked = -1;
        }

        // Consome o clique da pinça (devolve a linha ou -1).
        public int TakeClick() { int c = Clicked; Clicked = -1; return c; }

        // Redesenha: cabeçalho, linhas (o cursor fica destacado) e rodapé. O painel cresce para caber tudo.
        public void Render(string headerText, IReadOnlyList<string> rowTexts, int cursor, string footerText)
        {
            float left = -Width * 0.5f + Pad;
            float y = 0f;
            header.text = headerText ?? "";
            header.transform.localPosition = new Vector3(left, y, 0f);
            y -= TextHeight(header.text) + Gap * 2f;
            rowCount = rowTexts.Count;
            for (int i = 0; i < rowCount; i++)
            {
                var r = GetRow(i);
                r.Text.gameObject.SetActive(true); r.Hl.gameObject.SetActive(true);
                r.Text.text = rowTexts[i];
                r.Top = y + Gap * 0.5f;
                float h = TextHeight(rowTexts[i]);
                r.Text.transform.localPosition = new Vector3(left + 0.015f, y, 0f);
                y -= h + Gap;
                r.Bottom = y + Gap * 0.5f;
                r.Hl.localPosition = new Vector3(0f, (r.Top + r.Bottom) * 0.5f, 0.005f);
                r.Hl.localScale = new Vector3(Width - Pad, r.Top - r.Bottom, 1f);
                r.HlMat.color = i == Hover ? RowHover : i == cursor ? RowCursor : RowIdle;
            }
            for (int i = rowCount; i < rows.Count; i++) { rows[i].Text.gameObject.SetActive(false); rows[i].Hl.gameObject.SetActive(false); }
            y -= Gap;
            footer.text = footerText ?? "";
            footer.transform.localPosition = new Vector3(left, y, 0f);
            y -= TextHeight(footer.text);
            // Centraliza tudo verticalmente: desloca o conteúdo para o meio do fundo.
            float total = -y + Pad * 2f;
            float shift = total * 0.5f - Pad;
            header.transform.localPosition += Vector3.up * shift;
            footer.transform.localPosition += Vector3.up * shift;
            for (int i = 0; i < rowCount; i++)
            {
                var r = rows[i];
                r.Text.transform.localPosition += Vector3.up * shift;
                r.Hl.localPosition += Vector3.up * shift;
                r.Top += shift; r.Bottom += shift;
            }
            bg.localScale = new Vector3(Width, total, 1f);
        }

        RowView GetRow(int i)
        {
            while (rows.Count <= i)
            {
                var r = new RowView();
                r.Text = ArenaFx.MakeText(Root, "", CharSize, Color.white, TextAnchor.UpperLeft);
                r.HlMat = ArenaUtil.Unlit(RowIdle);
                r.Hl = ArenaUtil.Prim(PrimitiveType.Quad, Root, Vector3.zero, Vector3.one, r.HlMat).transform;
                foreach (var c in r.Hl.GetComponentsInChildren<Collider>()) Object.Destroy(c);
                rows.Add(r);
            }
            return rows[i];
        }

        // Raio do dedo × painel: distância até o plano e a linha acertada (-1 = no painel, fora das linhas).
        public bool Raycast(Ray ray, out float dist, out int row, out Vector3 point)
        {
            dist = 0f; row = -1; point = default;
            if (!IsVisible) return false;
            var plane = new Plane(Root.forward, Root.position);
            if (!plane.Raycast(ray, out dist) || dist <= 0f) return false;
            point = ray.GetPoint(dist);
            var local = Root.InverseTransformPoint(point);
            var size = bg.localScale;
            if (Mathf.Abs(local.x) > size.x * 0.5f || Mathf.Abs(local.y) > size.y * 0.5f) return false;
            for (int i = 0; i < rowCount; i++)
                if (local.y <= rows[i].Top && local.y > rows[i].Bottom) { row = i; break; }
            return true;
        }

        // Altura estimada do texto: soma das linhas; cada linha mede pelo maior <size=N> com letra nela (padrão 64).
        static readonly Stack<int> sizes = new Stack<int>();
        public static float TextHeight(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0f;
            sizes.Clear();
            int cur = 64, lineMax = 0;
            float h = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '\n') { h += LineHeight(lineMax > 0 ? lineMax : cur); lineMax = 0; continue; }
                if (c == '<')
                {
                    if (string.CompareOrdinal(s, i, "<size=", 0, 6) == 0)
                    {
                        int j = i + 6, n = 0;
                        while (j < s.Length && char.IsDigit(s[j])) { n = n * 10 + (s[j] - '0'); j++; }
                        sizes.Push(cur); cur = n;
                    }
                    else if (string.CompareOrdinal(s, i, "</size>", 0, 7) == 0) cur = sizes.Count > 0 ? sizes.Pop() : 64;
                    int close = s.IndexOf('>', i);
                    if (close > i) { i = close; continue; }
                }
                if (c != ' ') lineMax = Mathf.Max(lineMax, cur);
            }
            h += LineHeight(lineMax > 0 ? lineMax : cur);
            return h;
        }

        // A altura da linha não é proporcional ao <size>: medido no editor, linha de 42 = 74% da de 64.
        static float LineHeight(int size) => (size * 0.76f + 15.4f) * LinePerSize;

        // Quebra o texto em linhas de até maxChars (TextMesh não quebra sozinho).
        public static string Wrap(string text, int maxChars)
        {
            var sb = new StringBuilder();
            int lineLen = 0;
            foreach (var word in text.Split(' '))
            {
                if (lineLen > 0 && lineLen + 1 + word.Length > maxChars) { sb.Append('\n'); lineLen = 0; }
                else if (lineLen > 0) { sb.Append(' '); lineLen++; }
                sb.Append(word); lineLen += word.Length;
                int nl = word.LastIndexOf('\n');
                if (nl >= 0) lineLen = word.Length - nl - 1;
            }
            return sb.ToString();
        }
    }
}
