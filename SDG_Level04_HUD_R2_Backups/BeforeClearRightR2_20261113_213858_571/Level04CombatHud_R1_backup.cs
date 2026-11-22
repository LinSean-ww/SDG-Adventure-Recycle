using SDGAdventure.SharedPlayer;
using UnityEngine;
using UnityEngine.UI;

namespace SDGAdventure.Level04
{
    // LEVEL04_GAMEPLAY_HELP_V1
    // Replaces only the duplicate top-right HP bars with contextual instructions.
    // The main boss HP UI and existing player HUD are owned elsewhere.
    public sealed class Level04CombatHud : MonoBehaviour
    {
        private Level04PlayerHealth player;
        private BossHealth boss;
        private Image flash;
        private RectTransform root;
        private Text phaseLabel;
        private Text instructionsLabel;
        private Text shortcutLabel;
        private bool lastRecyclePhase;
        private float flashUntil;

        public void Initialize(Level04PlayerHealth health, BossHealth enemy, PlayerHudCanvas hud)
        {
            player = health;
            boss = enemy;
            hud.Build();

            Transform host = hud.transform.Find("PlayerHudCanvas");
            if (!host)
            {
                Debug.LogError("Level04 shared HUD canvas unavailable");
                return;
            }

            root = NewRect("Level04GameplayHelp", host);
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(1f, 1f);
            root.anchoredPosition = new Vector2(-18f, -20f);
            root.sizeDelta = new Vector2(334f, 174f);

            Image background = root.gameObject.AddComponent<Image>();
            background.color = new Color(0.018f, 0.085f, 0.105f, 0.91f);
            background.raycastTarget = false;

            Image topLine = Solid("HelpHeaderAccent", root,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(334f, 4f),
                new Vector2(167f, -2f), new Color(0.09f, 0.94f, 0.79f, 0.92f));
            topLine.raycastTarget = false;

            Image sideLine = Solid("HelpSideAccent", root,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(4f, 154f),
                new Vector2(2f, 0f), new Color(0.11f, 0.74f, 0.75f, 0.75f));
            sideLine.raycastTarget = false;

            AddText("HelpHeading", root, "第四關｜玩法說明", 18,
                new Vector2(12f, -10f), new Vector2(309f, 29f),
                new Color(0.61f, 1f, 0.9f, 1f));

            phaseLabel = AddText("CurrentPhase", root, "", 17,
                new Vector2(12f, -42f), new Vector2(309f, 26f),
                new Color(1f, 0.9f, 0.48f, 1f));

            instructionsLabel = AddText("PhaseInstructions", root, "", 15,
                new Vector2(12f, -72f), new Vector2(309f, 69f),
                new Color(0.92f, 0.98f, 1f, 1f));
            instructionsLabel.lineSpacing = 1.05f;

            shortcutLabel = AddText("HelpShortcut", root, "F1：重新顯示／隱藏完整教學", 13,
                new Vector2(12f, -145f), new Vector2(310f, 24f),
                new Color(0.49f, 0.87f, 0.91f, 0.95f));

            // Preserve the original non-blocking damage flash.
            RectTransform flashRect = NewRect("Level04DamageFlash", host);
            flashRect.SetAsFirstSibling();
            flashRect.anchorMin = Vector2.zero;
            flashRect.anchorMax = Vector2.one;
            flashRect.offsetMin = flashRect.offsetMax = Vector2.zero;
            flash = flashRect.gameObject.AddComponent<Image>();
            flash.color = Color.clear;
            flash.raycastTarget = false;

            if (player) player.Damaged += OnDamaged;
            RefreshInstructions();
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            return (RectTransform)obj.transform;
        }

        private static Image Solid(string name, Transform parent, Vector2 anchor,
            Vector2 pivot, Vector2 size, Vector2 position, Color color)
        {
            RectTransform rect = NewRect(name, parent);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Text AddText(string name, RectTransform parent, string value,
            int fontSize, Vector2 positionFromTopLeft, Vector2 size, Color color)
        {
            RectTransform rect = NewRect(name, parent);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = positionFromTopLeft;
            rect.sizeDelta = size;
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = PlayerGuiFont.Load();
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Normal;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.color = color;
            text.raycastTarget = false;
            text.text = value;
            return text;
        }

        private void OnDamaged()
        {
            flashUntil = Time.unscaledTime + 0.18f;
        }

        private void RefreshInstructions()
        {
            if (!phaseLabel || !instructionsLabel) return;
            lastRecyclePhase = boss && boss.IsDead;
            if (lastRecyclePhase)
            {
                phaseLabel.text = "階段 2｜資源回收";
                instructionsLabel.text =
                    "① 搜尋 Boss 戰後的 5 件回收物\n" +
                    "② 瞄準物品，依提示拾取或放下\n" +
                    "③ 完成 5 道資源循環題目";
            }
            else
            {
                phaseLabel.text = "階段 1｜機械 Boss 戰";
                instructionsLabel.text =
                    "① 取得並裝備電漿武器\n" +
                    "② 瞄準 Boss 攻擊，移動閃避\n" +
                    "③ 留意畫面上方的 Boss 血條";
            }
        }

        private void Update()
        {
            if (flash)
                flash.color = Time.unscaledTime < flashUntil
                    ? new Color(0.8f, 0f, 0f, 0.12f)
                    : Color.clear;

            if (boss && boss.IsDead != lastRecyclePhase)
                RefreshInstructions();
        }

        private void OnDestroy()
        {
            if (player) player.Damaged -= OnDamaged;
            if (root) Destroy(root.gameObject);
            if (flash) Destroy(flash.gameObject);
        }
    }
}
