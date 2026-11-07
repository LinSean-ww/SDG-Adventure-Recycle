using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace SDGAdventure.SharedPlayer
{
    public enum PlayerInputContext { Inactive, Gameplay, UI }

    // Only the action service survives scene changes. Players and cameras remain scene-owned.
    public sealed class PlayerInputService : MonoBehaviour
    {
        public static PlayerInputService Instance { get; private set; }
        public PlayerInputContext Context { get; private set; } = PlayerInputContext.Inactive;
        public PlayerKeybindingController Keybindings { get; private set; }
        public InputActionAsset Actions => actions;
        public Vector2 Move => Context == PlayerInputContext.Gameplay ? move.ReadValue<Vector2>() : Vector2.zero;
        public Vector2 Look => Context == PlayerInputContext.Gameplay ? look.ReadValue<Vector2>() : Vector2.zero;
        public bool SprintActive => Context == PlayerInputContext.Gameplay &&
            (Keybindings.Settings.sprintMode == ButtonMode.Hold ? sprint.IsPressed() : sprintToggled);
        public bool CrouchActive => Context == PlayerInputContext.Gameplay &&
            (Keybindings.Settings.crouchMode == ButtonMode.Hold ? crouch.IsPressed() : crouchToggled);

        public event Action JumpPressed;
        public event Action PronePressed;
        public event Action InteractPressed;
        public event Action TaskListPressed;
        public event Action UiTaskListPressed;
        public event Action PausePressed;
        public event Action UiCancelPressed;
        public event Action UiSubmitPressed;

        private InputActionAsset actions;
        private InputActionMap gameplay;
        private InputActionMap global;
        private InputActionMap ui;
        private InputAction move;
        private InputAction look;
        private InputAction jump;
        private InputAction sprint;
        private InputAction crouch;
        private InputAction prone;
        private InputAction interact;
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
            move = gameplay.FindAction("Move", true);
            look = gameplay.FindAction("Look", true);
            jump = gameplay.FindAction("Jump", true);
            sprint = gameplay.FindAction("Sprint", true);
            crouch = gameplay.FindAction("Crouch", true);
            prone = gameplay.FindAction("Prone", true);
            interact = gameplay.FindAction("Interact", true);
            taskList = gameplay.FindAction("TaskList", true);
            pause = global.FindAction("Pause", true);
            uiCancel = ui.FindAction("Cancel", true);
            uiSubmit = ui.FindAction("Submit", true);
            uiTaskList = ui.FindAction("TaskList", true);
            Keybindings = new PlayerKeybindingController(actions, new PlayerSettingsStore());
            // Upgrade saved legacy E interaction bindings to F, reserving E for right lean.
            for (int i = 0; i < interact.bindings.Count; i++)
            {
                if (string.Equals(interact.bindings[i].effectivePath,
                    "<Keyboard>/e", StringComparison.OrdinalIgnoreCase))
                    interact.ApplyBindingOverride(i, "<Keyboard>/f");
            }


            jump.performed += OnJump;
            sprint.performed += OnSprint;
            crouch.performed += OnCrouch;
            prone.performed += OnProne;
            interact.performed += OnInteract;
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
        private void OnJump(InputAction.CallbackContext _) => JumpPressed?.Invoke();
        private void OnProne(InputAction.CallbackContext _) => PronePressed?.Invoke();
        private void OnSprint(InputAction.CallbackContext _) { if (Keybindings.Settings.sprintMode == ButtonMode.Toggle) sprintToggled = !sprintToggled; }
        private void OnCrouch(InputAction.CallbackContext _) { if (Keybindings.Settings.crouchMode == ButtonMode.Toggle) crouchToggled = !crouchToggled; }
        private void OnInteract(InputAction.CallbackContext _) => InteractPressed?.Invoke();
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
                jump.performed -= OnJump;
                sprint.performed -= OnSprint;
                crouch.performed -= OnCrouch;
                prone.performed -= OnProne;
                interact.performed -= OnInteract;
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
