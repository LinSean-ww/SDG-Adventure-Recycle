using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace SDGAdventure.Experience
{
    /// <summary>
    /// Additional top-center overlay (sorting order 1600) independent from the older small Level04CombatHud.
    /// Uses the existing BossHealth.HealthChanged event; no polling or damage rewriting.
    /// </summary>
    public sealed class SDGLevel04BossOverlay : MonoBehaviour
    {
        private BossHealth boss;
        private ToiletMechBossAI ai;
        private NavMeshAgent agent;
        private float initialCooldown;
        private float initialSpeed;
        private RectTransform barFill;
        private Text title, stage;
        private Canvas canvas;
        private int previousPhase = -1;

        public void Bind(BossHealth target)
        {
            boss = target;
            ai = boss.bossAI != null ? boss.bossAI : boss.GetComponent<ToiletMechBossAI>();
            if (ai != null)
            {
                initialCooldown = ai.attackCooldown;
                agent = ai.agent != null ? ai.agent : ai.GetComponent<NavMeshAgent>();
                if (agent != null) initialSpeed = agent.speed;
            }
            Build();
            boss.HealthChanged += Refresh;
            boss.Died += OnDied;
            Refresh();
        }

        private void Build()
        {
            RectTransform root = SDGExperienceUI.Rect("SDG_Boss_Top_Bar", null);
            SceneManager.MoveGameObjectToScene(root.gameObject, gameObject.scene);
            canvas = root.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1600;
            var scaler = root.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = .5f;

            RectTransform panel = SDGExperienceUI.Rect("BossPanel", root);
            panel.anchorMin = panel.anchorMax = new Vector2(.5f, 1f);
            panel.pivot = new Vector2(.5f, 1f);
            panel.anchoredPosition = new Vector2(0, -15f);
            panel.sizeDelta = new Vector2(770, 96);
            var bg = panel.gameObject.AddComponent<Image>();
            bg.color = new Color(.065f, .1f, .12f, .88f);
            bg.raycastTarget = false;
            var outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(.9f, .28f, .13f, .95f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            title = SDGExperienceUI.Text(panel, "BossHPLabel", "機械 Boss", 26,
                new Vector2(0, 29), new Vector2(725, 36), Color.white);
            RectTransform track = SDGExperienceUI.Rect("BossTrack", panel);
            track.anchorMin = track.anchorMax = new Vector2(.5f, .5f);
            track.pivot = new Vector2(.5f, .5f);
            track.anchoredPosition = new Vector2(0, -9);
            track.sizeDelta = new Vector2(716, 20);
            var trackImage = track.gameObject.AddComponent<Image>();
            trackImage.raycastTarget = false;
            trackImage.color = new Color(.08f, .15f, .15f, 1f);

            barFill = SDGExperienceUI.Rect("BossFill", track);
            barFill.anchorMin = new Vector2(0, 0);
            barFill.anchorMax = new Vector2(1, 1);
            barFill.pivot = new Vector2(0, .5f);
            barFill.offsetMin = barFill.offsetMax = Vector2.zero;
            var image = barFill.gameObject.AddComponent<Image>();
            image.color = new Color(.96f, .32f, .12f, 1f);
            image.raycastTarget = false;
            stage = SDGExperienceUI.Text(panel, "BossPhase", "第一階段", 15,
                new Vector2(0, -34), new Vector2(730, 25), new Color(.8f, .9f, .84f));
        }

        private void Refresh()
        {
            if (boss == null || barFill == null) return;
            float ratio = Mathf.Clamp01(boss.CurrentHealth / (float)Mathf.Max(1, boss.maxHealth));
            barFill.anchorMax = new Vector2(ratio, 1);
            barFill.offsetMin = Vector2.zero;
            barFill.offsetMax = Vector2.zero;
            title.text = "機械 Boss   " + boss.CurrentHealth + " / " + boss.maxHealth;
            int phase = ratio > .70f ? 1 : ratio > .35f ? 2 : 3;
            if (boss.IsDead || boss.CurrentHealth <= 0)
            {
                stage.text = "Boss 已擊倒 · 下一步：資源回收";
                return;
            }
            stage.text = phase == 1 ? "階段 1｜觀察移動與攻擊" :
                         phase == 2 ? "階段 2｜Boss 加速，注意閃避" : "階段 3｜最終攻勢，保持距離";
            if (phase != previousPhase)
            {
                previousPhase = phase;
                // Modest difficulty progression, without touching the original attack hit/damage logic.
                if (ai != null)
                    ai.attackCooldown = Mathf.Max(1.6f, initialCooldown * (phase == 3 ? .78f : phase == 2 ? .90f : 1f));
                if (agent != null && initialSpeed > .01f)
                    agent.speed = initialSpeed * (phase == 3 ? 1.18f : phase == 2 ? 1.09f : 1f);
            }
        }

        private void OnDied() { Refresh(); }
        private void OnDestroy()
        {
            if (boss != null) { boss.HealthChanged -= Refresh; boss.Died -= OnDied; }
            if (ai != null) ai.attackCooldown = initialCooldown;
            if (agent != null && initialSpeed > 0f) agent.speed = initialSpeed;
            if (canvas != null) Destroy(canvas.gameObject);
        }
    }
}
