using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SDGAdventure.SharedPlayer
{
    public sealed class PlayerKeybindingController
    {
        private readonly InputActionAsset actions;
        private readonly PlayerSettingsStore store;
        public PlayerSettingsData Settings { get; private set; }

        public PlayerKeybindingController(InputActionAsset actions, PlayerSettingsStore store)
        {
            this.actions = actions != null ? actions : throw new ArgumentNullException(nameof(actions));
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            Settings = store.Load(actions);
            // Older versions stored E for interaction before right lean became configurable.
            var interact = FindAction("Gameplay", "Interact");
            var rightLean = FindAction("Gameplay", "LeanRight");
            if (interact != null && rightLean != null &&
                string.Equals(interact.bindings[0].effectivePath, "<Keyboard>/e", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(rightLean.bindings[0].effectivePath, "<Keyboard>/e", StringComparison.OrdinalIgnoreCase))
                interact.ApplyBindingOverride(0, "<Keyboard>/f");
            MirrorBinding("TaskList");
            MirrorBinding("Inventory");
            ResolveDropCollision();
        }

        // Preserve existing user bindings when the newly introduced default G
        // overlaps an older override. Move only Drop to a free keyboard key.
        private void ResolveDropCollision()
        {
            InputAction drop = FindAction("Gameplay", "Drop");
            if (drop == null || drop.bindings.Count == 0) return;
            bool Occupied(string path)
            {
                foreach (InputActionMap map in actions.actionMaps)
                {
                    if (!CanCoexist("Gameplay", map.name)) continue;
                    foreach (InputAction action in map.actions)
                    {
                        if (action == drop) continue;
                        foreach (InputBinding binding in action.bindings)
                            if (!binding.isComposite && string.Equals(binding.effectivePath, path, StringComparison.OrdinalIgnoreCase)) return true;
                    }
                }
                return false;
            }
            if (!Occupied(drop.bindings[0].effectivePath)) return;
            foreach (string key in new[] { "h", "j", "k", "l", "b", "n", "v", "x", "c", "y", "u", "i", "o", "t" })
            {
                string path = "<Keyboard>/" + key;
                if (Occupied(path)) continue;
                drop.ApplyBindingOverride(0, path);
                store.Save(actions, Settings);
                Debug.LogWarning("Drop binding moved to " + key.ToUpperInvariant() + " to preserve an existing key binding.");
                return;
            }
            // No silent collision: leave Drop unbound until the player chooses a key.
            drop.ApplyBindingOverride(0, string.Empty);
            store.Save(actions, Settings);
            Debug.LogWarning("Drop binding is unassigned because available keys are already in use.");
        }

        public void SetSprintMode(ButtonMode mode)
        {
            if (!ValidMode(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
            Settings.sprintMode = mode;
            store.Save(actions, Settings);
        }

        public void SetCrouchMode(ButtonMode mode)
        {
            if (!ValidMode(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
            Settings.crouchMode = mode;
            store.Save(actions, Settings);
        }

        public void SetMouseSensitivity(float value)
        {
            if (float.IsNaN(value) || value < 0.1f || value > 10f)
                throw new ArgumentOutOfRangeException(nameof(value));
            Settings.mouseSensitivity = value;
            store.Save(actions, Settings);
        }

        public bool TryApply(string mapName, string actionName, int bindingIndex, string path, out string error)
        {
            InputAction action = FindAction(mapName, actionName);
            if (action == null || bindingIndex < 0 || bindingIndex >= action.bindings.Count ||
                action.bindings[bindingIndex].isComposite ||
                !action.bindings[bindingIndex].path.StartsWith("<Keyboard>/", StringComparison.OrdinalIgnoreCase))
            {
                error = "The binding is not a rebindable keyboard key.";
                return false;
            }

            if (!TryNormalizeKey(path, out string normalized))
            {
                error = "Choose a valid keyboard key.";
                return false;
            }

            foreach (InputActionMap map in actions.actionMaps)
            {
                if (!CanCoexist(action.actionMap.name, map.name)) continue;
                foreach (InputAction other in map.actions)
                {
                    for (int i = 0; i < other.bindings.Count; i++)
                    {
                        if (other == action && i == bindingIndex) continue;
                        if (other.bindings[i].isComposite) continue;
                        if (string.Equals(other.bindings[i].effectivePath, normalized, StringComparison.OrdinalIgnoreCase))
                        {
                            error = "Already bound to " + map.name + "/" + other.name + ".";
                            return false;
                        }
                    }
                }
            }

            if (mapName == "Gameplay" && (actionName == "TaskList" || actionName == "Inventory"))
            {
                InputActionMap ui = actions.FindActionMap("UI", true);
                foreach (InputAction other in ui.actions)
                {
                    if (other.name == actionName) continue;
                    for (int i = 0; i < other.bindings.Count; i++)
                    {
                        if (other.bindings[i].isComposite) continue;
                        if (!string.Equals(other.bindings[i].effectivePath, normalized,
                                StringComparison.OrdinalIgnoreCase)) continue;
                        error = "Already bound to UI/" + other.name + ".";
                        return false;
                    }
                }
            }

            action.ApplyBindingOverride(bindingIndex, normalized);
            if (mapName == "Gameplay" && (actionName == "TaskList" || actionName == "Inventory")) MirrorBinding(actionName);
            store.Save(actions, Settings);
            error = null;
            return true;
        }

        // UI can keep the returned operation to cancel capture if its panel closes.
        public InputActionRebindingExtensions.RebindingOperation BeginInteractiveRebind(
            string mapName, string actionName, int bindingIndex, Action<bool, string> completed)
        {
            InputAction action = FindAction(mapName, actionName);
            if (action == null || bindingIndex < 0 || bindingIndex >= action.bindings.Count ||
                action.bindings[bindingIndex].isComposite ||
                !action.bindings[bindingIndex].path.StartsWith("<Keyboard>/", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("The binding is not a rebindable keyboard key.");

            bool wasEnabled = action.enabled;
            if (wasEnabled) action.Disable();
            bool accepted = false;
            string failure = "Rebind cancelled.";
            var operation = action.PerformInteractiveRebinding(bindingIndex)
                .WithControlsHavingToMatchPath("<Keyboard>")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnApplyBinding((_, path) => accepted = TryApply(mapName, actionName, bindingIndex, path, out failure))
                .OnComplete(op =>
                {
                    op.Dispose();
                    if (wasEnabled) action.Enable();
                    completed?.Invoke(accepted, failure);
                })
                .OnCancel(op =>
                {
                    op.Dispose();
                    if (wasEnabled) action.Enable();
                    completed?.Invoke(false, "Rebind cancelled.");
                });
            operation.Start();
            return operation;
        }

        public string BindingDisplay(string mapName, string actionName, int bindingIndex)
        {
            InputAction action = FindAction(mapName, actionName);
            if (action == null || bindingIndex < 0 || bindingIndex >= action.bindings.Count)
                throw new ArgumentOutOfRangeException(nameof(bindingIndex));
            return action.GetBindingDisplayString(bindingIndex);
        }

        public void RestoreDefaults()
        {
            store.Reset(actions);
            Settings = new PlayerSettingsData();
        }

        private InputAction FindAction(string mapName, string actionName) =>
            actions.FindActionMap(mapName)?.FindAction(actionName);

        private void MirrorBinding(string name)
        {
            InputAction gameplayTask = FindAction("Gameplay", name);
            InputAction uiTask = FindAction("UI", name);
            if (gameplayTask == null || uiTask == null) return;
            string path = gameplayTask.bindings[0].effectivePath;
            if (!string.Equals(uiTask.bindings[0].effectivePath, path, StringComparison.OrdinalIgnoreCase))
                uiTask.ApplyBindingOverride(0, path);
        }

        private static bool ValidMode(ButtonMode mode) => mode == ButtonMode.Hold || mode == ButtonMode.Toggle;

        private static bool CanCoexist(string first, string second) =>
            first == second || (first != "UI" && second != "UI");

        private static bool TryNormalizeKey(string path, out string normalized)
        {
            normalized = null;
            const string prefix = "<Keyboard>/";
            if (string.IsNullOrEmpty(path) || !path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return false;
            string name = path.Substring(prefix.Length);
            // Input System uses paths such as /1, while its enum names are Digit1.
            if (name.Length == 1 && name[0] >= '0' && name[0] <= '9')
            {
                normalized = prefix + name;
                return true;
            }
            if (!Enum.TryParse(name, true, out Key key) || key == Key.None || !Enum.IsDefined(typeof(Key), key))
                return false;
            normalized = prefix + key;
            return true;
        }
    }
}
