using System;
using SDGAdventure.SharedPlayer;
using UnityEngine;

namespace SDGAdventure.FrontEnd
{
    /// <summary>
    /// Manually selected slots. F5 never silently overwrites a previously saved game.
    /// Auto slot 0 is deliberately separate from manual slots 1..12.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SDGSaveSlotsOverlayV5 : MonoBehaviour
    {
        public static SDGSaveSlotsOverlayV5 Instance { get; private set; }
        private bool visible;
        private bool saving;
        private int page;
        private int confirmSlot = -1;
        private float confirmUntil;
        private float previousScale = 1f;
        private CursorLockMode previousCursorLock;
        private bool previousCursorVisible;
        private PlayerInputContext previousContext;
        private bool hadInputService;
        private string status = "";
        private Font displayFont;

        public static bool IsOpen { get { return Instance && Instance.visible; } }
        public static void OpenSave() { if (Instance) Instance.Open(true); }
        public static void OpenLoad() { if (Instance) Instance.Open(false); }

        private void Awake()
        {
            if (Instance && Instance != this) { Destroy(this); return; }
            Instance = this;
        }
        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (displayFont) Destroy(displayFont);
        }
        private void Open(bool save)
        {
            if (visible) return;
            saving = save;
            page = Mathf.Clamp((SDGSaveProgressRuntimeV1.SelectedSlot - 1) / 4, 0, 2);
            confirmSlot = -1;
            status = "";
            previousScale = Time.timeScale;
            previousCursorLock = PlayerCursor.LockState;
            previousCursorVisible = Cursor.visible;
            PlayerInputService service = PlayerInputService.Instance;
            hadInputService = service != null;
            if (service != null)
            {
                previousContext = service.Context;
                if (service.Context == PlayerInputContext.Gameplay)
                    service.SetContext(PlayerInputContext.UI);
            }
            Time.timeScale = 0f;
            PlayerCursor.LockState = CursorLockMode.None;
            Cursor.visible = true;
            visible = true;
        }
        private void Close(bool changingScene = false)
        {
            if (!visible) return;
            visible = false;
            if (changingScene) return;
            Time.timeScale = previousScale;
            if (hadInputService && PlayerInputService.Instance != null && previousContext == PlayerInputContext.Gameplay)
                PlayerInputService.Instance.SetContext(previousContext);
            PlayerCursor.LockState = previousCursorLock;
            Cursor.visible = previousCursorVisible;
        }
        private void OnGUI()
        {
            if (!visible) return;
            if (displayFont == null)
                displayFont = Font.CreateDynamicFontFromOSFont(
                    new[] { "Microsoft JhengHei", "Microsoft YaHei", "Arial" }, 18);
            Rect backdrop = new Rect(0, 0, Screen.width, Screen.height);
            Color tint = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, .72f);
            GUI.DrawTexture(backdrop, Texture2D.whiteTexture);
            GUI.color = tint;
            float w = Mathf.Min(625f, Mathf.Max(330f, Screen.width - 24f));
            float h = Mathf.Min(605f, Mathf.Max(365f, Screen.height - 24f));
            GUILayout.Window(20261116, new Rect((Screen.width-w)/2f, (Screen.height-h)/2f, w, h),
                Window, saving ? "手動儲存 — 覆蓋原存檔／另存新檔" : "讀取遊戲進度 — 選擇存檔", GUI.skin.window);
        }
        private void Window(int id)
        {
            GUIStyle style = new GUIStyle(GUI.skin.label) { fontSize = 17, wordWrap = true };
            GUIStyle button = new GUIStyle(GUI.skin.button) { fontSize = 17, fixedHeight = 38f };
            if (displayFont != null) { style.font = displayFont; button.font = displayFont; }
            GUILayout.Label(saving ? "自動存檔與手動存檔分開保存。覆蓋原存檔需再次確認。"
                : "讀取後返回該存檔的關卡與已支援的世界狀態。", style);
            GUILayout.Space(7);
            if (!saving)
            {
                if (SDGSaveProgressRuntimeV1.TryReadSlot(0, out SDGProgressRecordV1 auto))
                {
                    if (GUILayout.Button("自動存檔｜第 " + auto.levelNumber + " 關｜" + Stamp(auto), button))
                        Load(0);
                }
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("上一頁", button)) { page = (page + 2) % 3; confirmSlot = -1; }
            GUILayout.Label("手動存檔  第 " + (page + 1) + "／3 頁", style);
            if (GUILayout.Button("下一頁", button)) { page = (page + 1) % 3; confirmSlot = -1; }
            GUILayout.EndHorizontal();
            for (int i = 0; i < 4; i++)
            {
                int slot = page * 4 + i + 1;
                bool occupied = SDGSaveProgressRuntimeV1.TryReadSlot(slot, out SDGProgressRecordV1 record);
                string caption = occupied
                    ? "手動 " + slot + "｜第 " + record.levelNumber + " 關｜" + Stamp(record)
                    : "手動 " + slot + "｜空白欄位（可另存）";
                if (saving && confirmSlot == slot && Time.unscaledTime < confirmUntil)
                    caption = "確認覆蓋手動 " + slot + "？再點一次（舊檔會備份）";
                if (GUILayout.Button(caption, button))
                {
                    if (saving) Save(slot, occupied);
                    else if (occupied) Load(slot);
                    else status = "所選欄位沒有存檔。";
                }
            }
            GUILayout.Space(6);
            if (saving && GUILayout.Button("另存新檔（自動尋找空白手動欄位）", button))
            {
                int blank = -1;
                for (int i = 1; i <= 12; i++)
                    if (!SDGSaveProgressRuntimeV1.TryReadSlot(i, out _)) { blank = i; break; }
                if (blank < 0) status = "12 個手動欄位已滿；請選擇一個既有欄位並確認覆蓋。";
                else Save(blank, false);
            }
            if (!string.IsNullOrEmpty(status)) GUILayout.Label(status, style);
            if (GUILayout.Button("取消／返回", button)) Close();
            GUI.DragWindow(new Rect(0, 0, 10000, 28));
        }
        private static string Stamp(SDGProgressRecordV1 record)
        {
            if (record == null) return "";
            if (DateTime.TryParse(record.savedUtc, out DateTime stamp))
                return stamp.ToLocalTime().ToString("MM/dd HH:mm");
            return "舊存檔";
        }
        private void Save(int slot, bool existing)
        {
            if (existing && (confirmSlot != slot || Time.unscaledTime > confirmUntil))
            {
                confirmSlot = slot;
                confirmUntil = Time.unscaledTime + 10f;
                status = "這是覆蓋操作。請再次點選同一存檔欄位確認。";
                return;
            }
            confirmSlot = -1;
            if (SDGSaveProgressRuntimeV1.TrySave(slot, out string answer))
            {
                SDGSaveProgressRuntimeV1.SelectedSlot = slot;
                Debug.Log("[SDG Save V5] " + answer);
                Close();
            }
            else
            {
                status = answer;
                Debug.LogWarning("[SDG Save V5] " + answer);
            }
        }
        private void Load(int slot)
        {
            if (SDGSaveProgressRuntimeV1.TryLoad(slot, out string answer))
            {
                SDGSaveProgressRuntimeV1.SelectedSlot = slot;
                Close(true);
            }
            else status = answer;
        }
    }
}
