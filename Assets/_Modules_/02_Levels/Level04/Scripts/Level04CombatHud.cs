using SDGAdventure.SharedPlayer;
using UnityEngine;
using UnityEngine.UI;

namespace SDGAdventure.Level04
{
    // LEVEL04_HUD_CLEAR_TOP_RIGHT_R2
    // The duplicate top-right health bars and the R1 gameplay help panel are intentionally absent.
    // The existing central Boss HP bar and upper-left player HUD are owned by other components.
    public sealed class Level04CombatHud : MonoBehaviour
    {
        private Level04PlayerHealth player;
        private Image flash;
        private float flashUntil;

        public void Initialize(Level04PlayerHealth health, BossHealth enemy, PlayerHudCanvas hud)
        {
            player = health;
            hud.Build();
            Transform host = hud.transform.Find("PlayerHudCanvas");
            if (!host)
            {
                Debug.LogError("Level04 shared HUD canvas unavailable");
                return;
            }

            // Preserve the pre-existing damage-feedback effect only.
            // Do not create a top-right layout root, health bars, or help panel.
            GameObject flashObject = new GameObject("Level04DamageFlash", typeof(RectTransform), typeof(Image));
            RectTransform flashRect = flashObject.GetComponent<RectTransform>();
            flashRect.SetParent(host, false);
            flashRect.SetAsFirstSibling();
            flashRect.anchorMin = Vector2.zero;
            flashRect.anchorMax = Vector2.one;
            flashRect.offsetMin = Vector2.zero;
            flashRect.offsetMax = Vector2.zero;
            flash = flashObject.GetComponent<Image>();
            flash.color = Color.clear;
            flash.raycastTarget = false;

            if (player) player.Damaged += OnDamaged;
        }

        private void OnDamaged()
        {
            flashUntil = Time.unscaledTime + 0.18f;
        }

        private void Update()
        {
            if (flash)
                flash.color = Time.unscaledTime < flashUntil
                    ? new Color(0.8f, 0f, 0f, 0.12f)
                    : Color.clear;
        }

        private void OnDestroy()
        {
            if (player) player.Damaged -= OnDamaged;
            if (flash) Destroy(flash.gameObject);
        }
    }
}
