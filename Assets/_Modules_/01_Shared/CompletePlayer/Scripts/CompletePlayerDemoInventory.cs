using SDGAdventure.SharedPlayer;
using UnityEngine;

namespace SDGAdventure.CompletePlayer
{
    // Demo-only presentation. Inventory and equipment remain the level's responsibility.
    public sealed class CompletePlayerDemoInventory : MonoBehaviour
    {
        [SerializeField] private PlayerPauseMenu menu;
        private PlayerInputService input;
        private bool inventoryOpen, toggleQueued, cancelQueued;
        private int selectedSlot = 1;
        public bool InventoryOpen => inventoryOpen;
        public int SelectedSlot => selectedSlot;

        public void Configure(PlayerPauseMenu playerMenu) => menu = playerMenu;

        private void OnEnable()
        {
            input = PlayerInputService.Instance;
            if (input == null) return;
            input.InventoryPressed += QueueInventory;
            input.SlotPressed += SelectSlot;
            input.UiCancelPressed += QueueCancel;
        }

        private void OnDisable()
        {
            if (input == null) return;
            input.InventoryPressed -= QueueInventory;
            input.SlotPressed -= SelectSlot;
            input.UiCancelPressed -= QueueCancel;
            if (inventoryOpen && menu != null && menu.State == PlayerMenuState.Playing &&
                input.Context == PlayerInputContext.UI)
                CloseInventory();
            inventoryOpen = toggleQueued = cancelQueued = false;
        }

        private void QueueInventory() => toggleQueued = true;
        private void QueueCancel() { if (inventoryOpen) cancelQueued = true; }
        private void SelectSlot(int slot)
        {
            if (!inventoryOpen && input.Context == PlayerInputContext.Gameplay &&
                menu != null && menu.State == PlayerMenuState.Playing && slot >= 1 && slot <= 5)
                selectedSlot = slot;
        }

        private void Update()
        {
            if (input == null || menu == null) return;
            if (cancelQueued && inventoryOpen) CloseInventory();
            else if (toggleQueued && menu.State == PlayerMenuState.Playing &&
                     (inventoryOpen || input.Context == PlayerInputContext.Gameplay))
            {
                if (inventoryOpen) CloseInventory();
                else
                {
                    inventoryOpen = true;
                    input.SetContext(PlayerInputContext.UI);
                    PlayerCursor.LockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
            }
            toggleQueued = cancelQueued = false;
        }

        private void CloseInventory()
        {
            inventoryOpen = false;
            input.SetContext(PlayerInputContext.Gameplay);
            PlayerCursor.LockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public string SlotLabel(int slot)
        {
            if (slot < 1 || slot > 5) throw new System.ArgumentOutOfRangeException(nameof(slot));
            string key = input.Keybindings.BindingDisplay("Gameplay", "Slot" + slot, 0);
            return (selectedSlot == slot ? "▼ " : "") + slot + " [" + key + "]\n" +
                (selectedSlot == slot ? "已選取" : "空");
        }

        private void OnGUI()
        {
            if (input == null || menu == null || menu.State != PlayerMenuState.Playing) return;
            Font old = GUI.skin.font;
            Font font = PlayerGuiFont.Load();
            if (font != null) GUI.skin.font = font;
            float width = Mathf.Min(500f, Screen.width - 20f);
            float cell = width / 5f;
            for (int i = 1; i <= 5; i++)
            {
                GUI.Box(new Rect((Screen.width - width) * .5f + (i - 1) * cell,
                    Screen.height - 70f, cell - 3f, 54f),
                    SlotLabel(i));
            }
            if (inventoryOpen)
            {
                float panelWidth = Mathf.Min(360f, Screen.width - 20f);
                var panel = new Rect((Screen.width - panelWidth) * .5f, (Screen.height - 160f) * .5f, panelWidth, 160f);
                GUI.Box(panel, "背包測試");
                GUI.Label(new Rect(panel.x + 24f, panel.y + 48f, panel.width - 48f, 30f), "狀態：已開啟");
                GUI.Label(new Rect(panel.x + 24f, panel.y + 88f, panel.width - 48f, 30f), "按背包快捷鍵關閉（Esc 亦可關閉）");
            }
            GUI.skin.font = old;
        }
    }
}
