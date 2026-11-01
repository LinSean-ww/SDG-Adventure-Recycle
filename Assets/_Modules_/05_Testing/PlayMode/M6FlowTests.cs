using System;
using System.Collections;
using System.Reflection;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace M6IntegrationTests
{
    public sealed class M6FlowTests
    {
        private const string Selection = "Assets/_Modules_/05_Testing/Scenes/M6_DevelopmentSelection.unity";
        private const string C3 = "Assets/_Modules_/02_Levels/Level03/Scenes/C_Main.unity";
        private static Type RuntimeType(string name) => Type.GetType(name + ", Assembly-CSharp", true);
        private static object Get(object target, string member)
        {
            Type type = target.GetType();
            PropertyInfo property = type.GetProperty(member);
            return property != null ? property.GetValue(target) : type.GetField(member).GetValue(target);
        }
        private static object Call(object target, string method, params object[] args) =>
            target.GetType().GetMethod(method, BindingFlags.Public | BindingFlags.Instance).Invoke(target, args);
        private static Component Find(string type) =>
            (Component)UnityEngine.Object.FindObjectOfType(RuntimeType(type));
        private static object Coordinator => RuntimeType("SDGAdventure.ModuleSystem.ModuleCoordinator")
            .GetProperty("Instance").GetValue(null);

        private static IEnumerator WaitForScene(string path, int previousHandle = -1)
        {
            float until = Time.realtimeSinceStartup + 15f;
            while ((SceneManager.GetActiveScene().path != path || SceneManager.GetActiveScene().handle == previousHandle) &&
                Time.realtimeSinceStartup < until)
                yield return null;
            Assert.AreEqual(path, SceneManager.GetActiveScene().path);
            Assert.AreNotEqual(previousHandle, SceneManager.GetActiveScene().handle, "Scene reload did not complete.");
        }

        private static void Launch(object coordinator)
        {
            object[] args = { "c3_treasure_puzzle", null };
            Assert.IsTrue((bool)Call(coordinator, "TryLaunch", args), args[1]?.ToString());
        }

        private static IEnumerator ClickVisibleButton(Transform content, string name, float scrollPosition)
        {
            // Let C3UiView.Update apply the current panel mode before rebuilding layout.
            yield return null;
            Canvas.ForceUpdateCanvases();
            ScrollRect scroll = content.parent.GetComponent<ScrollRect>();
            Assert.IsNotNull(scroll, name + ": missing ScrollRect");
            Assert.IsTrue(scroll.isActiveAndEnabled, name + ": inactive ScrollRect");
            EventSystem events = EventSystem.current;
            Assert.IsNotNull(events, name + ": missing EventSystem after scene load");
            Assert.IsTrue(events.isActiveAndEnabled, name + ": inactive EventSystem");
            Assert.IsNotNull(events.currentInputModule, name + ": missing input module");
            Assert.AreEqual("InputSystemUIInputModule", events.currentInputModule.GetType().Name, name);
            Assert.IsTrue(events.currentInputModule.isActiveAndEnabled, name + ": inactive input module");
            scroll.StopMovement();
            float hidden = Mathf.Max(0f, scroll.content.rect.height - scroll.viewport.rect.height);
            float pixels = (1f - scrollPosition) * hidden - scroll.content.anchoredPosition.y;
            if (Mathf.Abs(pixels) > 0.5f)
            {
                Assert.Greater(scroll.scrollSensitivity, 0f, name);
                var wheel = new PointerEventData(events) {
                    position = RectTransformUtility.WorldToScreenPoint(null,
                        scroll.viewport.TransformPoint(scroll.viewport.rect.center)),
                    scrollDelta = new Vector2(0f, -pixels / scroll.scrollSensitivity)
                };
                var wheelHits = new List<RaycastResult>();
                events.RaycastAll(wheel, wheelHits);
                Assert.IsNotEmpty(wheelHits, name + ": viewport received no wheel raycast");
                wheel.pointerCurrentRaycast = wheelHits[0];
                float before = scroll.content.anchoredPosition.y;
                Assert.AreSame(scroll.gameObject, ExecuteEvents.ExecuteHierarchy(
                    wheelHits[0].gameObject, wheel, ExecuteEvents.scrollHandler),
                    name + ": top raycast does not route wheel events to ScrollRect");
                Assert.That(scroll.content.anchoredPosition.y - before, Is.EqualTo(pixels).Within(0.5f),
                    name + ": wheel did not move Content to the requested position");
            }
            scroll.StopMovement();
            for (int frame = 0; frame < 5; frame++) yield return null;
            Canvas.ForceUpdateCanvases();
            Button button = content.Find(name).GetComponent<Button>();
            Assert.IsTrue(button.gameObject.activeInHierarchy, name + ": inactive button");
            Assert.IsTrue(button.IsInteractable(), name + ": button not interactable");
            var pointer = new PointerEventData(events) {
                position = RectTransformUtility.WorldToScreenPoint(null,
                    ((RectTransform)button.transform).TransformPoint(((RectTransform)button.transform).rect.center)),
                button = PointerEventData.InputButton.Left
            };
            var hits = new List<RaycastResult>();
            events.RaycastAll(pointer, hits);
            Canvas canvas = button.GetComponentInParent<Canvas>();
            GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
            Assert.IsNotNull(raycaster, name + ": missing GraphicRaycaster");
            var direct = new List<RaycastResult>();
            raycaster.Raycast(pointer, direct);
            string details = name + " scene=" + SceneManager.GetActiveScene().handle +
                " point=" + pointer.position + " screen=" + Screen.width + "x" + Screen.height +
                " content=" + scroll.content.anchoredPosition + " normalized=" + scroll.verticalNormalizedPosition +
                " cull=" + button.image.canvasRenderer.cull + " depth=" + button.image.depth +
                " raycastTarget=" + button.image.raycastTarget + " raycaster=" + raycaster.isActiveAndEnabled +
                " direct=" + string.Join(",", direct.ConvertAll(hit => hit.gameObject.name)) +
                " event=" + string.Join(",", hits.ConvertAll(hit => hit.gameObject.name));
            Assert.IsTrue(scroll.viewport.GetComponent<RectMask2D>().IsRaycastLocationValid(pointer.position, raycaster.eventCamera), details);
            Assert.IsNotEmpty(direct, details);
            Assert.AreEqual(button.gameObject, direct[0].gameObject, details);
            Assert.IsNotEmpty(hits, details);
            Assert.AreEqual(button.gameObject, hits[0].gameObject, details);
            pointer.pointerCurrentRaycast = hits[0];
            Assert.IsTrue(ExecuteEvents.Execute(hits[0].gameObject, pointer, ExecuteEvents.pointerDownHandler), details);
            Assert.IsTrue(ExecuteEvents.Execute(hits[0].gameObject, pointer, ExecuteEvents.pointerUpHandler), details);
            Assert.IsTrue(ExecuteEvents.Execute(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler), details);
        }

        [UnityTest]
        public IEnumerator PuzzleAndCompletionButtonsReceiveRealRaycasts()
        {
            yield return SceneManager.LoadSceneAsync(Selection, LoadSceneMode.Single);
            Launch(Coordinator);
            yield return WaitForScene(C3);
            for (int run = 0; run < 2; run++)
            {
                Component progress = Find("C3TreasurePuzzle.C3TreasureProgress");
                foreach (string id in new[] { "fuse", "control_chip", "filter_module" })
                    Assert.IsTrue((bool)Call(progress, "TryCollect", id, id));
                Component hud = Find("C3TreasurePuzzle.C3TreasureHud");
                Call(hud, "OpenTaskSheet");
                yield return null;
                GameObject ui = GameObject.Find("C3UiCanvas");
                Transform sheet = ui.transform.Find("TaskSheet");
                Assert.IsTrue(sheet.gameObject.activeInHierarchy);
                Assert.AreEqual(new Color(0.035f, 0.065f, 0.09f, 0.96f), sheet.GetComponent<Image>().color);
                Assert.IsTrue(sheet.Find("Viewport/Content/TaskRow0").gameObject.activeSelf);
                Call(hud, "CloseTaskSheet");
                Component console = Find("C3TreasurePuzzle.C3PuzzleConsole");
                Assert.IsTrue((bool)Call(console, "OpenPanel"));
                yield return null;
                Transform content = GameObject.Find("C3UiCanvas").transform.Find("PuzzlePanel/Viewport/Content");
                RectTransform panel = (RectTransform)content.parent.parent;
                Assert.That(panel.rect.width, Is.EqualTo(Mathf.Min(720, Screen.width - 24)).Within(0.1f));
                Assert.That(panel.rect.height, Is.EqualTo(Mathf.Min(640, Screen.height - 24)).Within(0.1f));
                Assert.That(ui.transform.Find("PuzzleBackdrop").GetComponent<Image>().color.a, Is.EqualTo(0.75f).Within(0.001f));
                yield return ClickVisibleButton(content, "Choice0", 1f);
                yield return ClickVisibleButton(content, "Retry", 0f);
                Assert.AreEqual(0, ((System.Collections.ICollection)Get(console, "Selection")).Count);
                yield return ClickVisibleButton(content, "Close", 0f);
                Assert.IsFalse((bool)Get(console, "PanelOpen"));
                Assert.IsTrue((bool)Call(console, "OpenPanel"), "Puzzle panel must reopen after Close.");
                // Slots are shuffled on every scene entry; click the semantic answer through the UI.
                Component view = Find("C3TreasurePuzzle.C3UiView");
                foreach (string step in new[] { "Sorting", "Recycling", "PollutionTreatment" })
                {
                    int slot = Array.FindIndex(new[] { 0, 1, 2 }, i => Call(view, "GetOptionAt", i).ToString() == step);
                    Assert.GreaterOrEqual(slot, 0, "No displayed choice for " + step);
                    int before = ((System.Collections.ICollection)Get(console, "Selection")).Count;
                    yield return ClickVisibleButton(content, "Choice" + slot, 1f);
                    Assert.AreEqual(before + 1, ((System.Collections.ICollection)Get(console, "Selection")).Count,
                        "Pointer click did not select " + step);
                }
                Assert.That(content.Find("Confirm").position.y, Is.EqualTo(content.Find("Retry").position.y).Within(0.5f));
                Assert.That(content.Find("Retry").position.y, Is.EqualTo(content.Find("Close").position.y).Within(0.5f));
                yield return ClickVisibleButton(content, "Confirm", 0f);
                Assert.IsTrue((bool)Get(Find("C3TreasurePuzzle.C3PuzzleState"), "PuzzleSolved"),
                    "The semantic answer must solve the puzzle through the Confirm button.");
                Assert.IsTrue((bool)Get(console, "IsActivationPanel"), "Confirm must enter activation mode.");
                yield return ClickVisibleButton(content, "Close", 0f);
                Assert.IsFalse((bool)Get(console, "PanelOpen"), "Activation Close callback must close the panel.");
                Assert.IsTrue((bool)Call(console, "OpenPanel"), "Activation panel must reopen after Close.");
                yield return ClickVisibleButton(content, "Activate", 0f);
                Component device = Find("C3TreasurePuzzle.C3DeviceState");
                Assert.IsFalse((bool)Get(device, "LevelCompleted"));
                Call(device, "NotifyStartupFinished");
                Assert.IsTrue((bool)Call(Find("C3TreasurePuzzle.C3ExitTrigger"), "TryEnter", GameObject.Find("M2_Player").GetComponent<CharacterController>()));
                yield return null;
                Transform complete = GameObject.Find("C3UiCanvas").transform.Find("CompletionPanel/Viewport/Content");
                Assert.That(ui.transform.Find("CompletionBackdrop").GetComponent<Image>().color.a, Is.EqualTo(0.82f).Within(0.001f));
                float margin = Mathf.Min(20f, Mathf.Min(Screen.width, Screen.height) * 0.04f);
                Assert.That(((RectTransform)complete.parent.parent).rect.width, Is.EqualTo(Screen.width - 2 * margin).Within(0.1f));
                int previousHandle = SceneManager.GetActiveScene().handle;
                yield return ClickVisibleButton(complete, run == 0 ? "Restart" : "Selection", 0f);
                yield return WaitForScene(run == 0 ? C3 : Selection, previousHandle);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator PuzzleChoiceIsVisibleAndReceivesPointerRaycasts()
        {
            yield return SceneManager.LoadSceneAsync(C3, LoadSceneMode.Single);
            yield return null;
            Component progress = Find("C3TreasurePuzzle.C3TreasureProgress");
            foreach (string id in new[] { "fuse", "control_chip", "filter_module" })
                Assert.IsTrue((bool)Call(progress, "TryCollect", id, id));
            Assert.IsTrue((bool)Call(Find("C3TreasurePuzzle.C3PuzzleConsole"), "OpenPanel"));
            for (int frame = 0; frame < 5; frame++) yield return null;
            Canvas.ForceUpdateCanvases();
            GameObject canvas = GameObject.Find("C3UiCanvas");
            Assert.IsNotNull(canvas);
            Assert.IsTrue(canvas.GetComponent<Canvas>().pixelPerfect);
            Assert.That(canvas.GetComponent<CanvasScaler>().scaleFactor, Is.EqualTo(1f).Within(0.001f));
            foreach (Text label in canvas.GetComponentsInChildren<Text>(true))
            {
                Assert.IsNotNull(label.font, label.name);
                Assert.IsTrue(label.font.dynamic, label.name + " must use the imported dynamic CJK font.");
                Assert.IsNotNull(label.font.material, label.name + " must retain a live font material.");
                Assert.IsNotNull(label.font.material.mainTexture, label.name + " must retain a live glyph atlas.");
                Assert.AreNotEqual(FilterMode.Point, label.font.material.mainTexture.filterMode,
                    label.name + " should not render CJK glyphs with point sampling.");
                Assert.IsFalse(label.resizeTextForBestFit, label.name + " must not resample a smaller glyph.");
                Assert.That(label.transform.lossyScale.x, Is.EqualTo(1f).Within(0.001f), label.name);
            }
            Transform target = canvas.transform.Find("PuzzlePanel/Viewport/Content/Choice0");
            Assert.IsNotNull(target);
            Button button = target.GetComponent<Button>();
            Assert.IsNotNull(button);
            Assert.IsTrue(button.IsInteractable());
            Assert.Greater(((RectTransform)target).rect.width, 100f);
            Assert.IsNotNull(EventSystem.current);
            Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.GetComponent<Canvas>().renderMode);
            Assert.IsTrue(Cursor.visible, "The console must release the cursor for UI input.");
            Assert.IsNotNull(EventSystem.current.currentInputModule);
            Assert.AreEqual("InputSystemUIInputModule", EventSystem.current.currentInputModule.GetType().Name);
            Assert.IsTrue(EventSystem.current.currentInputModule.isActiveAndEnabled);
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(null,
                target.TransformPoint(((RectTransform)target).rect.center));
            var hits = new List<RaycastResult>();
            var pointer = new PointerEventData(EventSystem.current) { position = screenPoint };
            EventSystem.current.RaycastAll(pointer, hits);
            var direct = new List<RaycastResult>();
            canvas.GetComponent<GraphicRaycaster>().Raycast(pointer, direct);
            var viewport = (RectTransform)canvas.transform.Find("PuzzlePanel/Viewport");
            var corners = new Vector3[4]; viewport.GetWorldCorners(corners);
            Image targetImage = button.GetComponent<Image>();
            Assert.IsTrue(hits.Exists(hit => hit.gameObject == button.gameObject || hit.gameObject.transform.IsChildOf(button.transform)),
                "The visible puzzle choice must receive UI raycasts. position=" + screenPoint + " target=" + target.position +
                " screen=" + Screen.width + "x" + Screen.height + " viewport=" + corners[0] + "/" + corners[2] +
                " active=" + button.gameObject.activeInHierarchy + " cull=" + targetImage.canvasRenderer.cull +
                " raycast=" + targetImage.raycastTarget + " depth=" + targetImage.depth + " display=" + canvas.GetComponent<Canvas>().targetDisplay + " raycaster=" + canvas.GetComponent<GraphicRaycaster>().isActiveAndEnabled +
                " direct=" + string.Join(",", direct.ConvertAll(hit => hit.gameObject.name).ToArray()) +
                " hits=" + string.Join(",", hits.ConvertAll(hit => hit.gameObject.name).ToArray()));
            // Membership alone cannot detect an overlay above the choice. Exercise the top hit and callback too.
            Component console = Find("C3TreasurePuzzle.C3PuzzleConsole");
            yield return ClickVisibleButton(target.parent, "Choice0", 1f);
            Assert.AreEqual(1, ((System.Collections.ICollection)Get(console, "Selection")).Count,
                "Choice0 pointer click must select exactly one step.");
            object selected = Get(console, "Selection");
            Assert.AreEqual(Call(Find("C3TreasurePuzzle.C3UiView"), "GetOptionAt", 0),
                selected.GetType().GetProperty("Item").GetValue(selected, new object[] { 0 }),
                "The pointer callback must select the displayed step.");
        }

        [Test]
        public void GuiFontRecoversWhenCachedUnityFontIsLost()
        {
            Type fontType = RuntimeType("SDGAdventure.UI.CjkGuiFont");
            PropertyInfo asset = fontType.GetProperty("Asset");
            Assert.IsNotNull(asset.GetValue(null));
            fontType.GetField("font", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, null);
            Assert.IsNotNull(asset.GetValue(null), "A missing cached font must be reloaded after a prior successful load.");
        }

        [UnityTest]
        public IEnumerator UiAndFontSurviveThreeCompletionAndReloadCycles()
        {
            yield return SceneManager.LoadSceneAsync(Selection, LoadSceneMode.Single);
            object coordinator = Coordinator;
            for (int cycle = 0; cycle < 3; cycle++)
            {
                Launch(coordinator);
                yield return WaitForScene(C3);
                yield return null;
                Component progress = Find("C3TreasurePuzzle.C3TreasureProgress");
                Component puzzle = Find("C3TreasurePuzzle.C3PuzzleState");
                Component console = Find("C3TreasurePuzzle.C3PuzzleConsole");
                Component device = Find("C3TreasurePuzzle.C3DeviceState");
                Component screen = Find("C3TreasurePuzzle.C3CompletionScreen");
                Assert.AreEqual(1, UnityEngine.Object.FindObjectsOfType(RuntimeType("C3TreasurePuzzle.C3TreasureHud")).Length);
                Assert.AreEqual(1, UnityEngine.Object.FindObjectsOfType(RuntimeType("C3TreasurePuzzle.C3CompletionScreen")).Length);
                Assert.AreEqual(1f, Time.timeScale);
                Assert.IsFalse((bool)Get(screen, "Visible"));
                foreach (string id in new[] { "fuse", "control_chip", "filter_module" })
                    Assert.IsTrue((bool)Call(progress, "TryCollect", id, id));
                Assert.IsTrue((bool)Call(console, "OpenPanel"));
                Type fontType = RuntimeType("SDGAdventure.UI.CjkGuiFont");
                Font font = (Font)fontType.GetProperty("Asset").GetValue(null);
                Assert.IsNotNull(font);
                foreach (char ch in "廢棄物分類資源回收污染處理確認重試關閉")
                    Assert.IsTrue(font.HasCharacter(ch), "Missing glyph: " + ch);
                Type step = RuntimeType("C3TreasurePuzzle.C3ProcessingStep");
                foreach (string item in new[] { "Sorting", "Recycling", "PollutionTreatment" })
                    Assert.IsTrue((bool)Call(console, "SelectStep", Enum.Parse(step, item)));
                Assert.IsTrue((bool)Call(console, "Confirm"));
                Call(console, "ClosePanel");
                yield return null;
                Assert.IsTrue((bool)Call(console, "OpenPanel"));
                Assert.IsTrue((bool)Call(console, "ConfirmActivation"));
                Assert.IsFalse((bool)Get(screen, "Visible"));
                Call(device, "NotifyStartupFinished");
                Component exit = Find("C3TreasurePuzzle.C3ExitTrigger");
                Assert.IsTrue((bool)Call(exit, "TryEnter", GameObject.Find("M2_Player").GetComponent<CharacterController>()));
                Assert.IsTrue((bool)Get(screen, "Visible"));
                Call(screen, "ReturnToSelection");
                yield return WaitForScene(Selection);
                Assert.AreEqual(1f, Time.timeScale);
            }
        }

        [UnityTest]
        public IEnumerator FactoryStateExitAndEventsRequireTheRealPlayer()
        {
            yield return SceneManager.LoadSceneAsync(C3, LoadSceneMode.Single);
            yield return null;
            Component device = Find("C3TreasurePuzzle.C3DeviceState");
            Component progress = Find("C3TreasurePuzzle.C3TreasureProgress");
            Component puzzle = Find("C3TreasurePuzzle.C3PuzzleState");
            Component exit = Find("C3TreasurePuzzle.C3ExitTrigger");
            Component hud = Find("C3TreasurePuzzle.C3TreasureHud");
            Component screen = Find("C3TreasurePuzzle.C3CompletionScreen");
            CharacterController player = GameObject.Find("M2_Player").GetComponent<CharacterController>();
            Assert.AreEqual("Dormant", Get(device, "FactoryState").ToString());
            Assert.IsTrue(exit.GetComponent<Collider>().isTrigger);
            Assert.IsFalse((bool)Call(exit, "TryEnter", player));
            int activated = 0, running = 0, completed = 0, reported = 0;
            device.GetType().GetEvent("Activated").AddEventHandler(device, (Action)(() => activated++));
            device.GetType().GetEvent("Running").AddEventHandler(device, (Action)(() => running++));
            device.GetType().GetEvent("Completed").AddEventHandler(device, (Action)(() => completed++));
            object coordinator = Coordinator;
            EventInfo completedEvent = coordinator.GetType().GetEvent("Completed");
            Type resultType = RuntimeType("SDGAdventure.ModuleSystem.ModuleResult");
            // Count coordinator notifications without a compile-time reference to Assembly-CSharp.
            var counter = new NotificationCounter();
            Delegate handler = Delegate.CreateDelegate(completedEvent.EventHandlerType, counter,
                typeof(NotificationCounter).GetMethod("OnCompleted").MakeGenericMethod(resultType));
            completedEvent.AddEventHandler(coordinator, handler);
            foreach (string id in new[] { "fuse", "control_chip", "filter_module" })
                Assert.IsTrue((bool)Call(progress, "TryCollect", id, id));
            Assert.AreEqual(3, Get(progress, "CollectedCount"));
            Type step = RuntimeType("C3TreasurePuzzle.C3ProcessingStep");
            Array answer = Array.CreateInstance(step, 3);
            answer.SetValue(Enum.Parse(step, "Sorting"), 0);
            answer.SetValue(Enum.Parse(step, "Recycling"), 1);
            answer.SetValue(Enum.Parse(step, "PollutionTreatment"), 2);
            Assert.IsTrue((bool)Call(puzzle, "SubmitAnswer", answer));
            Assert.IsTrue((bool)Call(device, "TryActivate"));
            Assert.IsFalse((bool)Call(device, "TryActivate"));
            Assert.AreEqual(1, activated);
            Assert.AreEqual("Starting", Get(device, "FactoryState").ToString());
            StringAssert.Contains("重新啟動", (string)Get(hud, "GuidanceText"));
            Assert.IsFalse((bool)Get(screen, "Visible"));
            Assert.IsTrue(player.enabled);
            Assert.IsFalse((bool)Call(exit, "TryEnter", player));
            Assert.IsNull(Get(coordinator, "LastResult"));
            Call(device, "NotifyStartupFinished");
            Call(device, "NotifyStartupFinished");
            Assert.AreEqual(1, running);
            StringAssert.Contains("D 區出口", (string)Get(hud, "GuidanceText"));
            GameObject impostor = new GameObject("NotPlayer");
            Collider other = impostor.AddComponent<BoxCollider>();
            Assert.IsFalse((bool)Call(exit, "TryEnter", other));
            UnityEngine.Object.Destroy(impostor);
            Assert.IsTrue((bool)Call(exit, "TryEnter", player));
            Assert.IsFalse((bool)Call(exit, "TryEnter", player));
            Assert.AreEqual(1, completed);
            reported = counter.Count;
            Assert.AreEqual(1, reported);
            Assert.IsTrue((bool)Get(screen, "Visible"));
            Assert.AreEqual("Completed", Get(device, "FactoryState").ToString());
            completedEvent.RemoveEventHandler(coordinator, handler);
        }

        private sealed class NotificationCounter
        {
            public int Count;
            public void OnCompleted<T>(T result) => Count++;
        }

        [UnityTest]
        public IEnumerator PartsGlowOnlyNearPlayerAndRemainCollectible()
        {
            yield return SceneManager.LoadSceneAsync(C3, LoadSceneMode.Single);
            yield return null;
            GameObject player = GameObject.Find("M2_Player");
            CharacterController controller = player.GetComponent<CharacterController>();
            MonoBehaviour movement = (MonoBehaviour)player.GetComponent(RuntimeType("C3TreasurePuzzle.C3FirstPersonController"));
            movement.enabled = false;
            controller.enabled = false;
            Type partType = RuntimeType("C3TreasurePuzzle.C3CollectiblePart");
            var parts = UnityEngine.Object.FindObjectsOfType(partType);
            Assert.AreEqual(3, parts.Length);
            foreach (Component part in parts)
            {
                player.transform.position = part.transform.position + Vector3.right * 4f;
                yield return null;
                Assert.IsFalse((bool)Get(part, "IsGlowing"));
                player.transform.position = part.transform.position + Vector3.right * 1f;
                yield return null;
                Assert.IsTrue((bool)Get(part, "IsGlowing"));
                bool emits = false;
                foreach (Renderer renderer in part.GetComponentsInChildren<Renderer>())
                    if (renderer.enabled && renderer.sharedMaterial && renderer.sharedMaterial.IsKeywordEnabled("_EMISSION")) emits = true;
                Assert.IsTrue(emits, "A visible part material must have its emission variant enabled.");
                Assert.IsTrue((bool)Call(part, "CanInteract", player));
                Call(part, "Interact", player);
                Assert.IsFalse(part.gameObject.activeSelf);
            }
            Assert.AreEqual(3, Get(Find("C3TreasurePuzzle.C3TreasureProgress"), "CollectedCount"));
        }

        [UnityTest]
        public IEnumerator PartTargetRequiresTwoMetresAndUnobstructedSight()
        {
            yield return SceneManager.LoadSceneAsync(C3, LoadSceneMode.Single);
            yield return null;
            GameObject player = GameObject.Find("M2_Player");
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            ((MonoBehaviour)player.GetComponent(RuntimeType("C3TreasurePuzzle.C3FirstPersonController"))).enabled = false;
            Component interactor = player.GetComponent(RuntimeType("C3TreasurePuzzle.C3PlayerInteractor"));
            Camera camera = player.GetComponentInChildren<Camera>();
            Component part = null;
            foreach (Component candidate in UnityEngine.Object.FindObjectsOfType(RuntimeType("C3TreasurePuzzle.C3CollectiblePart")))
                if ((string)Get(candidate, "PartId") == "fuse") part = candidate;
            Assert.IsNotNull(part);
            part.transform.position = new Vector3(100f, 20f, 100f);
            camera.transform.position = new Vector3(100f, 20f, 97.5f);
            camera.transform.rotation = Quaternion.identity;
            Physics.SyncTransforms();
            Assert.IsNull(Call(interactor, "FindTarget"));
            camera.transform.position = new Vector3(100f, 20f, 98.5f);
            Physics.SyncTransforms();
            Assert.AreEqual(part, Call(interactor, "FindTarget"));
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(100f, 20f, 99.2f);
            wall.transform.localScale = new Vector3(1f, 1f, 0.1f);
            Physics.SyncTransforms();
            Assert.IsNull(Call(interactor, "FindTarget"));
            UnityEngine.Object.Destroy(wall);
        }

        [UnityTest]
        public IEnumerator FactoryStartupAndThreeWasteTypesUseBoundedVisualPool()
        {
            yield return SceneManager.LoadSceneAsync(C3,LoadSceneMode.Single);
            yield return null;
            Component device=Find("C3TreasurePuzzle.C3DeviceState");
            Component progress=Find("C3TreasurePuzzle.C3TreasureProgress");
            Component puzzle=Find("C3TreasurePuzzle.C3PuzzleState");
            Component presentation=Find("C3TreasurePuzzle.C3FactoryPresentation");
            Component line=Find("C3TreasurePuzzle.C3RecyclingLine");
            Assert.IsNotNull(line);
            Physics.SyncTransforms();
            C3V11Phase2RecyclingLineTests.AssertEquipmentConnections(Find("C3TreasurePuzzle.C3RecyclingLineGeometry"));
            Assert.AreEqual(0,(int)Get(line,"AcceptedUnits"));
            Type type=presentation.GetType();
            type.GetField("lightRestoreSeconds",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(presentation,.1f);
            type.GetField("motorStartSeconds",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(presentation,.1f);
            Light[] lights=(Light[])type.GetField("factoryLights",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(presentation);
            float initial=lights[0].intensity;
            Assert.IsFalse((bool)Get(presentation,"MotorActive"));
            foreach (string id in new[] {"fuse","control_chip","filter_module"}) Assert.IsTrue((bool)Call(progress,"TryCollect",id,id));
            Type step=RuntimeType("C3TreasurePuzzle.C3ProcessingStep");
            Array answer=Array.CreateInstance(step,3);
            string[] names={"Sorting","Recycling","PollutionTreatment"};
            for (int i=0;i<3;i++) answer.SetValue(Enum.Parse(step,names[i]),i);
            Assert.IsTrue((bool)Call(puzzle,"SubmitAnswer",answer)); Assert.IsTrue((bool)Call(device,"TryActivate"));
            Assert.AreEqual("Starting",Get(device,"FactoryState").ToString());
            float deadline=Time.realtimeSinceStartup+35f;
            while ((int)Get(presentation,"CompletedCycles")<3 && Time.realtimeSinceStartup<deadline) yield return null;
            Assert.IsTrue((bool)Get(presentation,"MotorActive")); Assert.Greater(lights[0].intensity,initial);
            Assert.AreEqual(9,(int)Get(line,"AcceptedUnits")); Assert.AreEqual(3,(int)Get(line,"ProcessedBatches"));
            object[] materials=C3V11Phase2RecyclingLineTests.Items(line,"Materials");
            object[] products=C3V11Phase2RecyclingLineTests.Items(line,"Products");
            Assert.AreEqual(3,products.Length);
            int paper=0,metal=0,plastic=0;
            foreach (object product in products)
            {
                string kind=C3V11Phase2RecyclingLineTests.Member(product,"MaterialType").ToString();
                if (kind=="Paper") paper++; else if (kind=="Metal") metal++; else if (kind=="Plastic") plastic++; else Assert.Fail(kind);
                Assert.IsTrue(((Transform)C3V11Phase2RecyclingLineTests.Member(product,"Product")).gameObject.activeInHierarchy);
            }
            Assert.AreEqual(1,paper); Assert.AreEqual(1,metal); Assert.AreEqual(1,plastic);
            yield return new WaitForSeconds(.3f);
            object[] same=C3V11Phase2RecyclingLineTests.Items(line,"Materials");
            Assert.AreEqual(materials.Length,same.Length);
            for (int i=0;i<materials.Length;i++) Assert.AreSame(materials[i],same[i]);
            Assert.AreEqual(3,(int)Get(line,"ProcessedBatches"),"Finite input must not produce endless timer credits");
        }

        private static string CollisionPath(Transform item)
        {
            string path = item.name;
            for (Transform parent = item.parent; parent != null; parent = parent.parent)
                path = parent.name + "/" + path;
            return path;
        }

        private static string CollisionState(Collider collider)
        {
            if (!collider) return "MISSING collider";
            return CollisionPath(collider.transform) + " id=" + collider.GetInstanceID() +
                " type=" + collider.GetType().Name + " enabled=" + collider.enabled +
                " active=" + collider.gameObject.activeInHierarchy + " trigger=" + collider.isTrigger +
                " layer=" + collider.gameObject.layer + " position=" + collider.transform.position.ToString("F6") +
                " scale=" + collider.transform.lossyScale.ToString("F6") +
                " bounds=" + collider.bounds.min.ToString("F6") + ".." + collider.bounds.max.ToString("F6");
        }

        private static void AssertSpawnCeilingCollision(GameObject player, CharacterController character)
        {
            Component factory = Find("C3TreasurePuzzle.C3ReferenceFactoryEnvironment");
            Component circulation = Find("C3TreasurePuzzle.C3V3Circulation");
            Assert.IsNotNull(factory, "Factory Awake must install the reference environment.");
            Assert.IsNotNull(circulation, "Factory Awake must install the current circulation.");
            Assert.IsNotNull(factory.transform.Find("C3_ReferenceRecyclingFactory_Visuals"));
            GameObject art = GameObject.Find("C_Factory_Art");
            Assert.IsNotNull(art);
            Transform lens = art.transform.Find("Lighting/Cyan_Console/IndicatorLens");
            Assert.IsNotNull(lens, "Missing authored legacy lamp.");
            Assert.AreSame(lens.parent.gameObject, GameObject.Find("Cyan_Console"), "Legacy lamp identity is ambiguous.");
            Collider retired = lens.GetComponent<Collider>();
            Transform deck = circulation.transform.Find("C3_V3_VerifiedCirculation/2F_CONNECTING_REAR_ROUTE");
            Assert.IsNotNull(deck, "Circulation Awake must build the current rear deck.");
            BoxCollider ceiling = deck.GetComponent<BoxCollider>();
            Assert.IsNotNull(retired);
            Assert.IsNotNull(ceiling);
            Physics.SyncTransforms();
            string setup = "M6 spawn collision (explicit upward Move, not the normal jump arc)\nlegacy=" + CollisionState(retired) +
                "\nceiling=" + CollisionState(ceiling) + "\nplayer=" + CollisionState(character) +
                " center=" + character.center.ToString("F6") + " height=" + character.height + " radius=" + character.radius +
                " skinWidth=" + character.skinWidth + " stepOffset=" + character.stepOffset +
                " ignoredLayers=" + Physics.GetIgnoreLayerCollision(player.layer, ceiling.gameObject.layer) +
                " ignoredPair=" + Physics.GetIgnoreCollision(character, ceiling);
            Debug.Log(setup);
            // V11 deliberately retires this invisible lamp's collision. Its bounds
            // cannot define the blocking height of the replacement circulation.
            Assert.IsFalse(retired.enabled, "The retired lamp must not become an invisible obstacle.\n" + setup);
            Assert.IsTrue(ceiling.enabled && ceiling.gameObject.activeInHierarchy && !ceiling.isTrigger, setup);
            Renderer deckRenderer = deck.GetComponent<Renderer>();
            Assert.IsNotNull(deckRenderer, setup);
            Assert.IsTrue(deckRenderer.enabled, "The blocking deck must remain visible.\n" + setup);
            Assert.IsTrue(character.enabled && character.detectCollisions, setup);
            Vector3 scale = character.transform.lossyScale;
            float scaleDeviation = (scale - Vector3.one).magnitude;

            Assert.Less(
                scaleDeviation,
                0.0001f,
                "The production capsule sweep assumes near-unit scale." +
                "\nActual scale=" + scale.ToString("F9") +
                "\nDeviation=" + scaleDeviation.ToString("G9") +
                "\n" + setup
            );
            Assert.IsFalse(Physics.GetIgnoreLayerCollision(player.layer, ceiling.gameObject.layer), setup);
            Assert.IsFalse(Physics.GetIgnoreCollision(character, ceiling), setup);

            MonoBehaviour movement = (MonoBehaviour)player.GetComponent(RuntimeType("C3TreasurePuzzle.C3FirstPersonController"));
            Assert.IsNotNull(movement, setup);
            bool movementEnabled = movement.enabled;
            Vector3 start = player.transform.position;
            bool cursorVisible = Cursor.visible;
            CursorLockMode cursorLock = Cursor.lockState;
            C3V11CollisionRecorder contacts = null;
            try
            {
                movement.enabled = false;
                Physics.SyncTransforms();
                Bounds before = character.bounds;
                float clearance = ceiling.bounds.min.y - before.max.y;
                Assert.Greater(clearance, 0.02f, "The spawn capsule must start below the live deck.\n" + setup);
                Assert.That(before.center.x, Is.InRange(ceiling.bounds.min.x, ceiling.bounds.max.x), setup);
                Assert.That(before.center.z, Is.InRange(ceiling.bounds.min.z, ceiling.bounds.max.z), setup);
                // Reach beyond the real underside even if gravity changed the spawn
                // height. The old fixed 2m move could end below this V11 deck.
                Vector3 requested = Vector3.up * (clearance + character.radius);
                Vector3 center = character.transform.TransformPoint(character.center);
                float halfSegment = Mathf.Max(0f, character.height * 0.5f - character.radius);
                Vector3 bottom = center - Vector3.up * halfSegment, top = center + Vector3.up * halfSegment;
                Collider[] overlaps = Physics.OverlapCapsule(bottom, top, character.radius, ~0, QueryTriggerInteraction.Ignore);
                RaycastHit[] sweep = Physics.CapsuleCastAll(bottom, top, character.radius, Vector3.up,
                    requested.y, ~0, QueryTriggerInteraction.Ignore);
                string approach = setup + "\nstart=" + start.ToString("F6") + " clearance=" + clearance +
                    " requested=" + requested.ToString("F6") + "\ninitial full-radius overlaps (including floor/skin contacts):";
                foreach (Collider hit in overlaps) approach += "\n  " + CollisionState(hit);
                foreach (RaycastHit hit in sweep)
                    approach += "\nsweep=" + CollisionPath(hit.collider.transform) + " distance=" + hit.distance +
                        " point=" + hit.point.ToString("F6") + " normal=" + hit.normal.ToString("F6");
                Assert.IsFalse(Array.Exists(overlaps, hit => hit == ceiling), approach);
                Assert.IsTrue(Array.Exists(sweep, hit => hit.collider == ceiling && hit.normal.y < -0.9f),
                    "The requested capsule path must intersect the live deck underside.\n" + approach);
                contacts = player.AddComponent<C3V11CollisionRecorder>();
                contacts.CaptureDetails = true;
                CollisionFlags flags = character.Move(requested);
                string result = approach + "\nactual=" + (player.transform.position - start).ToString("F6") +
                    " flags=" + flags + " final=" + CollisionState(character);
                foreach (C3V11CollisionRecorder.ContactEvidence hit in contacts.DetailedContacts)
                    result += "\ncontact=" + CollisionPath(hit.Collider.transform) + " point=" + hit.Point.ToString("F6") +
                        " normal=" + hit.Normal.ToString("F6") + " direction=" + hit.MoveDirection.ToString("F6") +
                        " length=" + hit.MoveLength;
                Assert.IsTrue((flags & CollisionFlags.Above) != 0, "Upward Move must report a ceiling collision.\n" + result);
                Assert.IsTrue(contacts.DetailedContacts.Exists(hit => hit.Collider == ceiling && hit.Normal.y < -0.9f),
                    "An unrelated blocker must not substitute for contact with the live deck.\n" + result);
                Assert.Greater(character.bounds.max.y, before.max.y + 0.02f, "The controller must actually move upward.\n" + result);
                Assert.LessOrEqual(character.bounds.max.y, ceiling.bounds.min.y + character.skinWidth,
                    "The capsule must not pass through the live deck beyond its production skin width.\n" + result);
                Debug.Log(result);
            }
            finally
            {
                if (contacts)
                {
                    contacts.enabled = false;
                    UnityEngine.Object.Destroy(contacts);
                }
                character.enabled = false;
                player.transform.position = start;
                character.enabled = true;
                Physics.SyncTransforms();
                movement.enabled = movementEnabled;
                Cursor.lockState = cursorLock;
                Cursor.visible = cursorVisible;
            }
        }

        [UnityTest]
        public IEnumerator RelocatedSpawnConsoleAndDoorTriggerAreReachable()
        {
            yield return SceneManager.LoadSceneAsync(C3, LoadSceneMode.Single);
            yield return null;
            GameObject player = GameObject.Find("M2_Player");
            CharacterController character = player.GetComponent<CharacterController>();
            Component device = Find("C3TreasurePuzzle.C3DeviceState");
            Component progress = Find("C3TreasurePuzzle.C3TreasureProgress");
            Component puzzle = Find("C3TreasurePuzzle.C3PuzzleState");
            Collider exit = Find("C3TreasurePuzzle.C3ExitTrigger").GetComponent<Collider>();
            GameObject console = GameObject.Find("M4_PuzzleConsole");
            Assert.That(player.transform.position.x, Is.EqualTo(-0.9f).Within(0.1f));
            Assert.That(player.transform.position.z, Is.EqualTo(18.95f).Within(0.1f));
            Assert.Greater(Vector3.Dot(player.transform.forward, Vector3.left), 0.9f);
            Vector3 capsuleBase = player.transform.position + Vector3.up * 0.36f;
            Vector3 capsuleTop = player.transform.position + Vector3.up * 1.44f;
            foreach (Collider hit in Physics.OverlapCapsule(capsuleBase, capsuleTop, 0.25f, ~0, QueryTriggerInteraction.Ignore))
                Assert.IsTrue(hit.transform.IsChildOf(player.transform), "Spawn overlaps " + hit.name);
            AssertSpawnCeilingCollision(player, character);
            character.enabled = false;
            player.transform.position = new Vector3(-0.9f, 0.58f, 18.95f);
            character.enabled = true;
            Assert.That(console.transform.position.x, Is.EqualTo(-20.9f).Within(0.01f));
            Assert.Greater(Vector3.Dot(console.transform.forward, Vector3.back), 0.99f);
            Collider consoleCollider = console.GetComponent<Collider>();
            Collider southWall = GameObject.Find("C_Wall_1_Left").GetComponent<Collider>();
            Assert.That(consoleCollider.bounds.min.y, Is.EqualTo(0.55f).Within(0.015f));
            Assert.That(
                console.transform.position.z,
                Is.EqualTo(4.15f).Within(0.02f),
                "Puzzle console must remain at its authored V11 position."
            );

            float southGap =
                consoleCollider.bounds.min.z -
                southWall.bounds.max.z;

            Assert.Greater(
                southGap,
                0.02f,
                "Puzzle console must not overlap the south wall."
            );
            Vector3 eye = new Vector3(-20.9f, 2.05f, 2.3f);
            Vector3 target = console.GetComponent<Collider>().bounds.center;
            Vector3 direction = target - eye;
            Assert.Less(direction.magnitude, 3f);
            Assert.IsTrue(Physics.Raycast(eye, direction.normalized, out RaycastHit rayHit, 3f, ~0, QueryTriggerInteraction.Ignore));
            Assert.AreEqual(console, rayHit.collider.gameObject, "Console interaction ray is blocked.");
            foreach (string id in new[] { "fuse", "control_chip", "filter_module" })
                Assert.IsTrue((bool)Call(progress, "TryCollect", id, id));
            Type step = RuntimeType("C3TreasurePuzzle.C3ProcessingStep");
            Array answer = Array.CreateInstance(step, 3);
            answer.SetValue(Enum.Parse(step, "Sorting"), 0);
            answer.SetValue(Enum.Parse(step, "Recycling"), 1);
            answer.SetValue(Enum.Parse(step, "PollutionTreatment"), 2);
            Assert.IsTrue((bool)Call(puzzle, "SubmitAnswer", answer));
            Assert.IsTrue((bool)Call(device, "TryActivate"));
            Call(device, "NotifyStartupFinished");
            MonoBehaviour movement = (MonoBehaviour)player.GetComponent(RuntimeType("C3TreasurePuzzle.C3FirstPersonController"));
            movement.enabled = false;
            character.enabled = false;
            player.transform.position = new Vector3(-1.5f, 0.56f, 12.5f);
            character.enabled = true;
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            character.Move(Vector3.right * 2f);
            yield return new WaitForFixedUpdate();
            Assert.Greater(player.transform.position.x, -1f, "Player must reach the B doorway before checking it.");
            Assert.IsFalse((bool)Get(device, "LevelCompleted"), "B door must not complete the level.");
            character.enabled = false;
            player.transform.position = new Vector3(-12.5f, 0.56f, 4f);
            character.enabled = true;
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            for (int i = 0; i < 20 && !(bool)Get(device, "LevelCompleted"); i++)
            {
                character.Move(Vector3.back * 0.2f);
                yield return new WaitForFixedUpdate();
            }
            Assert.IsTrue((bool)Get(device, "LevelCompleted"), "D doorway must be reachable and trigger completion.");
            Assert.That(exit.bounds.center.z, Is.EqualTo(1f).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator TemporaryDelayAndPhysicalTriggerCompleteAfterRunning()
        {
            yield return SceneManager.LoadSceneAsync(C3, LoadSceneMode.Single);
            yield return null;
            Component device = Find("C3TreasurePuzzle.C3DeviceState");
            Component progress = Find("C3TreasurePuzzle.C3TreasureProgress");
            Component puzzle = Find("C3TreasurePuzzle.C3PuzzleState");
            Component exit = Find("C3TreasurePuzzle.C3ExitTrigger");
            foreach (string id in new[] { "fuse", "control_chip", "filter_module" })
                Assert.IsTrue((bool)Call(progress, "TryCollect", id, id));
            Type step = RuntimeType("C3TreasurePuzzle.C3ProcessingStep");
            Array answer = Array.CreateInstance(step, 3);
            answer.SetValue(Enum.Parse(step, "Sorting"), 0);
            answer.SetValue(Enum.Parse(step, "Recycling"), 1);
            answer.SetValue(Enum.Parse(step, "PollutionTreatment"), 2);
            Assert.IsTrue((bool)Call(puzzle, "SubmitAnswer", answer));
            device.GetType().GetField("testStartupDelaySeconds", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(device, 0.05f);
            device.GetType().GetField("useTestStartupDelay", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(device, true);
            Assert.IsTrue((bool)Call(device, "TryActivate"));
            yield return new WaitForSeconds(0.15f);
            Assert.AreEqual("Running", Get(device, "FactoryState").ToString());
            GameObject player = GameObject.Find("M2_Player");
            CharacterController character = player.GetComponent<CharacterController>();
            MonoBehaviour movement = (MonoBehaviour)player.GetComponent(RuntimeType("C3TreasurePuzzle.C3FirstPersonController"));
            movement.enabled = false;
            character.enabled = false;
            Bounds bounds = exit.GetComponent<Collider>().bounds;
            player.transform.position = bounds.center + Vector3.right * (bounds.extents.x + 3f);
            character.enabled = true;
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            character.enabled = false;
            player.transform.position = bounds.center;
            character.enabled = true;
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.AreEqual("Completed", Get(device, "FactoryState").ToString());
        }

        [UnityTest]
        public IEnumerator C3CompletionRestartReturnAndReentry()
        {
            yield return SceneManager.LoadSceneAsync(Selection, LoadSceneMode.Single);
            object coordinator = Coordinator;
            Assert.IsNotNull(coordinator);
            Launch(coordinator);
            yield return WaitForScene(C3);

            Component progress = Find("C3TreasurePuzzle.C3TreasureProgress");
            Component puzzle = Find("C3TreasurePuzzle.C3PuzzleState");
            Component device = Find("C3TreasurePuzzle.C3DeviceState");
            Assert.IsNotNull(progress);
            Assert.IsNotNull(puzzle);
            Assert.IsNotNull(device);
            Assert.IsFalse((bool)Call(device, "TryActivate"), "Unfinished level must not activate.");
            Assert.IsNull(Get(coordinator, "LastResult"));

            Assert.IsTrue((bool)Call(progress, "TryCollect", "fuse", "Fuse"));
            Assert.IsTrue((bool)Call(progress, "TryCollect", "control_chip", "Chip"));
            Assert.IsTrue((bool)Call(progress, "TryCollect", "filter_module", "Filter"));
            Assert.IsFalse((bool)Call(device, "TryActivate"), "Collection alone must not complete.");
            Type stepType = RuntimeType("C3TreasurePuzzle.C3ProcessingStep");
            Array answer = Array.CreateInstance(stepType, 3);
            answer.SetValue(Enum.Parse(stepType, "Sorting"), 0);
            answer.SetValue(Enum.Parse(stepType, "Recycling"), 1);
            answer.SetValue(Enum.Parse(stepType, "PollutionTreatment"), 2);
            Assert.IsTrue((bool)Call(puzzle, "SubmitAnswer", answer));
            Assert.IsNull(Get(coordinator, "LastResult"), "Solving without activation must not complete.");
            Assert.IsTrue((bool)Call(device, "TryActivate"));
            Assert.IsFalse((bool)Get(device, "LevelCompleted"));
            Assert.IsNull(Get(coordinator, "LastResult"));
            Component exit = Find("C3TreasurePuzzle.C3ExitTrigger");
            Collider player = GameObject.Find("M2_Player").GetComponent<CharacterController>();
            Assert.IsFalse((bool)Call(exit, "TryEnter", player));
            Call(device, "NotifyStartupFinished");
            Assert.IsTrue((bool)Call(exit, "TryEnter", player));
            Assert.IsFalse((bool)Call(exit, "TryEnter", player));
            Assert.IsTrue((bool)Get(device, "LevelCompleted"));
            object result = Get(coordinator, "LastResult");
            Assert.IsNotNull(result);
            Assert.IsTrue((bool)Get(result, "Completed"));
            Assert.AreEqual("c3_treasure_puzzle", Get(result, "ModuleId"));
            Assert.AreEqual(3, Get(result, "CollectedParts"));
            Assert.IsFalse((bool)Call(device, "TryActivate"), "Activation must be one-shot.");
            Component adapter = Find("SDGAdventure.C3.C3ModuleAdapter");
            Assert.IsNotNull(adapter);
            Assert.IsFalse((bool)Call(coordinator, "ReportCompletion", adapter, result), "Coordinator must reject repeat results.");
            Component completionScreen = Find("C3TreasurePuzzle.C3CompletionScreen");
            Assert.IsTrue((bool)Get(completionScreen, "Visible"));
            Call(completionScreen, "RestartLevel");

            yield return WaitForScene(C3);
            yield return null;
            Assert.AreEqual(0, Get(Find("C3TreasurePuzzle.C3TreasureProgress"), "CollectedCount"));
            Assert.IsNull(Get(coordinator, "LastResult"));

            progress = Find("C3TreasurePuzzle.C3TreasureProgress");
            puzzle = Find("C3TreasurePuzzle.C3PuzzleState");
            device = Find("C3TreasurePuzzle.C3DeviceState");
            Assert.IsTrue((bool)Call(progress, "TryCollect", "fuse", "Fuse"));
            Assert.IsTrue((bool)Call(progress, "TryCollect", "control_chip", "Chip"));
            Assert.IsTrue((bool)Call(progress, "TryCollect", "filter_module", "Filter"));
            Assert.IsTrue((bool)Call(puzzle, "SubmitAnswer", answer));
            Assert.IsTrue((bool)Call(device, "TryActivate"));
            Call(device, "NotifyStartupFinished");
            exit = Find("C3TreasurePuzzle.C3ExitTrigger");
            player = GameObject.Find("M2_Player").GetComponent<CharacterController>();
            Assert.IsTrue((bool)Call(exit, "TryEnter", player));
            completionScreen = Find("C3TreasurePuzzle.C3CompletionScreen");
            Assert.IsTrue((bool)Get(completionScreen, "Visible"));
            Call(completionScreen, "ReturnToSelection");
            yield return WaitForScene(Selection);
            Assert.IsNull(Get(coordinator, "CurrentModuleId"));
            Assert.AreEqual(1f, Time.timeScale);
            Assert.IsNotNull(Get(coordinator, "LastResult"));
            Launch(coordinator);
            yield return WaitForScene(C3);
            yield return null;
            Assert.AreEqual(0, Get(Find("C3TreasurePuzzle.C3TreasureProgress"), "CollectedCount"));
            Assert.IsNull(Get(coordinator, "LastResult"));
        }
    }
}
