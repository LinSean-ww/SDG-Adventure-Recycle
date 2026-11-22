using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SDGAdventure.FrontEnd
{
    /// <summary>
    /// UI R1.4, Level02 only: prevent the chapter instruction board and the
    /// star-box tracker from obscuring each other.  Deliberately does not
    /// change any level controller, HUD content, scene asset, save data or input.
    /// It only controls the alpha of two IDENTIFIED UI panels.
    /// If either panel cannot be identified, it makes no changes at all.
    /// </summary>
    [DefaultExecutionOrder(1600)]
    public sealed class SDGLevel02HelpSequencerR14 : MonoBehaviour
    {
        private const float ChapterSeconds = 8f;
        private GameObject chapterPanel;
        private GameObject trackerPanel;
        private CanvasGroup chapterGroup, trackerGroup;
        private float chapterUntil;
        private float nextScan;
        private bool ready;
        private bool changedVisibility;
        private float originalChapterAlpha, originalTrackerAlpha;
        private bool originalChapterRaycasts, originalTrackerRaycasts;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureInstalled()
        {
            if (FindObjectOfType<SDGLevel02HelpSequencerR14>() != null) return;
            GameObject go = new GameObject("SDG L2 Help Sequencer R1.4");
            DontDestroyOnLoad(go);
            go.AddComponent<SDGLevel02HelpSequencerR14>();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            Forget();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Restore();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) { Restore(); Forget(); }
        private static bool InLevel02(Scene s)
        {
            return s.IsValid() && s.isLoaded &&
                (s.path ?? "").IndexOf("/Level02/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void Forget()
        {
            chapterPanel = trackerPanel = null;
            chapterGroup = trackerGroup = null;
            nextScan = 0f;
            ready = changedVisibility = false;
        }

        private void Restore()
        {
            if (!changedVisibility) return;
            if (chapterGroup != null)
            {
                chapterGroup.alpha = originalChapterAlpha;
                chapterGroup.blocksRaycasts = originalChapterRaycasts;
            }
            if (trackerGroup != null)
            {
                trackerGroup.alpha = originalTrackerAlpha;
                trackerGroup.blocksRaycasts = originalTrackerRaycasts;
            }
            changedVisibility = false;
        }

        private void Update()
        {
            Scene active = SceneManager.GetActiveScene();
            if (!InLevel02(active)) return;
            if (!ready)
            {
                if (Time.unscaledTime < nextScan) return;
                nextScan = Time.unscaledTime + .65f;
                TryFindPanels(active);
                return;
            }
            if (WasF1Pressed()) chapterUntil = Time.unscaledTime + ChapterSeconds;
        }

        private void LateUpdate()
        {
            if (!ready || chapterGroup == null || trackerGroup == null ||
                !InLevel02(SceneManager.GetActiveScene())) return;
            // The original F1 script can hide its own chapter panel.  Respect it.
            bool chapterVisible = Time.unscaledTime < chapterUntil &&
                chapterPanel != null && chapterPanel.activeInHierarchy;
            float chapterAlpha = chapterVisible ? 1f : 0f;
            float trackerAlpha = chapterVisible ? 0f : 1f;
            if (Mathf.Abs(chapterGroup.alpha - chapterAlpha) > .01f) chapterGroup.alpha = chapterAlpha;
            if (Mathf.Abs(trackerGroup.alpha - trackerAlpha) > .01f) trackerGroup.alpha = trackerAlpha;
            // Invisible UI must not intercept mouse clicks in the Windows Player.
            chapterGroup.blocksRaycasts = chapterVisible && originalChapterRaycasts;
            trackerGroup.blocksRaycasts = !chapterVisible && originalTrackerRaycasts;
            changedVisibility = true;
        }

        private static bool WasF1Pressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.F1)) return true;
#endif
            return false;
        }

        private void TryFindPanels(Scene scene)
        {
            RectTransform chapter = null, tracker = null;
            foreach (Text text in Resources.FindObjectsOfTypeAll<Text>())
            {
                if (text == null || text.gameObject.scene != scene) continue;
                Inspect(text.text, text.transform, ref chapter, ref tracker);
            }
            foreach (TMP_Text text in Resources.FindObjectsOfTypeAll<TMP_Text>())
            {
                if (text == null || text.gameObject.scene != scene) continue;
                Inspect(text.text, text.transform, ref chapter, ref tracker);
            }
            if (chapter == null || tracker == null || chapter == tracker) return;
            chapterPanel = chapter.gameObject;
            trackerPanel = tracker.gameObject;
            chapterGroup = chapter.gameObject.GetComponent<CanvasGroup>();
            if (chapterGroup == null) chapterGroup = chapter.gameObject.AddComponent<CanvasGroup>();
            trackerGroup = tracker.gameObject.GetComponent<CanvasGroup>();
            if (trackerGroup == null) trackerGroup = tracker.gameObject.AddComponent<CanvasGroup>();
            originalChapterAlpha = chapterGroup.alpha;
            originalTrackerAlpha = trackerGroup.alpha;
            originalChapterRaycasts = chapterGroup.blocksRaycasts;
            originalTrackerRaycasts = trackerGroup.blocksRaycasts;
            chapterUntil = Time.unscaledTime + ChapterSeconds;
            ready = true;
            Debug.Log("[SDG UI R1.4] Level02 chapter/tracker panels identified; time-sequenced display enabled.");
        }

        private static void Inspect(string text, Transform target, ref RectTransform chapter, ref RectTransform tracker)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (chapter == null && text.Contains("星星箱搬運任務")) chapter = PanelRoot(target);
            if (tracker == null && text.Contains("尋找星星箱")) tracker = PanelRoot(target);
        }

        private static RectTransform PanelRoot(Transform origin)
        {
            RectTransform fallback = null;
            for (Transform parent = origin; parent != null; parent = parent.parent)
            {
                RectTransform r = parent as RectTransform;
                if (r == null || parent.GetComponent<Canvas>() != null) break;
                float w = r.rect.width, h = r.rect.height;
                if (w < 370f || w > 1600f || h < 130f || h > 650f) continue;
                if (fallback == null) fallback = r;
                if (parent.GetComponent<Image>() != null || parent.GetComponent<RawImage>() != null)
                    return r;
            }
            return fallback;
        }
    }
}
