using SDGAdventure.SharedPlayer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SDGAdventure.PlayerControlTests.Editor
{
    // Creates an unsaved, ordinary scene for manual portability checks.
    public static class CompletePlayerIndependentScene
    {
        [MenuItem("SDG Adventure/Complete Player/Open Independent Test Scene")]
        public static void Open()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.position = new Vector3(0f, -0.25f, 0f);
            floor.transform.localScale = new Vector3(20f, 0.5f, 20f);
            var light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Modules_/01_Shared/CompletePlayer/Prefabs/CompletePlayer.prefab");
            if (!prefab) throw new System.InvalidOperationException("CompletePlayer prefab is missing.");
            var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            player.transform.position = new Vector3(0f, 0.1f, 0f);
            Selection.activeGameObject = player;
        }
    }
}
