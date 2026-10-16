using System.Text;
using Arena.Core;
using UnityEngine;

namespace Arena
{
    // HUD preso à cabeça (VR e desktop): barras de vida/estamina/mana/XP, habilidades, mensagens, barra do chefe,
    // vinheta da esquiva/investida e flash de dano.
    [RequireComponent(typeof(ArenaPlayer))]
    public sealed class ArenaHud : MonoBehaviour
    {
        ArenaPlayer player;
        Transform root;
        Transform hp, sta, mana, xp, bossHp, bossPosture, bossRoot;
        TextMesh info, message, bossName;
        WeaponAbilityCaster wac;
        Renderer vignetteR, hurtR;
        float hurtFlash;
        LeftHandPowers handPowers;
        Transform pausePanel;
        readonly StringBuilder sb = new StringBuilder();

        void Start()
        {
            player = GetComponent<ArenaPlayer>();
            player.Hurt += _ => hurtFlash = 1f;
            root = new GameObject("HUD").transform;
            root.SetParent(player.Head, false);
            root.localPosition = new Vector3(-0.32f, -0.3f, 0.9f);
            root.localRotation = Quaternion.Euler(10f, -12f, 0f);

            hp = Bar(root, 0.00f, new Color(0.8f, 0.2f, 0.2f));
            sta = Bar(root, -0.035f, new Color(0.23f, 0.7f, 0.23f));
            mana = Bar(root, -0.07f, new Color(0.23f, 0.44f, 0.85f));
            xp = Bar(root, -0.105f, new Color(0.85f, 0.65f, 0.17f), 0.012f);
            info = ArenaFx.MakeText(root, "", 0.0042f, Color.white, TextAnchor.UpperLeft);
            info.transform.localPosition = new Vector3(0f, -0.13f, 0f);

            message = ArenaFx.MakeText(player.Head, "", 0.006f, new Color(1f, 0.83f, 0.42f));
            message.transform.localPosition = new Vector3(0, 0.12f, 1.1f);

            bossRoot = new GameObject("ChefeHUD").transform;
            bossRoot.SetParent(player.Head, false);
            bossRoot.localPosition = new Vector3(-0.3f, 0.3f, 1.1f);
            bossName = ArenaFx.MakeText(bossRoot, "Rei Goblin", 0.005f, new Color(1f, 0.6f, 0.5f), TextAnchor.LowerLeft);
            bossName.transform.localPosition = new Vector3(0, 0.02f, 0);
            bossHp = Bar(bossRoot, 0f, new Color(0.75f, 0.12f, 0.12f), 0.025f, 0.6f);
            bossPosture = Bar(bossRoot, -0.035f, new Color(0.9f, 0.7f, 0.12f), 0.012f, 0.6f);

            vignetteR = Overlay(new Color(0, 0, 0, 0), 0.31f);
            hurtR = Overlay(new Color(0.8f, 0f, 0f, 0f), 0.3f);

            pausePanel = new GameObject("Pausa").transform;
            pausePanel.SetParent(player.Head, false);
            pausePanel.localPosition = new Vector3(0, 0, 1.0f);
            ArenaUtil.Prim(PrimitiveType.Quad, pausePanel, new Vector3(0, 0, 0.01f), new Vector3(0.9f, 0.5f, 1f), ArenaUtil.Unlit(new Color(0.05f, 0.04f, 0.07f, 0.9f)));
            var pt = ArenaFx.MakeText(pausePanel,
                "<b>PAUSADO</b>\n\n<size=44>Para continuar: botão Menu (☰) do controle esquerdo\n" +
                "ou o gesto de menu da mão esquerda (palma virada para você + pinça), ou A\n\n" +
                "B: voltar ao menu inicial\n\nDesktop: P/Esc ou Enter continua · R menu inicial</size>",
                0.006f, Color.white);
            pt.transform.localPosition = Vector3.zero;
            pausePanel.gameObject.SetActive(false);
        }

        Transform Bar(Transform parent, float y, Color c, float h = 0.022f, float w = 0.3f)
        {
            ArenaUtil.Prim(PrimitiveType.Cube, parent, new Vector3(w / 2f, y, 0.001f), new Vector3(w + 0.006f, h + 0.006f, 0.001f), ArenaUtil.Unlit(new Color(0, 0, 0, 0.55f)));
            var pivot = new GameObject("Fill").transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = new Vector3(0, y, 0);
            ArenaUtil.Prim(PrimitiveType.Cube, pivot, new Vector3(w / 2f, 0, 0), new Vector3(w, h, 0.001f), ArenaUtil.Unlit(c));
            return pivot;
        }

        Renderer Overlay(Color c, float z)
        {
            var q = ArenaUtil.Prim(PrimitiveType.Quad, player.Head, new Vector3(0, 0, z), new Vector3(1.2f, 1.2f, 1f), ArenaUtil.Unlit(c));
            return q.GetComponent<Renderer>();
        }

        // HUD na cabeça (padrão) ou no pulso esquerdo como um relógio (opção da pausa; só no VR).
        void PlaceHud()
        {
            bool wrist = SaveSystem.Options.wristHud && player.IsVr && player.leftHand;
            var parent = wrist ? player.leftHand : player.Head;
            if (root.parent == parent) return;
            root.SetParent(parent, false);
            if (wrist)
            {
                root.localPosition = new Vector3(0.06f, 0.04f, -0.14f);   // em cima do pulso, olhando para cima
                root.localRotation = Quaternion.Euler(90f, 0f, 0f);
                root.localScale = Vector3.one * 0.7f;
            }
            else
            {
                root.localPosition = new Vector3(-0.32f, -0.3f, 0.9f);
                root.localRotation = Quaternion.Euler(10f, -12f, 0f);
                root.localScale = Vector3.one;
            }
        }

        static void SetFill(Transform t, float f) => t.localScale = new Vector3(Mathf.Clamp(f, 0.001f, 1f), 1, 1);

        void LateUpdate()
        {
            // Se o código for recarregado com o jogo rodando, as referências criadas no Start somem: fica quieto.
            if (!player || !pausePanel || player.State == null) return;
            pausePanel.gameObject.SetActive(false);   // a pausa agora é o PauseMenu (com opções)
            PlaceHud();
            var s = player.State;
            SetFill(hp, s.Hp.Fraction);
            SetFill(sta, s.Stamina.Fraction);
            SetFill(mana, s.Mana.Fraction);
            var prog = s.Progression;
            SetFill(xp, prog.IsMaxLevel ? 1f : prog.Xp / (float)prog.XpNeeded);

            sb.Clear();
            sb.Append(s.Class.Name).Append("  Nv ").Append(prog.Level);
            if (prog.UnspentPoints > 0) sb.Append("  <color=#ffd36b>+").Append(prog.UnspentPoints).Append(" ponto(s)</color>");
            var rc = WeaponModels.RarityColor(player.Weapon.Rarity);
            sb.Append("\n<color=#").Append(ColorUtility.ToHtmlStringRGB(rc)).Append('>').Append(player.Weapon.Def?.Name).Append("</color>");
            sb.Append("  <color=#ffd34d>").Append(s.Gold).Append(" ouro</color>");
            if (!player.Weapon.Held) sb.Append(" <color=#ff9090>(arremessada)</color>");
            int n = 1;
            foreach (var node in s.Tree.Nodes)
            {
                if (!node.IsActive) continue;
                sb.Append('\n').Append(n++).Append(' ').Append(node.Name).Append(": ");
                if (!s.Tree.Has(node.Id)) sb.Append("<color=#777>bloqueada</color>");
                else { float cd = s.CooldownLeft(node.Id); sb.Append(cd > 0f ? $"<color=#aaa>{cd:0.0}s</color>" : "<color=#7CFC00>pronta</color>"); }
            }
            var shield = player.Shield;
            if (shield && shield.Available) sb.Append(shield.Equipped ? "\nEscudo na mão" : "\n<color=#9fd0ff>Escudo nas costas: mão livre</color>");
            if (handPowers == null) handPowers = player.GetComponent<LeftHandPowers>();
            if (handPowers && handPowers.HandTracked && player.Input.LeftHandFree)
                sb.Append("\n<color=#999>Mão: ").Append(handPowers.CurrentShape).Append("</color>");
            if (player.Weapon && player.Weapon.TwoHanded) sb.Append("\n<color=#ffd36b>Duas mãos</color>");
            if (player.Weapon && player.Weapon.Embedded)
                sb.Append("\n<color=#ff8080>Espada cravada: segure o gatilho ").Append((player.Weapon.PullProgress * 100f).ToString("0")).Append("%</color>");
            if (s.CounterStance > 0f) sb.Append("\n<color=#ffd36b>CONTRA ATIVO</color>");
            if (s.WindCharges > 0) sb.Append("\n<color=#cff4ff>Vento: ").Append(s.WindCharges).Append("</color>");
            if (s.ActiveItem != null)
            {
                float acd = s.WeaponAbilityCooldown;
                if (!wac) wac = player.GetComponent<WeaponAbilityCaster>();
                sb.Append("\nArma: ").Append(s.ActiveItem.Ability.Name).Append(": ")
                  .Append(wac && wac.Charged ? "<color=#ffe680>CARREGADA</color>" : acd > 0f ? $"<color=#aaa>{acd:0.0}s</color>" : "<color=#7CFC00>pronta</color>");
            }
            if (ArenaGame.Instance && ArenaGame.Instance.NearBonfire) sb.Append("\n<color=#ffb060>Fogueira: curando</color>");
            info.text = sb.ToString();

            var g = ArenaGame.Instance;
            message.text = g && g.MessageTime > 0f && !ArenaGame.Paused ? g.CurrentMessage : "";
            bool boss = g && g.Boss && !g.Boss.Dead;
            bossRoot.gameObject.SetActive(boss);
            if (boss)
            {
                bossName.text = g.Boss.Phase == 2 ? "Rei Goblin — Fúria" : "Rei Goblin";
                SetFill(bossHp, g.Boss.State.HpFraction);
                SetFill(bossPosture, g.Boss.State.Posture.Fraction);
            }

            hurtFlash = Mathf.MoveTowards(hurtFlash, 0f, Time.unscaledDeltaTime * 3f);
            hurtR.material.color = new Color(0.8f, 0f, 0f, hurtFlash * 0.35f);
            vignetteR.material.color = new Color(0f, 0f, 0f, player.VignetteLevel * 0.55f);
        }
    }
}
