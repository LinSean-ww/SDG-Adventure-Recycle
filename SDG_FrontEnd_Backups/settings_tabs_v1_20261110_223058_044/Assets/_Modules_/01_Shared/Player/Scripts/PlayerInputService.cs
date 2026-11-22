using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace SDGAdventure.SharedPlayer
{
    // Production reads the OS state, so losing focus still blocks gameplay.
    // The requested state is recorded separately for UI and automated checks.
    public static class PlayerCursor
    {
        public static CursorLockMode RequestedLockState { get; private set; }
#if UNITY_EDITOR
        private static bool simulated;
        private static CursorLockMode simulatedState;
        private static CursorLockMode previousRequestedState;
        public static void BeginSimulation(CursorLockMode initialState)
        {
            if (simulated) throw new InvalidOperationException("Cursor simulation is already active.");
            previousRequestedState = RequestedLockState;
            simulated = true;
            simulatedState = initialState;
            RequestedLockState = initialState;
        }
        public static void EndSimulation()
        {
            if (!simulated) return;
            simulated = false;
            RequestedLockState = previousRequestedState;
        }
#endif
        public static CursorLockMode LockState
        {
            get
            {
#if UNITY_EDITOR
                if (simulated) return simulatedState;
#endif
                return Cursor.lockState;
            }
            set
            {
                RequestedLockState = value;
#if UNITY_EDITOR
                if (simulated) { simulatedState = value; return; }
#endif
                Cursor.lockState = value;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
#if UNITY_EDITOR
            simulated = false;
#endif
            RequestedLockState = CursorLockMode.None;
        }
    }

    public enum PlayerInputContext { Inactive, Gameplay, UI }

    // Only the action service survives scene changes. Players and cameras remain scene-owned.
    public sealed class PlayerInputService : MonoBehaviour
    {
        public static PlayerInputService Instance { get; private set; }
        public PlayerInputContext Context { get; private set; } = PlayerInputContext.Inactive;
        public PlayerKeybindingController Keybindings { get; private set; }
        private Func<bool> gameplayActionBlocker;
        public bool GameplayActionsBlocked => gameplayActionBlocker != null && gameplayActionBlocker();
        public void SetGameplayActionBlocker(Func<bool> blocker) => gameplayActionBlocker = blocker;
        public void ClearGameplayActionBlocker(Func<bool> blocker)
        { if (gameplayActionBlocker == blocker) gameplayActionBlocker = null; }
        public InputActionAsset Actions => actions;
        public Vector2 Move => Context == PlayerInputContext.Gameplay ? move.ReadValue<Vector2>() : Vector2.zero;
        public Vector2 Look => Context == PlayerInputContext.Gameplay ? look.ReadValue<Vector2>() : Vector2.zero;
        public bool SprintActive => Context == PlayerInputContext.Gameplay &&
            (Keybindings.Settings.sprintMode == ButtonMode.Hold ? sprint.IsPressed() : sprintToggled);
        public bool CrouchActive => Context == PlayerInputContext.Gameplay &&
            (Keybindings.Settings.crouchMode == ButtonMode.Hold ? crouch.IsPressed() : crouchToggled);

        public bool InteractWasPressed => Context == PlayerInputContext.Gameplay && interact.WasPressedThisFrame();
        public bool PrimaryPressed => Context == PlayerInputContext.Gameplay && primary.WasPressedThisFrame();
        public bool SecondaryPressed => Context == PlayerInputContext.Gameplay && secondary.WasPressedThisFrame();
        public bool SecondaryHeld => Context == PlayerInputContext.Gameplay && secondary.IsPressed();
        public bool LeanLeftActive => Context == PlayerInputContext.Gameplay && leanLeft.IsPressed();
        public bool LeanRightActive => Context == PlayerInputContext.Gameplay && leanRight.IsPressed();
        public bool InventoryWasPressed => Context == PlayerInputContext.Gameplay ? inventory.WasPressedThisFrame() :
            Context == PlayerInputContext.UI && uiInventory.WasPressedThisFrame();
        public bool SlotWasPressed(int slot) => Context == PlayerInputContext.Gameplay &&
            slot >= 1 && slot <= slots.Length && slots[slot - 1].WasPressedThisFrame();
        public event Action InventoryPressed;
        // Slot numbers are 1-based.
        public event Action<int> SlotPressed;
        public bool ReloadWasPressed => Context == PlayerInputContext.Gameplay && reload.WasPressedThisFrame();
        public event Action ReloadPressed;
        public bool DropWasPressed => Context == PlayerInputContext.Gameplay && drop.WasPressedThisFrame();
        public event Action DropPressed;
        public event Action JumpPressed;
        public event Action PronePressed;
        public event Action InteractPressed;
        public event Action HeadlampPressed;
        public event Action TaskListPressed;
        public event Action UiTaskListPressed;
        public event Action PausePressed;
        public event Action UiCancelPressed;
        public event Action UiSubmitPressed;

        private InputActionAsset actions;
        private InputActionMap gameplay;
        private InputActionMap global;
        private InputActionMap ui;
        private InputAction leanLeft, leanRight, inventory, uiInventory;
        private InputAction[] slots;
        private InputAction primary, secondary;
        private InputAction move;
        private InputAction look;
        private InputAction reload;
        private InputAction drop;
        private InputAction jump;
        private InputAction sprint;
        private InputAction crouch;
        private InputAction prone;
        private InputAction interact;
        private InputAction headlamp;
        private InputAction taskList;
        private InputAction pause;
        private InputAction uiCancel;
        private InputAction uiSubmit;
        private InputAction uiTaskList;
        private bool sprintToggled;
        private bool crouchToggled;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic() => Instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var host = new GameObject("PlayerInputService");
            DontDestroyOnLoad(host);
            host.AddComponent<PlayerInputService>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InputActionAsset source = Resources.Load<InputActionAsset>("SDGPlayerControls");
            if (source == null)
            {
                Debug.LogError("SDGPlayerControls.inputactions is missing from Resources.");
                enabled = false;
                return;
            }

            actions = Instantiate(source);
            gameplay = actions.FindActionMap("Gameplay", true);
            global = actions.FindActionMap("Global", true);
            ui = actions.FindActionMap("UI", true);
            leanLeft = gameplay.FindAction("LeanLeft", true);
            leanRight = gameplay.FindAction("LeanRight", true);
            inventory = gameplay.FindAction("Inventory", true);
            uiInventory = ui.FindAction("Inventory", true);
            slots = new InputAction[5];
            for (int i = 0; i < slots.Length; i++) slots[i] = gameplay.FindAction("Slot" + (i + 1), true);
            primary = gameplay.FindAction("Fire", true);
            secondary = gameplay.FindAction("ScanAim", true);
            move = gameplay.FindAction("Move", true);
            look = gameplay.FindAction("Look", true);
            reload = gameplay.FindAction("Reload", true);
            drop = gameplay.FindAction("Drop", true);
            jump = gameplay.FindAction("Jump", true);
            sprint = gameplay.FindAction("Sprint", true);
            crouch = gameplay.FindAction("Crouch", true);
            prone = gameplay.FindAction("Prone", true);
            interact = gameplay.FindAction("Interact", true);
            headlamp = gameplay.FindAction("Headlamp", true);
            taskList = gameplay.FindAction("TaskList", true);
            pause = global.FindAction("Pause", true);
            uiCancel = ui.FindAction("Cancel", true);
            uiSubmit = ui.FindAction("Submit", true);
            uiTaskList = ui.FindAction("TaskList", true);
            Keybindings = new PlayerKeybindingController(actions, new PlayerSettingsStore());
            inventory.performed += OnInventory;
            uiInventory.performed += OnInventory;
            foreach (var slot in slots) slot.performed += OnSlot;

            reload.performed += OnReload;
            drop.performed += OnDrop;
            jump.performed += OnJump;
            sprint.performed += OnSprint;
            crouch.performed += OnCrouch;
            prone.performed += OnProne;
            interact.performed += OnInteract;
            headlamp.performed += OnHeadlamp;
            taskList.performed += OnTaskList;
            pause.performed += OnPause;
            uiCancel.performed += OnUiCancel;
            uiSubmit.performed += OnUiSubmit;
            uiTaskList.performed += OnUiTaskList;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            SetContext(PlayerInputContext.Inactive);
        }

        public void SetContext(PlayerInputContext context)
        {
            if (actions == null) return;
            if (context < PlayerInputContext.Inactive || context > PlayerInputContext.UI)
                throw new ArgumentOutOfRangeException(nameof(context));
            gameplay.Disable();
            global.Disable();
            ui.Disable();
            sprintToggled = false;
            crouchToggled = false;
            Context = context;
            if (context == PlayerInputContext.Gameplay)
            {
                gameplay.Enable();
                global.Enable();
            }
            else if (context == PlayerInputContext.UI) ui.Enable();
        }

        public void SetSprintMode(ButtonMode mode)
        {
            Keybindings.SetSprintMode(mode);
            sprintToggled = false;
        }

        public void SetCrouchMode(ButtonMode mode)
        {
            Keybindings.SetCrouchMode(mode);
            crouchToggled = false;
        }

        public void SetMouseSensitivity(float value) => Keybindings.SetMouseSensitivity(value);

        public void RestoreDefaults()
        {
            Keybindings.RestoreDefaults();
            sprintToggled = false;
            crouchToggled = false;
        }

        private void OnSceneUnloaded(Scene _) => SetContext(PlayerInputContext.Inactive);
        private void OnInventory(InputAction.CallbackContext _) => InventoryPressed?.Invoke();
        private void OnSlot(InputAction.CallbackContext context)
        {
            for (int i = 0; i < slots.Length; i++)
                if (context.action == slots[i]) { SlotPressed?.Invoke(i + 1); break; }
        }
        private void OnReload(InputAction.CallbackContext _) => ReloadPressed?.Invoke();
        private void OnDrop(InputAction.CallbackContext _) => DropPressed?.Invoke();
        private void OnJump(InputAction.CallbackContext _) => JumpPressed?.Invoke();
        private void OnProne(InputAction.CallbackContext _) => PronePressed?.Invoke();
        private void OnSprint(InputAction.CallbackContext _) { if (Keybindings.Settings.sprintMode == ButtonMode.Toggle) sprintToggled = !sprintToggled; }
        private void OnCrouch(InputAction.CallbackContext _) { if (Keybindings.Settings.crouchMode == ButtonMode.Toggle) crouchToggled = !crouchToggled; }
        private void OnInteract(InputAction.CallbackContext _) => InteractPressed?.Invoke();
        private void OnHeadlamp(InputAction.CallbackContext _) { if (Context == PlayerInputContext.Gameplay) HeadlampPressed?.Invoke(); }
        private void OnTaskList(InputAction.CallbackContext _) => TaskListPressed?.Invoke();
        private void OnPause(InputAction.CallbackContext _) => PausePressed?.Invoke();
        private void OnUiCancel(InputAction.CallbackContext _) => UiCancelPressed?.Invoke();
        private void OnUiSubmit(InputAction.CallbackContext _) => UiSubmitPressed?.Invoke();
        private void OnUiTaskList(InputAction.CallbackContext _) => UiTaskListPressed?.Invoke();

        private void OnDestroy()
        {
            if (Instance != this) return;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            if (actions != null)
            {
                actions.Disable();
                inventory.performed -= OnInventory;
                uiInventory.performed -= OnInventory;
                foreach (var slot in slots) slot.performed -= OnSlot;
                reload.performed -= OnReload;
                drop.performed -= OnDrop;
                jump.performed -= OnJump;
                sprint.performed -= OnSprint;
                crouch.performed -= OnCrouch;
                prone.performed -= OnProne;
                interact.performed -= OnInteract;
                headlamp.performed -= OnHeadlamp;
                taskList.performed -= OnTaskList;
                pause.performed -= OnPause;
                uiCancel.performed -= OnUiCancel;
                uiSubmit.performed -= OnUiSubmit;
                uiTaskList.performed -= OnUiTaskList;
                Destroy(actions);
            }
            Instance = null;
        }
    }
}
