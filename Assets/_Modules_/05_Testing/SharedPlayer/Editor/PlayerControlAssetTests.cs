using System.Collections;
using System.Linq;
using NUnit.Framework;
using SDGAdventure.PlayerTest;
using SDGAdventure.SharedPlayer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SDGAdventure.PlayerControlTests
{
    public sealed class PlayerControlAssetTests
    {
        private const string PrefabPath = "Assets/_Modules_/01_Shared/Player/Prefabs/SharedPlayer.prefab";
        private const string ScenePath = "Assets/_Modules_/05_Testing/SharedPlayer/PlayerControlTest.unity";

        [Test]
        public void SharedCanvasFontLoadsWithoutChangingGuiSkin()
        {
            GUISkin source = EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector);
            Font original = source.font;
            Font packaged = Resources.Load<Font>("SharedPlayer/Fonts/NotoSansCJKtc-Regular");
            Assert.IsNotNull(packaged, "The shared player package must contain its runtime CJK font.");
            Assert.AreSame(packaged, PlayerGuiFont.Load());
            Assert.IsNotNull(Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/_Modules_/06_ExternalAssets/ThirdPartyNotices/SharedPlayer/NotoSansCJK-OFL.txt"));
            Assert.AreSame(original, source.font);
        }

        [Test]
        public void TaskListActionIsBoundToPInSharedInputAsset()
        {
            var actions = Resources.Load<UnityEngine.InputSystem.InputActionAsset>("SDGPlayerControls");
            Assert.IsNotNull(actions);
            Assert.AreEqual("<Keyboard>/p", actions.FindActionMap("Gameplay")
                .FindAction("TaskList").bindings[0].effectivePath);
            Assert.AreEqual("<Keyboard>/p", actions.FindActionMap("UI")
                .FindAction("TaskList").bindings[0].effectivePath);
        }

        [Test]
        public void SharedPlayerPrefabHasOneMotorCameraAndMenu()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab);
            Assert.AreEqual(1, prefab.GetComponentsInChildren<CharacterController>(true).Length);
            Assert.AreEqual(1, prefab.GetComponentsInChildren<Camera>(true).Length);
            Assert.AreEqual(1, prefab.GetComponentsInChildren<AudioListener>(true).Length);
            Assert.AreEqual(1, prefab.GetComponentsInChildren<SharedFirstPersonController>(true).Length);
            Assert.AreEqual(1, prefab.GetComponentsInChildren<PlayerInteractor>(true).Length);
            Assert.AreEqual(1, prefab.GetComponentsInChildren<PlayerPauseMenu>(true).Length);
            Assert.IsNotNull(new SerializedObject(prefab.GetComponent<SharedFirstPersonController>())
                .FindProperty("viewTransform").objectReferenceValue);
            Assert.IsNotNull(new SerializedObject(prefab.GetComponent<PlayerInteractor>())
                .FindProperty("playerCamera").objectReferenceValue);
        }

        [Test]
        public void StandaloneSceneReferencesPrefabAndTestObjects()
        {
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath));
            Assert.Contains(PrefabPath, AssetDatabase.GetDependencies(ScenePath, true));
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                GameObject[] roots = scene.GetRootGameObjects();
                SharedFirstPersonController[] players = roots.SelectMany(root =>
                    root.GetComponentsInChildren<SharedFirstPersonController>(true)).ToArray();
                Assert.AreEqual(1, players.Length);
                Assert.AreEqual(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath),
                    PrefabUtility.GetCorrespondingObjectFromSource(players[0].gameObject));
                Assert.AreEqual(1, roots.SelectMany(root => root.GetComponentsInChildren<Camera>(true)).Count());
                Assert.AreEqual(1, roots.SelectMany(root => root.GetComponentsInChildren<AudioListener>(true)).Count());
                Assert.AreEqual(1, roots.SelectMany(root =>
                    root.GetComponentsInChildren<PlayerControlTestInteractable>(true)).Count());
                Assert.AreEqual(1, roots.SelectMany(root =>
                    root.GetComponentsInChildren<PlayerControlTestHud>(true)).Count());
                Assert.AreEqual(1, roots.SelectMany(root =>
                    root.GetComponentsInChildren<PlayerControlTestTaskDemo>(true)).Count());
                Assert.IsNotNull(new SerializedObject(players[0].GetComponent<PlayerPauseMenu>())
                    .FindProperty("testReturnPoint").objectReferenceValue);
                Assert.IsTrue(roots.Any(root => root.name == "LowTunnelRoof"));
                Assert.IsTrue(roots.Any(root => root.name == "JumpPlatform"));
                Assert.IsFalse(roots.SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true))
                    .Any(component => component == null), "Scene has a missing script.");
            }
            finally
            {
                if (SceneManager.sceneCount > 1) EditorSceneManager.CloseScene(scene, true);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        [UnityTest]
        public IEnumerator StandaloneSceneEntersPlayModeWithoutBuildSettings()
        {
            Assume.That(Application.isBatchMode, "This scene switching check runs in a separate batch Editor.");
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            yield return new EnterPlayMode();
            yield return null;
            Assert.AreEqual(ScenePath, SceneManager.GetActiveScene().path);
            Assert.AreEqual(1, Object.FindObjectsByType<PlayerInputService>(FindObjectsSortMode.None).Length);
            Assert.AreEqual(1, Object.FindObjectsByType<SharedFirstPersonController>(FindObjectsSortMode.None).Length);
            Assert.AreEqual(1, Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length);
            Assert.AreEqual(1, Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length);
            Assert.AreEqual(1, Object.FindObjectsByType<PlayerControlTestInteractable>(FindObjectsSortMode.None).Length);
            Assert.AreEqual(1, Object.FindObjectsByType<PlayerControlTestTaskDemo>(FindObjectsSortMode.None).Length);
            Assert.AreEqual(3, Object.FindFirstObjectByType<PlayerTaskJournal>().TotalCount);
            Assert.AreEqual(PlayerInputContext.Gameplay, PlayerInputService.Instance.Context);
            yield return new ExitPlayMode();
        }

    }
}
