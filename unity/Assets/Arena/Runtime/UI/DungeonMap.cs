using Arena.Core;
using UnityEngine;

namespace Arena
{
    // Minimapa da dungeon: aparece ao lado do menu de pausa. Salas visitadas em cores (limpa, em combate, fogueira,
    // escada, chefe), as vizinhas apagadas e o resto escondido; ponto amarelo = você, seta = para onde olha.
    [RequireComponent(typeof(ArenaPlayer))]
    public sealed class DungeonMap : MonoBehaviour
    {
        const float Size = 0.6f;   // lado do mapa em metros (a 1 m dos olhos)

        ArenaPlayer player;
        Transform panel, content, you;
        TextMesh title;
        bool wasShown;

        void Awake() => player = GetComponent<ArenaPlayer>();

        void Start()
        {
            panel = new GameObject("Minimapa").transform;
            panel.SetParent(player.Head, false);
            panel.localPosition = new Vector3(0.74f, 0f, 1.0f);
            panel.localRotation = Quaternion.Euler(0, 32f, 0);   // virado para o jogador
            ArenaUtil.Prim(PrimitiveType.Quad, panel, new Vector3(0, 0, 0.01f), new Vector3(Size + 0.06f, Size + 0.12f, 1f), ArenaUtil.Unlit(new Color(0.05f, 0.04f, 0.07f, 0.92f)));
            title = ArenaFx.MakeText(panel, "", 0.005f, new Color(0.95f, 0.9f, 0.8f));
            title.transform.localPosition = new Vector3(0, Size / 2f + 0.03f, 0);
            panel.gameObject.SetActive(false);
        }

        void Update()
        {
            if (!panel) return;
            var game = ArenaGame.Instance;
            bool show = ArenaGame.Paused && game && game.Mode == ArenaGame.GameMode.Dungeon && game.Dungeon.Active;
            if (panel.gameObject.activeSelf != show) panel.gameObject.SetActive(show);
            if (!show) { wasShown = false; return; }
            var run = game.Dungeon.Run;
            var proj = new MapProjection(run.Floor);
            if (!wasShown) { Rebuild(run, proj); wasShown = true; }
            // Você: posição e direção.
            var feet = player.FeetPosition;
            var (u, v) = proj.ToMap(feet.x, feet.z);
            you.localPosition = new Vector3(u * Size, v * Size, -0.004f);
            var f = player.HeadForward;
            you.localRotation = Quaternion.Euler(0, 0, -Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg);
        }

        void Rebuild(DungeonRun run, MapProjection proj)
        {
            if (content) Destroy(content.gameObject);
            content = new GameObject("Conteudo").transform;
            content.SetParent(panel, false);
            var f = run.Floor;
            title.text = "Andar " + f.Number + " de " + DungeonGenerator.Floors + " · profundidade " + run.Tier;
            for (int i = 1; i < f.Rooms.Count; i++)
                if (MapProjection.Revealed(f, i) && MapProjection.Revealed(f, i - 1))
                    Rect(proj, f.Corridors[i - 1], new Color(0.35f, 0.33f, 0.3f), 0f);
            for (int i = 0; i < f.Rooms.Count; i++)
            {
                if (!MapProjection.Revealed(f, i)) continue;
                var room = f.Rooms[i];
                Color c = !room.Visited ? new Color(0.25f, 0.24f, 0.26f)
                        : room == run.ActiveRoom ? new Color(0.8f, 0.2f, 0.15f)
                        : room.Kind == RoomKind.Fogueira || room.Kind == RoomKind.Inicio ? new Color(0.9f, 0.55f, 0.2f)
                        : room.Kind == RoomKind.Escada ? new Color(0.55f, 0.4f, 1f)
                        : room.Kind == RoomKind.Chefe ? new Color(0.6f, 0.1f, 0.1f)
                        : room.Cleared ? new Color(0.3f, 0.6f, 0.35f) : new Color(0.5f, 0.5f, 0.5f);
                Rect(proj, room.Rect, c, -0.001f);
                string icon = room.Kind switch
                {
                    RoomKind.Inicio or RoomKind.Fogueira => "fogueira",
                    RoomKind.Escada => "escada",
                    RoomKind.Chefe => "CHEFE",
                    _ => room.Visited && room.HasChest && room.Cleared ? "baú" : "",
                };
                if (icon != "")
                {
                    var (u, v) = proj.ToMap(room.Rect.CenterX, room.Rect.CenterZ);
                    var t = ArenaFx.MakeText(content, icon, 0.0035f, Color.white);
                    t.transform.localPosition = new Vector3(u * Size, v * Size, -0.003f);
                }
            }
            you = new GameObject("Voce").transform;
            you.SetParent(content, false);
            ArenaUtil.Prim(PrimitiveType.Quad, you, Vector3.zero, Vector3.one * 0.025f, ArenaUtil.Unlit(new Color(1f, 0.9f, 0.2f)));
            ArenaUtil.Prim(PrimitiveType.Quad, you, new Vector3(0, 0.022f, 0), new Vector3(0.01f, 0.025f, 1f), ArenaUtil.Unlit(new Color(1f, 0.9f, 0.2f)));
        }

        void Rect(MapProjection proj, Rect2 r, Color c, float z)
        {
            var (u, v) = proj.ToMap(r.CenterX, r.CenterZ);
            ArenaUtil.Prim(PrimitiveType.Quad, content, new Vector3(u * Size, v * Size, z), new Vector3(r.Width * proj.Scale * Size, r.Depth * proj.Scale * Size, 1f), ArenaUtil.Unlit(c));
        }
    }
}
