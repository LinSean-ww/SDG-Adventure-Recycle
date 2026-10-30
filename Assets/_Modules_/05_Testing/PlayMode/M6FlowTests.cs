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

        private static IEnumerator WaitForScene(string path)
        {
            float until = Time.realtimeSinceStartup + 15f;
            while (SceneManager.GetActiveScene().path != path && Time.realtimeSinceStartup < until)
                yield return null;
            Assert.AreEqual(path, SceneManager.GetActiveScene().path);
        }

        private static void Launch(object coordinator)
        {
            object[] args = { "c3_treasure_puzzle", null };
            Assert.IsTrue((bool)Call(coordinator, "TryLaunch", args), args[1]?.ToString());
        }

        private static IEnumerator ClickVisibleButton(Transform content, string name, float scrollPosition)
        {
            ScrollRect scroll = content.parent.GetComponent<ScrollRect>();
            scroll.verticalNormalizedPosition = scrollPosition;
            for (int frame = 0; frame < 5; frame++) yield return null;
            Canvas.ForceUpdateCanvases();
            Button button = content.Find(name).GetComponent<Button>();
            Assert.IsTrue(button.IsInteractable(), name);
            var pointer = new PointerEventData(EventSystem.current) {
                position = RectTransformUtility.WorldToScreenPoint(null, button.transform.position),
                button = PointerEventData.InputButton.Left
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.IsNotEmpty(hits, name + " received no raycast");
            Assert.AreEqual(button.gameObject, hits[0].gameObject, name + " was blocked by " + hits[0].gameObject.name);
            ExecuteEvents.Execute(hits[0].gameObject, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(hits[0].gameObject, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler);
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
                Assert.IsTrue((bool)Call(console, "OpenPanel"));
                yield return ClickVisibleButton(content, "Choice0", 1f);
                yield return ClickVisibleButton(content, "Choice1", 1f);
                yield return ClickVisibleButton(content, "Choice2", 1f);
                yield return ClickVisibleButton(content, "Confirm", 0f);
                Assert.That(content.Find("Confirm").position.y, Is.EqualTo(content.Find("Retry").position.y).Within(0.5f));
                Assert.That(content.Find("Retry").position.y, Is.EqualTo(content.Find("Close").position.y).Within(0.5f));
                Assert.IsTrue((bool)Get(Find("C3TreasurePuzzle.C3PuzzleState"), "PuzzleSolved"));
                yield return ClickVisibleButton(content, "Close", 0f);
                Assert.IsTrue((bool)Call(console, "OpenPanel"));
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
                yield return ClickVisibleButton(complete, run == 0 ? "Restart" : "Selection", 0f);
                yield return WaitForScene(run == 0 ? C3 : Selection);
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
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(null, target.position);
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = screenPoint }, hits);
            var viewport = (RectTransform)canvas.transform.Find("PuzzlePanel/Viewport");
            var corners = new Vector3[4]; viewport.GetWorldCorners(corners);
            Image targetImage = button.GetComponent<Image>();
            Assert.IsTrue(hits.Exists(hit => hit.gameObject == button.gameObject || hit.gameObject.transform.IsChildOf(button.transform)),
                "The visible puzzle choice must receive UI raycasts. position=" + screenPoint + " target=" + target.position +
                " screen=" + Screen.width + "x" + Screen.height + " viewport=" + corners[0] + "/" + corners[2] +
                " active=" + button.gameObject.activeInHierarchy + " cull=" + targetImage.canvasRenderer.cull +
                " raycast=" + targetImage.raycastTarget + " depth=" + targetImage.depth + " display=" + canvas.GetComponent<Canvas>().targetDisplay + " raycaster=" + canvas.GetComponent<GraphicRaycaster>().isActiveAndEnabled +
                " hits=" + string.Join(",", hits.ConvertAll(hit => hit.gameObject.name).ToArray()));
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
            yield return SceneManager.LoadSceneAsync(C3, LoadSceneMode.Single);
            yield return null;
            Component device = Find("C3TreasurePuzzle.C3DeviceState");
            Component progress = Find("C3TreasurePuzzle.C3TreasureProgress");
            Component puzzle = Find("C3TreasurePuzzle.C3PuzzleState");
            Component presentation = Find("C3TreasurePuzzle.C3FactoryPresentation");
            Assert.IsNotNull(presentation);
            Renderer steelSource = GameObject.Find("DarkServicePillar")?.GetComponent<Renderer>();
            Renderer feedRail = GameObject.Find("B區輸送帶支架")?.GetComponent<Renderer>();
            Assert.IsNotNull(steelSource, "The C-area dark steel source must exist.");
            Assert.IsNotNull(feedRail, "The feed rail must use a visible renderer.");
            Assert.AreSame(steelSource.sharedMaterial, feedRail.sharedMaterial,
                "The feed rail should reuse existing industrial steel without editing a shared material.");
            Type type = presentation.GetType();
            type.GetField("lightRestoreSeconds", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(presentation, 0.1f);
            type.GetField("motorStartSeconds", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(presentation, 0.1f);
            type.GetField("cycleSeconds", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(presentation, 0.15f);
            GameObject[] waste = (GameObject[])type.GetField("waste", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(presentation);
            Transform[] marks = (Transform[])type.GetField("feedMarks", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(presentation);
            Vector3 markBefore = marks[0].localPosition;
            Light[] lights = (Light[])type.GetField("factoryLights", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(presentation);
            float initialLight = lights[0].intensity;
            Assert.AreEqual(3, waste.Length);
            Assert.IsFalse((bool)Get(presentation, "MotorActive"));
            foreach (GameObject item in waste) Assert.IsFalse(item.activeSelf);
            foreach (string id in new[] { "fuse", "control_chip", "filter_module" })
                Assert.IsTrue((bool)Call(progress, "TryCollect", id, id));
            Type step = RuntimeType("C3TreasurePuzzle.C3ProcessingStep");
            Array answer = Array.CreateInstance(step, 3);
            answer.SetValue(Enum.Parse(step, "Sorting"), 0);
            answer.SetValue(Enum.Parse(step, "Recycling"), 1);
            answer.SetValue(Enum.Parse(step, "PollutionTreatment"), 2);
            Assert.IsTrue((bool)Call(puzzle, "SubmitAnswer", answer));
            Assert.IsTrue((bool)Call(device, "TryActivate"));
            Assert.AreEqual("Starting", Get(device, "FactoryState").ToString());
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual("Running", Get(device, "FactoryState").ToString());
            Assert.IsTrue((bool)Get(presentation, "MotorActive"));
            Assert.Greater(lights[0].intensity, initialLight);
            yield return new WaitForSeconds(0.1f);
            Assert.Greater(Vector3.Distance(markBefore, marks[0].localPosition), 0.01f);
            yield return new WaitForSeconds(0.65f);
            Assert.GreaterOrEqual((int)Get(presentation, "CompletedCycles"), 3);
            GameObject[,] markers = (GameObject[,])type.GetField("sortedMarkers", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(presentation);
            for (int i = 0; i < 3; i++) Assert.IsTrue(markers[i, 0].activeSelf, "Each sorted category must retain a visible result.");
            GameObject[] same = (GameObject[])type.GetField("waste", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(presentation);
            for (int i = 0; i < 3; i++) Assert.AreSame(waste[i], same[i]);
            type.GetField("cycleSeconds", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(presentation, 10f);
            for (int i = 0; i < 3; i++)
            {
                type.GetField("cycleIndex", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(presentation, i);
                type.GetField("cycleTime", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(presentation, 9.8f);
                yield return null;
                Assert.IsTrue(waste[i].activeSelf);
                Assert.That(waste[i].transform.localPosition.x, Is.EqualTo(-0.85f + i * 0.85f).Within(0.2f));
                Assert.Greater(waste[i].transform.localPosition.z, 0.8f);
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
            Collider blue = GameObject.Find("Cyan_Console").transform.Find("IndicatorLens").GetComponent<Collider>();
            Assert.IsNotNull(blue);
            Assert.IsFalse(blue.isTrigger);
            Assert.Greater(blue.bounds.min.y - character.bounds.max.y, 0.02f);
            MonoBehaviour spawnMovement = (MonoBehaviour)player.GetComponent(RuntimeType("C3TreasurePuzzle.C3FirstPersonController"));
            spawnMovement.enabled = false;
            character.Move(Vector3.up * 2f);
            Assert.LessOrEqual(character.bounds.max.y, blue.bounds.min.y + 0.1f, "Jump must not pass through the wall fixture.");
            character.enabled = false;
            player.transform.position = new Vector3(-0.9f, 0.58f, 18.95f);
            character.enabled = true;
            Assert.That(console.transform.position.x, Is.EqualTo(-20.9f).Within(0.01f));
            Assert.Greater(Vector3.Dot(console.transform.forward, Vector3.back), 0.99f);
            Collider consoleCollider = console.GetComponent<Collider>();
            Collider southWall = GameObject.Find("C_Wall_1_Left").GetComponent<Collider>();
            Assert.That(consoleCollider.bounds.min.y, Is.EqualTo(0.55f).Within(0.015f));
            Assert.That(consoleCollider.bounds.min.z, Is.EqualTo(southWall.bounds.max.z).Within(0.02f));
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
