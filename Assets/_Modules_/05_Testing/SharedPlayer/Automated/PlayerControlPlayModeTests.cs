using System;
using System.Collections;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using SDGAdventure.PlayerTest;
using SDGAdventure.SharedPlayer;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace SDGAdventure.PlayerControlTests
{
    public sealed class PlayerControlPlayModeTests
    {
        private const string PrefabPath = "Assets/_Modules_/01_Shared/Player/Prefabs/SharedPlayer.prefab";

        [TearDown]
        public void RestoreTimeScale() => Time.timeScale = 1f;

        [Test]
        public void DefaultsRebindConflictSaveReloadAndRestore()
        {
            InputActionAsset source = Resources.Load<InputActionAsset>("SDGPlayerControls");
            Assert.IsNotNull(source);
            InputActionAsset actions = UnityEngine.Object.Instantiate(source);
            string key = "SDGAdventure.FullPlayerTest." + Guid.NewGuid().ToString("N");
            var store = new PlayerSettingsStore(key);
            try
            {
                var settings = new PlayerKeybindingController(actions, store);
                InputActionMap gameplay = actions.FindActionMap("Gameplay");
                InputAction move = gameplay.FindAction("Move");
                Assert.AreEqual("<Keyboard>/w", move.bindings[1].effectivePath);
                Assert.AreEqual("<Keyboard>/s", move.bindings[2].effectivePath);
                Assert.AreEqual("<Keyboard>/a", move.bindings[3].effectivePath);
                Assert.AreEqual("<Keyboard>/d", move.bindings[4].effectivePath);
                Assert.AreEqual("<Mouse>/delta", gameplay.FindAction("Look").bindings[0].effectivePath);
                Assert.AreEqual("<Keyboard>/space", gameplay.FindAction("Jump").bindings[0].effectivePath);
                Assert.AreEqual("<Keyboard>/leftShift", gameplay.FindAction("Sprint").bindings[0].effectivePath);
                Assert.AreEqual("<Keyboard>/leftCtrl", gameplay.FindAction("Crouch").bindings[0].effectivePath);
                Assert.AreEqual("<Keyboard>/f", gameplay.FindAction("Interact").bindings[0].effectivePath);
                Assert.AreEqual("<Keyboard>/p", gameplay.FindAction("TaskList").bindings[0].effectivePath);
                Assert.AreEqual("<Keyboard>/p", actions.FindActionMap("UI").FindAction("TaskList").bindings[0].effectivePath);
                Assert.AreEqual("<Keyboard>/escape", actions.FindActionMap("Global").FindAction("Pause").bindings[0].effectivePath);
                Assert.AreEqual(2f, settings.Settings.mouseSensitivity);
                Assert.IsFalse(settings.TryApply("Gameplay", "Interact", 0, "<Keyboard>/space", out string conflict));
                StringAssert.Contains("Gameplay/Jump", conflict);
                Assert.IsFalse(settings.TryApply("Gameplay", "TaskList", 0, "<Keyboard>/f", out conflict));
                StringAssert.Contains("Gameplay/Interact", conflict);
                Assert.IsFalse(settings.TryApply("Gameplay", "TaskList", 0, "<Keyboard>/enter", out conflict));
                StringAssert.Contains("UI/Submit", conflict);
                Assert.IsTrue(settings.TryApply("Gameplay", "Interact", 0, "<Keyboard>/n", out string error), error);
                Assert.IsTrue(settings.TryApply("Gameplay", "TaskList", 0, "<Keyboard>/o", out error), error);
                Assert.AreEqual("<Keyboard>/O", actions.FindActionMap("UI").FindAction("TaskList").bindings[0].effectivePath);
                settings.SetMouseSensitivity(4.5f);
                settings.SetSprintMode(ButtonMode.Toggle);
                settings.SetCrouchMode(ButtonMode.Toggle);

                InputActionAsset reloaded = UnityEngine.Object.Instantiate(source);
                try
                {
                    var loaded = new PlayerKeybindingController(reloaded, store);
                    Assert.AreEqual("<Keyboard>/N", reloaded.FindActionMap("Gameplay").FindAction("Interact").bindings[0].effectivePath);
                    Assert.AreEqual("<Keyboard>/O", reloaded.FindActionMap("Gameplay").FindAction("TaskList").bindings[0].effectivePath);
                    Assert.AreEqual("<Keyboard>/O", reloaded.FindActionMap("UI").FindAction("TaskList").bindings[0].effectivePath);
                    Assert.AreEqual(4.5f, loaded.Settings.mouseSensitivity);
                    Assert.AreEqual(ButtonMode.Toggle, loaded.Settings.sprintMode);
                    Assert.AreEqual(ButtonMode.Toggle, loaded.Settings.crouchMode);
                    loaded.RestoreDefaults();
                    Assert.AreEqual("<Keyboard>/f", reloaded.FindActionMap("Gameplay").FindAction("Interact").bindings[0].effectivePath);
                    Assert.AreEqual("<Keyboard>/p", reloaded.FindActionMap("Gameplay").FindAction("TaskList").bindings[0].effectivePath);
                    Assert.AreEqual("<Keyboard>/p", reloaded.FindActionMap("UI").FindAction("TaskList").bindings[0].effectivePath);
                    Assert.AreEqual(2f, loaded.Settings.mouseSensitivity);
                    Assert.AreEqual(ButtonMode.Hold, loaded.Settings.sprintMode);
                    Assert.IsFalse(PlayerPrefs.HasKey(key));
                }
                finally { UnityEngine.Object.DestroyImmediate(reloaded); }
            }
            finally
            {
                store.Reset(actions);
                UnityEngine.Object.DestroyImmediate(actions);
            }
        }

        [Test]
        public void InteractiveRebindCanBeCancelledWithoutChangingKey()
        {
            InputActionAsset actions = UnityEngine.Object.Instantiate(Resources.Load<InputActionAsset>("SDGPlayerControls"));
            string key = "SDGAdventure.FullPlayerTest." + Guid.NewGuid().ToString("N");
            var store = new PlayerSettingsStore(key);
            try
            {
                var bindings = new PlayerKeybindingController(actions, store);
                bool called = false;
                var operation = bindings.BeginInteractiveRebind("Gameplay", "Interact", 0,
                    (accepted, _) =>
                    {
                        called = true;
                        Assert.IsFalse(accepted);
                    });
                operation.Cancel();
                Assert.IsTrue(called);
                Assert.AreEqual("<Keyboard>/f", actions.FindActionMap("Gameplay")
                    .FindAction("Interact").bindings[0].effectivePath);
                Assert.IsFalse(PlayerPrefs.HasKey(key));
            }
            finally
            {
                store.Reset(actions);
                UnityEngine.Object.DestroyImmediate(actions);
            }
        }

        [Test]
        public void VersionOneSettingsMigrateWithoutLosingModes()
        {
            InputActionAsset actions = UnityEngine.Object.Instantiate(Resources.Load<InputActionAsset>("SDGPlayerControls"));
            string key = "SDGAdventure.FullPlayerTest." + Guid.NewGuid().ToString("N");
            var store = new PlayerSettingsStore(key);
            try
            {
                var old = new PlayerSettingsData
                {
                    version = 1,
                    sprintMode = ButtonMode.Toggle,
                    crouchMode = ButtonMode.Hold,
                };
                actions.FindActionMap("Gameplay").FindAction("Interact")
                    .ApplyBindingOverride(0, "<Keyboard>/g");
                old.bindingOverridesJson = actions.SaveBindingOverridesAsJson();
                string payload = "1|1|0|" + old.bindingOverridesJson;
                using (SHA256 hash = SHA256.Create())
                    old.checksum = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(payload))).Replace("-", "");
                PlayerPrefs.SetString(key, JsonUtility.ToJson(old));
                PlayerPrefs.Save();
                PlayerSettingsData migrated = store.Load(actions);
                Assert.AreEqual(PlayerSettingsData.CurrentVersion, migrated.version);
                Assert.AreEqual(ButtonMode.Toggle, migrated.sprintMode);
                Assert.AreEqual(2f, migrated.mouseSensitivity);
                Assert.AreEqual("<Keyboard>/g", actions.FindActionMap("Gameplay")
                    .FindAction("Interact").bindings[0].effectivePath);
                Assert.IsTrue(PlayerPrefs.HasKey(key));
            }
            finally
            {
                store.Reset(actions);
                UnityEngine.Object.DestroyImmediate(actions);
            }
        }

        [Test]
        public void TaskJournalAcceptsLevelDataAndUpdatesProgress()
        {
            GameObject host = new GameObject("TaskJournalTest");
            try
            {
                PlayerTaskJournal journal = host.AddComponent<PlayerTaskJournal>();
                int changed = 0;
                journal.Changed += () => changed++;
                journal.SetTasks(new[]
                {
                    new PlayerTaskItem("first", "第一項", "示範內容"),
                    new PlayerTaskItem("second", "第二項", "另一段內容")
                });
                Assert.AreEqual(2, journal.TotalCount);
                Assert.AreEqual(0, journal.CompletedCount);
                Assert.IsTrue(journal.SetCompleted("first"));
                Assert.AreEqual(1, journal.CompletedCount);
                Assert.IsTrue(journal.Tasks[0].completed);
                Assert.IsFalse(journal.SetCompleted("missing"));
                Assert.AreEqual(2, changed);
                Assert.Throws<ArgumentException>(() => journal.SetTasks(new[]
                {
                    new PlayerTaskItem("same", "甲", ""),
                    new PlayerTaskItem("same", "乙", "")
                }));
                Assert.AreEqual(2, journal.TotalCount);
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        [UnityTest]
        public IEnumerator CrouchChangesColliderAndBlockedCeilingPreventsStanding()
        {
            GameObject floor = Floor(new Vector3(100f, 0f, 100f));
            GameObject playerObject = Player(new Vector3(100f, 0f, 100f));
            yield return null;
            var controller = playerObject.GetComponent<SharedFirstPersonController>();
            controller.TickMotor(Vector2.zero, false, true, false, 0.5f);
            Assert.AreEqual(1.1f, controller.CurrentHeight, 0.02f);
            Assert.AreEqual(2f, controller.CurrentSpeed);
            GameObject ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ceiling.transform.position = new Vector3(100f, 1.4f, 100f);
            ceiling.transform.localScale = new Vector3(2f, 0.2f, 2f);
            Physics.SyncTransforms();
            Assert.IsFalse(controller.CanStand());
            controller.TickMotor(Vector2.zero, false, false, false, 0.5f);
            Assert.AreEqual(1.1f, controller.CurrentHeight, 0.02f);
            UnityEngine.Object.Destroy(ceiling);
            yield return null;
            Assert.IsTrue(controller.CanStand());
            controller.TickMotor(Vector2.zero, true, false, false, 0.5f);
            Assert.AreEqual(controller.StandingHeight, controller.CurrentHeight, 0.02f);
            Assert.AreEqual(7f, controller.CurrentSpeed);
            UnityEngine.Object.Destroy(playerObject);
            UnityEngine.Object.Destroy(floor);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TestTunnelBlocksStandingAndAllowsCrouchedTraversal()
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(200f, -0.2f, 5f);
            ground.transform.localScale = new Vector3(8f, 0.4f, 20f);
            GameObject roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roof.transform.position = new Vector3(200f, 1.35f, 5f);
            roof.transform.localScale = new Vector3(2.95f, 0.3f, 6f);
            GameObject left = GameObject.CreatePrimitive(PrimitiveType.Cube);
            left.transform.position = new Vector3(198.65f, 0.7f, 5f);
            left.transform.localScale = new Vector3(0.25f, 1.4f, 6f);
            GameObject right = GameObject.CreatePrimitive(PrimitiveType.Cube);
            right.transform.position = new Vector3(201.35f, 0.7f, 5f);
            right.transform.localScale = new Vector3(0.25f, 1.4f, 6f);
            Physics.SyncTransforms();
            GameObject playerObject = Player(new Vector3(200f, 0f, 0f));
            yield return null;
            var controller = playerObject.GetComponent<SharedFirstPersonController>();
            CharacterController motor = playerObject.GetComponent<CharacterController>();
            Assert.AreEqual(1.2f, roof.GetComponent<Collider>().bounds.min.y, 0.01f);
            Assert.AreEqual(0f, ground.GetComponent<Collider>().bounds.max.y, 0.01f);
            Assert.AreEqual(0.3f, motor.radius, 0.001f);
            Assert.AreEqual(0.08f, motor.skinWidth, 0.001f);

            for (int i = 0; i < 100; i++)
                controller.TickMotor(Vector2.up, false, false, false, 0.02f);
            Assert.Less(playerObject.transform.position.z, 2f, "Standing player entered the low tunnel.");

            controller.TickMotor(Vector2.zero, false, true, false, 0.5f);
            Assert.AreEqual(1.1f, motor.height, 0.02f);
            Assert.AreEqual(0.55f, motor.center.y, 0.02f);
            Assert.AreEqual(0.05f, motor.stepOffset, 0.001f);
            for (int i = 0; i < 120; i++)
                controller.TickMotor(Vector2.up, false, true, false, 0.02f);
            Assert.Greater(playerObject.transform.position.z, 3f,
                "Crouched player could not enter the tunnel; z=" + playerObject.transform.position.z);
            controller.TickMotor(Vector2.zero, false, false, false, 0.5f);
            Assert.AreEqual(1.1f, motor.height, 0.02f, "Player stood up under the roof.");
            for (int i = 0; i < 170; i++)
                controller.TickMotor(Vector2.up, false, true, false, 0.02f);
            Assert.Greater(playerObject.transform.position.z, 8.5f,
                "Crouched player could not leave the tunnel; z=" + playerObject.transform.position.z);
            controller.TickMotor(Vector2.zero, false, false, false, 0.5f);
            Assert.AreEqual(1.8f, motor.height, 0.02f, "Player did not stand after leaving the tunnel.");
            Assert.AreEqual(0.3f, motor.stepOffset, 0.001f);
            UnityEngine.Object.Destroy(playerObject);
            UnityEngine.Object.Destroy(ground);
            UnityEngine.Object.Destroy(roof);
            UnityEngine.Object.Destroy(left);
            UnityEngine.Object.Destroy(right);
            yield return null;
        }

        [UnityTest]
        public IEnumerator JumpMovesPlayerUpFromGround()
        {
            GameObject floor = Floor(new Vector3(120f, 0f, 120f));
            GameObject playerObject = Player(new Vector3(120f, 0f, 120f));
            yield return null;
            var controller = playerObject.GetComponent<SharedFirstPersonController>();
            for (int i = 0; i < 10 && !controller.IsGrounded; i++)
            {
                controller.TickMotor(Vector2.zero, false, false, false, 0.02f);
                yield return null;
            }
            Assert.IsTrue(controller.IsGrounded, "Player did not settle on the test floor. Player y=" +
                playerObject.transform.position.y + ", floor=" + floor.GetComponent<Collider>().bounds);
            float before = playerObject.transform.position.y;
            controller.TickMotor(Vector2.zero, false, false, true, 0.02f);
            Assert.Greater(playerObject.transform.position.y, before + 0.01f);
            UnityEngine.Object.Destroy(playerObject);
            UnityEngine.Object.Destroy(floor);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PauseBlocksGameplayAndRestoresCursorAndTime()
        {
            // A previous test can leave a Level03 player in the loaded scene.
            // Temporarily detach its menu subscription, then restore its state.
            var priorMenus = UnityEngine.Object.FindObjectsByType<PlayerPauseMenu>(FindObjectsSortMode.None)
                .Where(m => m.isActiveAndEnabled).ToArray();
            var originalInput = PlayerInputService.Instance;
            var originalContext = originalInput.Context;
            var originalLock = Cursor.lockState;
            bool originalVisible = Cursor.visible;
            float originalTimeScale = Time.timeScale;
            GameObject floor = null;
            GameObject playerObject = null;
            GameObject returnPoint = null;
            try
            {
            foreach (var prior in priorMenus) prior.enabled = false;
            floor = Floor(new Vector3(140f, 0f, 140f));
            playerObject = Player(new Vector3(140f, 0f, 140f));
            yield return null;
            var menu = playerObject.GetComponent<PlayerPauseMenu>();
            var input = PlayerInputService.Instance;
            Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType<PlayerPauseMenu>(
                FindObjectsSortMode.None).Count(m => m.isActiveAndEnabled), "The input test requires one active player menu.");
            Assert.AreEqual(PlayerInputContext.Gameplay, input.Context);
            menu.Pause();
            Assert.AreEqual(PlayerMenuState.Paused, menu.State);
            Assert.AreEqual(0f, Time.timeScale);
            Assert.AreEqual(PlayerInputContext.UI, input.Context);
            Assert.IsFalse(input.Actions.FindActionMap("Gameplay").enabled);
            Assert.IsFalse(input.Actions.FindActionMap("Global").enabled);
            Assert.IsTrue(input.Actions.FindActionMap("UI").enabled);
            Assert.AreEqual(CursorLockMode.None, Cursor.lockState);
            Assert.IsTrue(Cursor.visible);
            Vector3 position = playerObject.transform.position;
            yield return null;
            Assert.AreEqual(position, playerObject.transform.position);
            menu.OpenSettings();
            Assert.AreEqual(PlayerMenuState.Settings, menu.State);
            Assert.AreEqual(PlayerInputContext.UI, input.Context);
            menu.BackToPause();
            menu.Resume();
            Assert.AreEqual(1f, Time.timeScale);
            Assert.AreEqual(PlayerInputContext.Gameplay, input.Context);
            Assert.AreEqual(PlayerMenuState.Playing, menu.State);
            returnPoint = new GameObject("TestReturnPoint");
            returnPoint.transform.position = new Vector3(140f, 0f, 140f);
            menu.SetTestReturnPoint(returnPoint.transform);
            playerObject.GetComponent<SharedFirstPersonController>()
                .Teleport(new Vector3(142f, 0f, 140f), Quaternion.identity);
            menu.Pause();
            menu.ReturnToTestStart();
            Assert.AreEqual(returnPoint.transform.position, playerObject.transform.position);
            Assert.AreEqual(PlayerMenuState.Playing, menu.State);
            }
            finally
            {
                if (playerObject) { playerObject.SetActive(false); UnityEngine.Object.Destroy(playerObject); }
                if (floor) UnityEngine.Object.Destroy(floor);
                if (returnPoint) UnityEngine.Object.Destroy(returnPoint);
                foreach (var prior in priorMenus) if (prior) prior.enabled = true;
                if (originalInput) originalInput.SetContext(originalContext);
                PlayerCursor.LockState = originalLock;
                Cursor.visible = originalVisible;
                Time.timeScale = originalTimeScale;
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator RepeatedPauseSettingsCyclesRestoreGameplayState()
        {
            GameObject floor = Floor(new Vector3(180f, 0f, 180f));
            GameObject playerObject = Player(new Vector3(180f, 0f, 180f));
            yield return null;
            var menu = playerObject.GetComponent<PlayerPauseMenu>();
            var input = PlayerInputService.Instance;
            menu.Journal.SetTasks(new[] { new PlayerTaskItem("demo", "示範", "進度測試") });
            Canvas[] initialCanvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            var ownedCanvases = playerObject.GetComponentsInChildren<Canvas>(true);
            Assert.AreEqual(1, ownedCanvases.Count(c => c.name == "PlayerMenuCanvas"));
            Assert.AreEqual(1, ownedCanvases.Count(c => c.name == "PlayerHudCanvas"));
            for (int cycle = 0; cycle < 5; cycle++)
            {
                menu.Pause();
                yield return null;
                Assert.IsTrue(menu.View.IsVisible);
                Assert.IsFalse(playerObject.GetComponent<PlayerHudCanvas>().IsVisible);
                Assert.IsNotNull(menu.View.Panel.GetComponent<Image>());
                Assert.AreEqual(1f, menu.View.Panel.GetComponent<Image>().color.a);
                Vector3[] corners = new Vector3[4];
                menu.View.Panel.GetWorldCorners(corners);
                Assert.GreaterOrEqual(corners[0].x, 0f);
                Assert.GreaterOrEqual(corners[0].y, 0f);
                Assert.LessOrEqual(corners[2].x, Screen.width + 1f);
                Assert.LessOrEqual(corners[2].y, Screen.height + 1f);
                menu.OpenSettings();
                Assert.AreEqual(PlayerMenuState.Settings, menu.State);
                Assert.AreEqual(PlayerInputContext.UI, input.Context);
                Assert.AreEqual(0f, Time.timeScale);
                menu.BackToPause();
                menu.Resume();
                Assert.AreEqual(PlayerMenuState.Playing, menu.State);
                Assert.AreEqual(PlayerInputContext.Gameplay, input.Context);
                Assert.AreEqual(1f, Time.timeScale);
                Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType<PlayerInputService>(
                    FindObjectsSortMode.None).Length);
                menu.OpenTasks();
                Assert.AreEqual(PlayerMenuState.Tasks, menu.State);
                Assert.AreEqual(PlayerInputContext.UI, input.Context);
                Assert.IsFalse(input.Actions.FindActionMap("Global").enabled,
                    "Esc must not also invoke Pause while the task panel is open.");
                Assert.IsTrue(input.Actions.FindActionMap("UI").FindAction("Cancel").enabled);
                Assert.IsFalse(input.Actions.FindActionMap("Gameplay").enabled);
                Assert.AreEqual(0f, Time.timeScale);
                menu.CloseTasks();
                Assert.AreEqual(PlayerMenuState.Playing, menu.State);
                Assert.AreEqual(1f, Time.timeScale);
                CollectionAssert.AreEquivalent(initialCanvases, UnityEngine.Object.FindObjectsByType<Canvas>(
                    FindObjectsSortMode.None), "Pause cycles must not create or lose canvases.");
                Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType<EventSystem>(
                    FindObjectsSortMode.None).Length);
                yield return null;
            }
            UnityEngine.Object.Destroy(playerObject);
            UnityEngine.Object.Destroy(floor);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PauseMenuHasOneResumeButtonAndPreservesOtherPanels()
        {
            GameObject floor = Floor(new Vector3(220f, 0f, 220f));
            GameObject playerObject = Player(new Vector3(220f, 0f, 220f));
            GameObject returnPoint = new GameObject("TestReturnPoint");
            returnPoint.transform.position = new Vector3(220f, 0f, 220f);
            yield return null;
            var menu = playerObject.GetComponent<PlayerPauseMenu>();
            var input = PlayerInputService.Instance;
            menu.SetTestReturnPoint(returnPoint.transform);
            for (int cycle = 0; cycle < 3; cycle++)
            {
                menu.Pause();
                Transform content = menu.View.Panel.Find("ScrollViewport/ScrollContent");
                Button[] buttons = content.GetComponentsInChildren<Button>();
                CollectionAssert.AreEqual(new[] { "繼續遊戲", "操作設定", "返回測試起點" },
                    buttons.Select(button => button.GetComponentInChildren<Text>().text).ToArray());
                Transform footer = menu.View.Panel.Find("Footer");
                Assert.IsFalse(footer.gameObject.activeSelf, "Pause must not show a second Resume button.");
                buttons[0].onClick.Invoke();
                Assert.AreEqual(PlayerMenuState.Playing, menu.State);
                Assert.AreEqual(PlayerInputContext.Gameplay, input.Context);

                menu.Pause();
                content = menu.View.Panel.Find("ScrollViewport/ScrollContent");
                content.GetComponentsInChildren<Button>()[1].onClick.Invoke();
                Assert.AreEqual(PlayerMenuState.Settings, menu.State);
                Assert.IsTrue(footer.gameObject.activeSelf);
                Assert.AreEqual("返回暫停選單", footer.GetComponentInChildren<Text>().text);
                footer.GetComponent<Button>().onClick.Invoke();
                Assert.AreEqual(PlayerMenuState.Paused, menu.State);
                Assert.IsFalse(footer.gameObject.activeSelf);
                menu.View.Panel.Find("ScrollViewport/ScrollContent")
                    .GetComponentsInChildren<Button>()[2].onClick.Invoke();
                Assert.AreEqual(PlayerMenuState.Playing, menu.State);
                Assert.AreEqual(returnPoint.transform.position, playerObject.transform.position);

                menu.OpenTasks();
                Assert.AreEqual(PlayerMenuState.Tasks, menu.State);
                Assert.IsTrue(footer.gameObject.activeSelf);
                menu.CloseTasks();
                Assert.AreEqual(PlayerInputContext.Gameplay, input.Context);
                yield return null;
            }
            UnityEngine.Object.Destroy(playerObject);
            UnityEngine.Object.Destroy(floor);
            UnityEngine.Object.Destroy(returnPoint);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TaskAndPauseKeysSwitchOnlyTheIntendedPanel()
        {
            GameObject playerObject = Player(new Vector3(240f, 0f, 240f));
            yield return null;
            var menu = playerObject.GetComponent<PlayerPauseMenu>();
            var input = PlayerInputService.Instance;
            InputSettings.BackgroundBehavior previousBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSettings.EditorInputBehaviorInPlayMode previousEditorInput =
                InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            Keyboard keyboard = Keyboard.current;
            bool addedKeyboard = keyboard == null;
            if (addedKeyboard) keyboard = InputSystem.AddDevice<Keyboard>();
            Assert.IsNotNull(keyboard);
            Assert.AreEqual(PlayerInputContext.Gameplay, input.Context);
            InputAction taskAction = input.Actions.FindActionMap("Gameplay").FindAction("TaskList");
            Assert.IsTrue(taskAction.enabled);
            Assert.Greater(taskAction.controls.Count, 0, "TaskList is not resolved to a keyboard control.");
            Assert.AreEqual("<Keyboard>/p", taskAction.bindings[0].effectivePath);
            Assert.AreSame(keyboard, taskAction.controls[0].device);
            bool taskPerformed = false;
            taskAction.performed += _ => taskPerformed = true;
            try
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.P));
                InputSystem.Update();
                Assert.IsTrue(keyboard.pKey.isPressed, "Queued P state did not reach the Input System.");
                Assert.AreEqual(PlayerInputContext.UI, input.Context,
                    "P did not switch the shared input service into UI mode.");
                Assert.IsTrue(taskPerformed, "TaskList binding did not perform on the P press.");
                Assert.AreEqual(PlayerMenuState.Tasks, menu.State);
                Assert.AreEqual(PlayerInputContext.UI, input.Context);
                Assert.AreEqual(0f, Time.timeScale);
                Assert.IsFalse(input.Actions.FindActionMap("Global").enabled);
                Assert.IsTrue(input.Actions.FindActionMap("UI").FindAction("TaskList").enabled);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();

                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.P));
                InputSystem.Update();
                Assert.AreEqual(PlayerMenuState.Playing, menu.State,
                    "The configured task key should also close Tasks.");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();

                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.P));
                InputSystem.Update();
                Assert.AreEqual(PlayerMenuState.Tasks, menu.State);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();

                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                InputSystem.Update();
                Assert.AreEqual(PlayerMenuState.Playing, menu.State,
                    "Esc should close Tasks without opening Pause in the same input event.");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();

                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                InputSystem.Update();
                Assert.AreEqual(PlayerMenuState.Paused, menu.State);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();

                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                InputSystem.Update();
                Assert.AreEqual(PlayerMenuState.Playing, menu.State);
                Assert.AreEqual(1f, Time.timeScale);
            }
            finally
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();
                if (addedKeyboard) InputSystem.RemoveDevice(keyboard);
                InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInput;
                InputSystem.settings.backgroundBehavior = previousBackground;
                UnityEngine.Object.Destroy(playerObject);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator RaycastInteractionUsesSharedInterface()
        {
            GameObject floor = Floor(new Vector3(160f, 0f, 160f));
            GameObject playerObject = Player(new Vector3(160f, 0f, 160f));
            GameObject target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.transform.position = new Vector3(160f, 1.5f, 162f);
            target.transform.localScale = new Vector3(1f, 1.8f, 1f);
            Physics.SyncTransforms();
            var interactable = target.AddComponent<PlayerControlTestInteractable>();
            yield return null;
            var interactor = playerObject.GetComponent<PlayerInteractor>();
            Camera camera = playerObject.GetComponentInChildren<Camera>();
            Ray testRay = new Ray(camera.transform.position, camera.transform.forward);
            Physics.Raycast(testRay, out RaycastHit hit, 3f);
            Assert.AreSame(interactable, interactor.FindTarget(), "Camera=" + camera.transform.position +
                " forward=" + camera.transform.forward + " hit=" + (hit.collider == null ? "none" : hit.collider.name));
            interactable.Interact(playerObject);
            Assert.AreEqual(1, interactable.InteractionCount);
            playerObject.GetComponent<PlayerPauseMenu>().Pause();
            Assert.IsFalse(interactor.TryInteract());
            Assert.AreEqual(1, interactable.InteractionCount);
            UnityEngine.Object.Destroy(playerObject);
            UnityEngine.Object.Destroy(target);
            UnityEngine.Object.Destroy(floor);
            yield return null;
        }

        private static GameObject Player(Vector3 position)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab);
            GameObject player = UnityEngine.Object.Instantiate(prefab, position, Quaternion.identity);
            Physics.SyncTransforms();
            return player;
        }

        private static GameObject Floor(Vector3 position)
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = position + Vector3.down * 0.1f;
            floor.transform.localScale = new Vector3(8f, 0.2f, 8f);
            Physics.SyncTransforms();
            return floor;
        }
    }
}
