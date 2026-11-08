using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class Level01ExitSetup
{
    public static void Install()
    {
        var scene = EditorSceneManager.OpenScene(Level01ModuleAdapter.ScenePath, OpenSceneMode.Single);
        if (scene.GetRootGameObjects().Any(x => x.name == "Level01 Exit Trigger"))
            throw new InvalidOperationException("Level01 exit already exists; refusing to overwrite it.");
        var controller = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<DoorInteraction>(true)).Single();
        if (Vector3.Distance(controller.doorObject.position, new Vector3(12.478f, 1.992f, 0.3f)) > 0.05f)
            throw new InvalidOperationException("Door position changed; inspect geometry before installing exit.");
        var exit = new GameObject("Level01 Exit Trigger");
        exit.transform.position = new Vector3(12.478f, 1.45f, 1.5f);
        var collider = exit.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.size = new Vector3(4.6f, 2.8f, 0.7f);
        exit.AddComponent<Level01ExitTrigger>();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Failed to save Level01 scene.");
        Debug.Log("LEVEL01_EXIT_INSTALLED position=(12.478,1.450,1.500) size=(4.600,2.800,0.700)");
    }

    public static void Inspect()
    {
        var scene = EditorSceneManager.OpenScene(Level01ModuleAdapter.ScenePath, OpenSceneMode.Single);
        var roots = scene.GetRootGameObjects();
        var controller = roots.SelectMany(x => x.GetComponentsInChildren<DoorInteraction>(true)).Single();
        var door = controller.doorObject;
        Debug.Log($"L01_GEOMETRY door={door.name} position={door.position.ToString("F3")} rotation={door.eulerAngles.ToString("F2")} scale={door.lossyScale.ToString("F3")} forward={door.forward.ToString("F3")}");
        Debug.Log($"L01_GEOMETRY controller={controller.transform.position.ToString("F3")} player={roots.Single(x => x.name == "Level01 CompletePlayer").transform.position.ToString("F3")}");
        foreach (var c in door.GetComponentsInChildren<Collider>(true))
            Debug.Log($"L01_DOOR_COLLIDER name={c.name} enabled={c.enabled} trigger={c.isTrigger} bounds={c.bounds.center.ToString("F3")} size={c.bounds.size.ToString("F3")}");
        var nearby = roots.SelectMany(x => x.GetComponentsInChildren<Collider>(true))
            .Where(x => x.enabled && x.gameObject.activeInHierarchy && !x.isTrigger && !x.transform.IsChildOf(door) &&
                Vector3.Distance(x.bounds.ClosestPoint(door.position), door.position) < 8f)
            .OrderBy(x => Vector3.Distance(x.bounds.ClosestPoint(door.position), door.position)).Take(35);
        foreach (var c in nearby)
            Debug.Log($"L01_NEAR_COLLIDER name={c.name} bounds={c.bounds.center.ToString("F3")} size={c.bounds.size.ToString("F3")}");
        door.position -= Vector3.up * controller.openMoveDistance;
        Physics.SyncTransforms();
        foreach (float z in new[] { -3f, -1f, 0.3f, 1f, 2f, 3f, 5f })
        {
            var hits = Physics.RaycastAll(new Vector3(door.position.x, 15f, z), Vector3.down, 25f, ~0,
                QueryTriggerInteraction.Ignore).OrderBy(x => x.distance).Take(4);
            foreach (var hit in hits) Debug.Log($"L01_GROUND z={z:F1} hit={hit.collider.name} point={hit.point.ToString("F3")} normal={hit.normal.ToString("F3")}");
        }
        foreach (float y in new[] { 1.0f, 1.8f, 2.6f })
        {
            foreach (var hit in Physics.SphereCastAll(new Vector3(door.position.x, y, -1f), 0.22f,
                Vector3.forward, 4f, ~0, QueryTriggerInteraction.Ignore).OrderBy(x => x.distance))
                Debug.Log($"L01_PATH y={y:F1} hit={hit.collider.name} point={hit.point.ToString("F3")} distance={hit.distance:F3}");
        }
    }
}
