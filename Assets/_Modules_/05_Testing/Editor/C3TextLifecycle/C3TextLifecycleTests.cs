using System;
using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public sealed class C3TextLifecycleTests
{
    private const string Scene = "Assets/_Modules_/02_Levels/Level03/Scenes/C_Main.unity";
    private static Type RuntimeType(string name) => Type.GetType(name + ", Assembly-CSharp", true);
    private static Component Find(string name) => (Component)UnityEngine.Object.FindObjectOfType(RuntimeType(name));
    private static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method).Invoke(target, args);
    private static void Click(Button button)
    {
        Assert.IsTrue(button.gameObject.activeInHierarchy);
        Assert.IsTrue(button.IsInteractable());
        Assert.IsNotNull(EventSystem.current);
        ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left },
            ExecuteEvents.pointerClickHandler);
    }

    [UnityTest] public IEnumerator PlayModeCycleOne() => Exercise();
    [UnityTest] public IEnumerator PlayModeCycleTwo() => Exercise();
    [UnityTest] public IEnumerator PlayModeCycleThree() => Exercise();

    private static IEnumerator Exercise()
    {
        EditorSceneManager.OpenScene(Scene);
        yield return new EnterPlayMode();
        yield return null;
        Component progress = Find("C3TreasurePuzzle.C3TreasureProgress");
        Component console = Find("C3TreasurePuzzle.C3PuzzleConsole");
        foreach (string id in new[] { "fuse", "control_chip", "filter_module" })
            Assert.IsTrue((bool)Call(progress, "TryCollect", id, id));
        Assert.IsTrue((bool)Call(console, "OpenPanel"));
        yield return null;
        GameObject canvas = GameObject.Find("C3UiCanvas");
        Assert.IsNotNull(canvas);
        Transform content = canvas.transform.Find("PuzzlePanel/Viewport/Content");
        Assert.IsNotNull(content);
        string[] expectedSteps = { "廢棄物分類", "資源回收", "污染處理" };
        Button[] optionButtons = new Button[3];
        string[] optionLabels = new string[3];
        for (int i = 0; i < optionButtons.Length; i++)
        {
            optionButtons[i] = content.Find("Choice" + i).GetComponent<Button>();
            Text label = optionButtons[i].transform.Find("Label").GetComponent<Text>();
            Assert.IsNotNull(label.font);
            optionLabels[i] = label.text;
        }
        CollectionAssert.AreEquivalent(expectedSteps, optionLabels,
            "The three processing choices must each appear exactly once, regardless of shuffle order.");
        for (int frame = 0; frame < 10; frame++)
        {
            EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Repaint();
            yield return null;
        }
        yield return Resources.UnloadUnusedAssets();
        for (int frame = 0; frame < 10; frame++)
        {
            EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Repaint();
            yield return null;
        }
        foreach (string expectedStep in expectedSteps)
        {
            int slot = Array.IndexOf(optionLabels, expectedStep);
            Assert.GreaterOrEqual(slot, 0);
            Click(optionButtons[slot]);
        }
        Click(canvas.transform.Find("PuzzlePanel/Viewport/Content/Confirm").GetComponent<Button>());
        Call(console, "ClosePanel");
        yield return null;
        Assert.IsTrue((bool)Call(console, "OpenPanel"));
        yield return null;
        Click(canvas.transform.Find("PuzzlePanel/Viewport/Content/Activate").GetComponent<Button>());
        Component device = Find("C3TreasurePuzzle.C3DeviceState");
        Assert.IsFalse((bool)device.GetType().GetProperty("LevelCompleted").GetValue(device));
        Call(device, "NotifyStartupFinished");
        Component exit = Find("C3TreasurePuzzle.C3ExitTrigger");
        Assert.IsTrue((bool)Call(exit, "TryEnter", GameObject.Find("M2_Player").GetComponent<CharacterController>()));
        yield return null;
        Component completion = Find("C3TreasurePuzzle.C3CompletionScreen");
        Assert.IsTrue((bool)completion.GetType().GetProperty("Visible").GetValue(completion));
        Call(completion, "RestartLevel");
        yield return null;
        yield return null;
        Assert.IsNotNull(GameObject.Find("C3UiCanvas"));
        Assert.IsFalse((bool)Find("C3TreasurePuzzle.C3CompletionScreen").GetType().GetProperty("Visible")
            .GetValue(Find("C3TreasurePuzzle.C3CompletionScreen")));
        Type coordinatorType = RuntimeType("SDGAdventure.ModuleSystem.ModuleCoordinator");
        object coordinator = coordinatorType.GetProperty("Instance").GetValue(null);
        object[] returnArgs = { null };
        Assert.IsTrue((bool)Call(coordinator, "ReturnToSelection", returnArgs));
        yield return null;
        GameObject selection = GameObject.Find("M6SelectionCanvas");
        Assert.IsNotNull(selection);
        yield return Resources.UnloadUnusedAssets();
        EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Repaint();
        yield return null;
        Click(selection.transform.Find("Panel/Launch").GetComponent<Button>());
        yield return null;
        yield return null;
        Assert.IsNotNull(GameObject.Find("C3UiCanvas"));
        yield return new ExitPlayMode();
    }
}
