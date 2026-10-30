using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace M6IntegrationTests
{
    // This asmdef deliberately does not reference Assembly-CSharp; use the same
    // reflection boundary as the existing C3 integration tests. No new packages.
    [NonParallelizable]
    public sealed class C3V11SafeCirculationTests
    {
        private const string ScenePath = "Assets/_Modules_/02_Levels/Level03/Scenes/C_Main.unity";
        private GameObject player;
        private CharacterController body;
        private Component controller, progress, runtime;
        private Camera camera;
        private MethodInfo tick;
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private Component previousRuntime, inputService;
        private bool previousRuntimeEnabled, previousHandlerDetached, inventoryScopeActive, globalsCaptured, factoryOwned;
        private GameObject inventoryHost;
        private PropertyInfo singleton;
        private UnityEngine.Events.UnityAction<Scene, LoadSceneMode> previousSceneListener;
        private object previousInputContext;
        private bool previousSprintToggled, previousCrouchToggled;
        private float previousTimeScale;
        private CursorLockMode previousCursorLock;
        private bool previousCursorVisible, hadViewPreference;
        private int previousViewPreference;
        private string previousInventory, previousSlots;
        private bool previousPowered;
        private C3V11CollisionRecorder contacts;
        private const string ViewPreferenceKey = "SDGAdventure.Player.ViewMode.v1";

        private static Type RuntimeType(string name) => Type.GetType(name + ", Assembly-CSharp", true);
        private static Component Find(string name) => Object.FindObjectOfType(RuntimeType(name)) as Component;
        private static MethodInfo Method(Type type, string name, params Type[] parameterTypes)
        {
            MethodInfo method = type.GetMethod(name, AnyInstance, null, parameterTypes, null);
            Assert.IsNotNull(method, type.FullName + "." + name + " signature changed");
            return method;
        }
        private static object Call(Component item, string name, params object[] args)
        {
            Assert.IsNotNull(item, "Missing receiver for " + name);
            return Method(item.GetType(), name, args.Select(a => a.GetType()).ToArray()).Invoke(item, args);
        }
        private static object Property(Component item, string name)
        {
            Assert.IsNotNull(item, "Missing receiver for " + name);
            PropertyInfo property = item.GetType().GetProperty(name, AnyInstance);
            Assert.IsNotNull(property, item.GetType().FullName + "." + name + " property changed");
            return property.GetValue(item);
        }
        private static object Field(Component item, string name)
        {
            Assert.IsNotNull(item, "Missing receiver for " + name);
            FieldInfo field = item.GetType().GetField(name, AnyInstance);
            Assert.IsNotNull(field, item.GetType().FullName + "." + name + " field changed");
            return field.GetValue(item);
        }
        private static string InventoryFingerprint(Component owner) => string.Join("|",
            ((IEnumerable)Field(owner, "inventory")).Cast<object>().Select(entry => JsonUtility.ToJson(entry)));
        private static string SlotsFingerprint(Component owner) => string.Join("|", (string[])Field(owner, "quickSlots"));

        [UnitySetUp]
        public IEnumerator OpenFactory()
        {
            // NUnit can reuse the fixture instance, including after a failed setup.
            runtime = null;
            inventoryHost = null;
            previousRuntime = null;
            previousSceneListener = null;
            inputService = null;
            previousInputContext = null;
            previousHandlerDetached = inventoryScopeActive = factoryOwned = false;
            previousTimeScale = Time.timeScale;
            previousCursorLock = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            hadViewPreference = PlayerPrefs.HasKey(ViewPreferenceKey);
            previousViewPreference = PlayerPrefs.GetInt(ViewPreferenceKey);
            globalsCaptured = true;
            Time.timeScale = 1f;

            Type inputType = Type.GetType("SDGAdventure.SharedPlayer.PlayerInputService, SDGAdventure.SharedPlayer", true);
            inputService = inputType.GetProperty("Instance").GetValue(null) as Component;
            if (inputService)
            {
                previousInputContext = Property(inputService, "Context");
                previousSprintToggled = (bool)Field(inputService, "sprintToggled");
                previousCrouchToggled = (bool)Field(inputService, "crouchToggled");
            }

            // Suspend, never destroy, the pre-existing persistent inventory BEFORE
            // loading C_Main: its sceneLoaded handler also resets sorting state.
            Type runtimeType = RuntimeType("EcoAdventureRuntime");
            singleton = runtimeType.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public);
            Assert.IsNotNull(singleton);
            Assert.IsNotNull(singleton.GetSetMethod(true), "Inventory singleton setter changed");
            previousRuntime = singleton.GetValue(null) as Component;
            if (previousRuntime)
            {
                previousRuntimeEnabled = ((Behaviour)previousRuntime).enabled;
                previousInventory = InventoryFingerprint(previousRuntime);
                previousSlots = SlotsFingerprint(previousRuntime);
                previousPowered = (bool)Field(previousRuntime, "factoryPowered");
                previousSceneListener = (UnityEngine.Events.UnityAction<Scene, LoadSceneMode>)Delegate.CreateDelegate(
                    typeof(UnityEngine.Events.UnityAction<Scene, LoadSceneMode>), previousRuntime,
                    Method(runtimeType, "SceneLoaded", typeof(Scene), typeof(LoadSceneMode)));
            }
            inventoryScopeActive = true;
            if (previousRuntime)
            {
                ((Behaviour)previousRuntime).enabled = false;
                SceneManager.sceneLoaded -= previousSceneListener;
                previousHandlerDetached = true;
            }
            singleton.GetSetMethod(true).Invoke(null, new object[] { null });
            inventoryHost = new GameObject("V11_TEST_INVENTORY");
            runtime = inventoryHost.AddComponent(runtimeType);
            ((Behaviour)runtime).enabled = false; // API calls only; no physical input polling
            Assert.AreSame(runtime, singleton.GetValue(null));
            if (inputService) Call(inputService, "SetContext", Enum.Parse(previousInputContext.GetType(), "Inactive"));

            factoryOwned = true;
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            yield return null;
            player = GameObject.Find("M2_Player");
            Assert.IsNotNull(player);
            body = player.GetComponent<CharacterController>();
            Assert.IsNotNull(body);
            controller = player.GetComponent(RuntimeType("C3TreasurePuzzle.C3FirstPersonController"));
            Assert.IsNotNull(controller);
            ((Behaviour)controller).enabled = false;
            Component interactor = player.GetComponent(RuntimeType("C3TreasurePuzzle.C3PlayerInteractor"));
            Assert.IsNotNull(interactor);
            ((Behaviour)interactor).enabled = false;
            camera = player.GetComponentInChildren<Camera>();
            Assert.IsNotNull(camera);
            tick = Method(controller.GetType(), "TickMovement", typeof(Vector2), typeof(bool), typeof(bool), typeof(bool), typeof(float));
            progress = Find("C3TreasurePuzzle.C3TreasureProgress");
            Assert.IsNotNull(progress);
            contacts = player.AddComponent<C3V11CollisionRecorder>();
            Call(runtime, "BindCamera");
            Assert.AreSame(runtime, singleton.GetValue(null));
            Assert.AreSame(camera, Field(runtime, "activeCamera"));
            Assert.That(body.radius, Is.EqualTo(.3f).Within(.001f));
            Assert.That(body.height, Is.EqualTo(1.8f).Within(.001f));
            Assert.That(body.center, Is.EqualTo(Vector3.up * .9f));
            Assert.That(body.stepOffset, Is.EqualTo(.3f).Within(.001f));
            Assert.That(body.slopeLimit, Is.EqualTo(45f).Within(.001f));
            Assert.That(body.skinWidth, Is.EqualTo(.08f).Within(.001f));
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            // Stop test factories before restoring the original singleton; otherwise
            // their Update could grant sorted materials into the original state.
            try
            {
                if (factoryOwned)
                {
                    Scene factory = SceneManager.GetSceneByPath(ScenePath);
                    if (factory.IsValid() && factory.isLoaded)
                    {
                        Scene empty = SceneManager.CreateScene("V11_CLEANUP_" + Guid.NewGuid().ToString("N"));
                        SceneManager.SetActiveScene(empty);
                        foreach (GameObject root in factory.GetRootGameObjects()) root.SetActive(false);
                        yield return SceneManager.UnloadSceneAsync(factory);
                    }
                }
            }
            finally
            {
                try
                {
                    // Destroy ONLY the fixture-owned inventory, including after a setup
                    // failure. DestroyImmediate runs OnDestroy before restoration.
                    if (inventoryHost) Object.DestroyImmediate(inventoryHost);
                    if (inventoryScopeActive)
                    {
                        singleton.GetSetMethod(true).Invoke(null, new object[] { previousRuntime ? previousRuntime : null });
                        if (previousRuntime)
                        {
                            if (previousHandlerDetached) SceneManager.sceneLoaded += previousSceneListener;
                            ((Behaviour)previousRuntime).enabled = previousRuntimeEnabled;
                            Call(previousRuntime, "BindCamera"); // discard the unloaded camera binding
                        }
                    }
                }
                finally { RestoreGlobals(); }
            }
            if (inventoryScopeActive)
            {
                Assert.AreSame(previousRuntime, singleton.GetValue(null), "Fixture did not restore original singleton");
                if (previousRuntime)
                {
                    Assert.AreEqual(previousInventory, InventoryFingerprint(previousRuntime), "Original inventory changed");
                    Assert.AreEqual(previousSlots, SlotsFingerprint(previousRuntime), "Original quick slots changed");
                    Assert.AreEqual(previousPowered, Field(previousRuntime, "factoryPowered"), "Original factory state changed");
                }
            }
        }

        private void RestoreGlobals()
        {
            if (!globalsCaptured) return;
            try
            {
                if (inputService && previousInputContext != null)
                {
                    Call(inputService, "SetContext", previousInputContext);
                    inputService.GetType().GetField("sprintToggled", AnyInstance).SetValue(inputService, previousSprintToggled);
                    inputService.GetType().GetField("crouchToggled", AnyInstance).SetValue(inputService, previousCrouchToggled);
                }
            }
            finally
            {
                try
                {
                    if (hadViewPreference) PlayerPrefs.SetInt(ViewPreferenceKey, previousViewPreference);
                    else PlayerPrefs.DeleteKey(ViewPreferenceKey);
                    PlayerPrefs.Save();
                }
                finally
                {
                    Time.timeScale = previousTimeScale;
                    Cursor.lockState = previousCursorLock;
                    Cursor.visible = previousCursorVisible;
                }
            }
        }

        private void StartAt(Vector3 position)
        {
            // Initial condition only. All route travel thereafter uses the production
            // controller's TickMovement -> CharacterController.Move, including gravity.
            body.enabled = false;
            player.transform.position = position;
            player.transform.rotation = Quaternion.identity;
            body.enabled = true;
            Physics.SyncTransforms();
            body.Move(Vector3.down * .1f);
        }

        private IEnumerator WalkTo(float x, float y, float z)
        {
            Vector3 goal = new Vector3(x, y, z);
            for (int i = 0; i < 700; i++)
            {
                Vector3 direction = goal - player.transform.position;
                direction.y = 0f;
                if (direction.magnitude < .09f) break;
                player.transform.rotation = Quaternion.LookRotation(direction);
                tick.Invoke(controller, new object[] { Vector2.up, false, false, false, 1f / 60f });
                Assert.Greater(player.transform.position.y, .40f, "Fell through floor on route to " + goal);
                yield return null;
            }
            for (int i = 0; i < 12; i++)
            {
                tick.Invoke(controller, new object[] { Vector2.zero, false, false, false, 1f / 60f });
                yield return null;
            }
            Vector3 actual = player.transform.position;
            Assert.That(Vector2.Distance(new Vector2(actual.x, actual.z), new Vector2(x, z)),
                Is.LessThan(.15f), "Route blocked before " + goal + "; player=" + actual);
            Assert.That(actual.y, Is.EqualTo(y).Within(.16f), "Wrong floor at " + goal);
        }

        private Component[] Parts() => Object.FindObjectsOfType(RuntimeType("C3TreasurePuzzle.C3CollectiblePart"))
            .Cast<Component>().ToArray();
        private int Quantity(string id)
        {
            foreach (object entry in (IEnumerable)Field(runtime, "inventory"))
                if ((string)entry.GetType().GetField("id").GetValue(entry) == id)
                    return (int)entry.GetType().GetField("quantity").GetValue(entry);
            return 0;
        }

        [UnityTest]
        public IEnumerator WalkACollectExactlyOneChipAndReturnDownB()
        {
            StartAt(new Vector3(-12.5f, .60f, 2f)); // D-side first-floor entry aisle
            yield return WalkTo(-23.2f, .56f, 2f);
            yield return WalkTo(-23.2f, .56f, 3.1f);
            yield return WalkTo(-23.2f, 4.5f, 14.2f);
            yield return WalkTo(-23.2f, 4.5f, 19.2f);
            yield return WalkTo(-13.2f, 4.5f, 19.2f);
            Component chip = Parts().Single(p => (string)Property(p, "PartId") == "control_chip");
            Assert.IsTrue(chip.gameObject.activeInHierarchy);
            Assert.IsTrue(chip.GetComponentsInChildren<Renderer>().Any(r => r.enabled));
            Assert.IsTrue(chip.GetComponent<Collider>().enabled);
            Assert.AreSame(progress, Field(chip, "progress"));
            Collider bench = GameObject.Find("UpperChipMaintenanceBench").GetComponent<Collider>();
            Bounds chipBounds = chip.GetComponent<Collider>().bounds;
            Assert.IsTrue(bench.enabled && bench.gameObject.activeInHierarchy);
            Assert.That(chipBounds.min.y, Is.EqualTo(bench.bounds.max.y).Within(.08f), "Chip no longer rests on the real desk");
            Assert.That(chipBounds.center.x, Is.InRange(bench.bounds.min.x, bench.bounds.max.x));
            Assert.That(chipBounds.center.z, Is.InRange(bench.bounds.min.z, bench.bounds.max.z));
            // Aim from the physically reached position. An intervening rail, bench
            // collider or invisible wall must fail the original interactor's real ray.
            camera.transform.LookAt(chip.GetComponent<Collider>().bounds.center);
            Component interactor = player.GetComponent(RuntimeType("C3TreasurePuzzle.C3PlayerInteractor"));
            Assert.AreSame(chip, Call(interactor, "FindTarget"), "Chip cannot be targeted from maintenance aisle");
            int events = 0;
            Action changed = () => events++;
            EventInfo changedEvent = progress.GetType().GetEvent("Changed");
            Assert.IsNotNull(changedEvent, "Treasure Changed event contract changed");
            Assert.AreEqual(typeof(Action), changedEvent.EventHandlerType);
            changedEvent.AddEventHandler(progress, changed);
            try
            {
                Call(chip, "Interact", player);
                Assert.IsTrue((bool)Call(progress, "HasPart", "control_chip"));
                Assert.AreEqual(1, Quantity("control_chip"));
                Assert.AreEqual(1, Property(progress, "CollectedCount"));
                Assert.AreEqual(1, events);
                Assert.IsFalse(chip.gameObject.activeSelf);
                Call(chip, "Interact", player);
                Assert.IsFalse((bool)Call(progress, "TryCollect", "control_chip", "控制晶片"));
                Assert.AreEqual(1, Quantity("control_chip"));
                Assert.AreEqual(1, events);
            }
            finally { changedEvent.RemoveEventHandler(progress, changed); }
            // Continue beyond the real maintenance desk, then use the independent B exit.
            yield return WalkTo(-1.55f, 4.5f, 19.2f);
            yield return WalkTo(-1.55f, 4.5f, 14.2f);
            yield return WalkTo(-1.55f, .56f, 3.1f);
            yield return WalkTo(-1.55f, .56f, 2f);
        }

        [UnityTest]
        public IEnumerator WalkBThenRearConnectionAndDescendA()
        {
            StartAt(new Vector3(-1.55f, .60f, 2f));
            yield return WalkTo(-1.55f, .56f, 3.1f);
            yield return WalkTo(-1.55f, 4.5f, 14.2f);
            yield return WalkTo(-1.55f, 4.5f, 19.2f);
            yield return WalkTo(-23.2f, 4.5f, 19.2f);
            yield return WalkTo(-23.2f, 4.5f, 14.2f);
            yield return WalkTo(-23.2f, .56f, 3.1f);
        }

        [UnityTest]
        public IEnumerator GroundBCDoorApproachRemainsOpenUnderTheEastFlight()
        {
            StartAt(new Vector3(-3.5f, .60f, 10.7f));
            yield return WalkTo(.6f, .55f, 10.7f);
        }

        [UnityTest]
        public IEnumerator EveryGuardStopsStandingAndProneCapsulesIncludingCorners()
        {
            // Each row initializes a separate guard contact, then drives the original
            // capsule through it. These are barrier tests, not teleported route tests.
            Vector3[] starts = {
                new Vector3(-23.2f,4.5f,16f), new Vector3(-23.2f,4.5f,16f),
                new Vector3(-10f,4.5f,20.5f), new Vector3(-10f,4.5f,19.2f),
                new Vector3(-1.55f,4.5f,16f), new Vector3(-1.55f,4.5f,16f),
                new Vector3(-23.2f,4.5f,20.5f), new Vector3(-1.55f,4.5f,20.5f),
                new Vector3(-23.2f,2.53f,8.525f), new Vector3(-23.2f,2.53f,8.525f),
                new Vector3(-1.55f,2.53f,8.525f), new Vector3(-1.55f,2.53f,8.525f),
                new Vector3(-23.2f,.56f,3.1f), new Vector3(-23.2f,.56f,3.1f),
                new Vector3(-1.55f,.56f,3.1f), new Vector3(-1.55f,.56f,3.1f) };
            Vector3[] directions = { Vector3.left, Vector3.right, Vector3.forward, Vector3.back,
                Vector3.right, Vector3.left, new Vector3(-1,0,1).normalized, new Vector3(1,0,1).normalized,
                Vector3.left, Vector3.right, Vector3.left, Vector3.right,
                Vector3.left, Vector3.right, Vector3.left, Vector3.right };
            string[] guardPaths = {
                "V11_UPPER_GUARDS/GUARD_WEST_OUTER", "V11_UPPER_GUARDS/GUARD_WEST_INNER",
                "V11_UPPER_GUARDS/GUARD_REAR_NORTH", "V11_UPPER_GUARDS/GUARD_REAR_SOUTH",
                "V11_UPPER_GUARDS/GUARD_EAST_WALL_OUTER", "V11_UPPER_GUARDS/GUARD_EAST_WALL_INNER_AFTER_STAIR",
                "V11_UPPER_GUARDS/GUARD_REAR_NORTH", "V11_UPPER_GUARDS/GUARD_REAR_NORTH",
                "STAIR_A_WEST/GUARD_FLIGHT_-1", "STAIR_A_WEST/GUARD_FLIGHT_1",
                "STAIR_B_EAST/GUARD_FLIGHT_-1", "STAIR_B_EAST/GUARD_FLIGHT_1",
                "STAIR_A_WEST/GUARD_BOTTOM_-1", "STAIR_A_WEST/GUARD_BOTTOM_1",
                "STAIR_B_EAST/GUARD_BOTTOM_-1", "STAIR_B_EAST/GUARD_BOTTOM_1" };
            Transform circulationRoot = GameObject.Find("C3_V3_VerifiedCirculation").transform;
            for (int stance = 0; stance < 2; stance++)
            for (int sample = 0; sample < starts.Length; sample++)
            {
                StartAt(starts[sample]);
                body.height = stance == 0 ? (float)Field(controller, "standingHeight") :
                    Mathf.Clamp((float)Field(controller, "proneHeight"), body.radius * 2f, (float)Field(controller, "crouchHeight"));
                body.center = Vector3.up * (body.height * .5f);
                Physics.SyncTransforms();
                Transform guard = circulationRoot.Find(guardPaths[sample]);
                Assert.IsNotNull(guard, guardPaths[sample]);
                Collider expected = guard.GetComponent<Collider>();
                Assert.IsTrue(expected && expected.enabled && !expected.isTrigger);
                contacts.SideContacts.Clear();
                Vector3 from = player.transform.position;
                for (int i = 0; i < 100; i++)
                {
                    body.Move(directions[sample] * .04f + Vector3.down * .025f);
                    yield return null;
                }
                Assert.Less(Vector3.Distance(player.transform.position, from), 1.25f,
                    "Passed guard at sample " + sample + ", stance " + stance);
                Assert.Greater(player.transform.position.y, starts[sample].y - .20f, "Fell at guard sample " + sample);
                Assert.IsTrue(contacts.SideContacts.Contains(expected),
                    "No real controller contact with " + guardPaths[sample] + "; an unrelated obstacle may have stopped the player");
            }
            body.height = 1.8f;
            body.center = Vector3.up * .9f;
        }

        [UnityTest]
        public IEnumerator WorldGeometryMatchesEnabledCollidersAndHasNoHeadObstructions()
        {
            Transform root = GameObject.Find("C3_V3_VerifiedCirculation").transform;
            foreach (BoxCollider collider in root.GetComponentsInChildren<BoxCollider>())
            {
                if (!collider.enabled || collider.isTrigger) continue;
                Renderer renderer = collider.GetComponent<Renderer>();
                Assert.IsNotNull(renderer, collider.name + " is an unexplained invisible box");
                Assert.IsTrue(renderer.enabled && renderer.gameObject.activeInHierarchy, collider.name);
                Assert.Less(Vector3.Distance(renderer.bounds.center, collider.bounds.center), .01f, collider.name);
                Assert.Less(Vector3.Distance(renderer.bounds.size, collider.bounds.size), .01f, collider.name);
                Assert.Greater(collider.transform.lossyScale.x, 0f, "Mirrored mesh " + collider.name);
            }
            MeshCollider[] guards = root.GetComponentsInChildren<MeshCollider>()
                .Where(c => c.name.StartsWith("GUARD_")).ToArray();
            Assert.AreEqual(14, guards.Length); // six upper edges + two sides x two sections x two stairs
            foreach (MeshCollider guard in guards)
            {
                Assert.IsTrue(guard.enabled && guard.convex && !guard.isTrigger);
                Assert.IsTrue(guard.gameObject.activeInHierarchy);
                Renderer[] bars = guard.GetComponentsInChildren<Renderer>();
                Assert.Greater(bars.Length, 3);
                Bounds visible = bars[0].bounds;
                foreach (Renderer bar in bars) { Assert.IsTrue(bar.enabled); visible.Encapsulate(bar.bounds); }
                Assert.Less(Vector3.Distance(visible.min, guard.bounds.min), .12f, guard.name);
                Assert.Less(Vector3.Distance(visible.max, guard.bounds.max), .12f, guard.name);
            }
            Renderer[] sceneRenderers = Object.FindObjectsOfType<Renderer>();
            foreach (float x in new[] { -23.2f, -1.55f })
            foreach (float offset in new[] { -.35f, 0f, .35f })
            for (float z = 13.8f; z <= 20.8f; z += .2f) AssertClearHead(x + offset, z, sceneRenderers);
            for (float x = -23.2f; x <= -1.55f; x += .2f) AssertClearHead(x, 19.2f, sceneRenderers);
            foreach (string name in new[] { "UpperChipMaintenanceBench", "UpperDistributionBox" })
            {
                GameObject station = GameObject.Find(name);
                Assert.IsNotNull(station);
                Assert.IsTrue(station.GetComponent<Collider>().enabled);
                Assert.IsTrue(station.GetComponentsInChildren<Renderer>().All(r => r.enabled));
            }
            // Retained OBJ equipment still needs an actual imported WORLD-bounds check.
            Renderer feed = GameObject.Find("V9_BDoorToSorterVisibleBelt").GetComponentInChildren<Renderer>();
            Assert.That(feed.bounds.min.x, Is.EqualTo(-7.49f).Within(.24f));
            Assert.That(feed.bounds.max.x, Is.EqualTo(-.64f).Within(.24f));
        }

        private void AssertClearHead(float x, float z, Renderer[] sceneRenderers)
        {
            // The production collider supplies physics; render bounds also catch
            // collider-free decoration intersecting the upper body/head corridor.
            Bounds head = new Bounds(new Vector3(x, 5.98f, z), new Vector3(.76f, .64f, .20f));
            Assert.IsTrue(Physics.Raycast(new Vector3(x, 4.8f, z), Vector3.down,
                out RaycastHit floor, .5f, ~0, QueryTriggerInteraction.Ignore), "Gap under upper route");
            Assert.That(floor.point.y, Is.EqualTo(4.5f).Within(.01f));
            StringAssert.StartsWith("2F_", floor.collider.name);
            foreach (Renderer renderer in sceneRenderers)
            {
                if (!renderer.enabled || renderer.transform.IsChildOf(player.transform)) continue;
                if (!head.Intersects(renderer.bounds)) continue;
                // A combined non-convex OBJ (notably the B feed) can have a large
                // empty AABB. Test its actual imported triangles before reporting
                // an obstruction; bounds overlap alone is not collision evidence.
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                Assert.IsNotNull(filter, "Cannot inspect candidate geometry: " + renderer.name);
                Assert.IsNotNull(filter.sharedMesh, "Candidate has no imported mesh: " + renderer.name);
                GameObject probe = new GameObject("V11_HEAD_GEOMETRY_PROBE");
                GameObject candidate = new GameObject("V11_DECORATION_GEOMETRY_PROBE");
                try
                {
                    BoxCollider capsuleHead = probe.AddComponent<BoxCollider>();
                    capsuleHead.size = head.size;
                    capsuleHead.isTrigger = true;
                    candidate.transform.SetPositionAndRotation(filter.transform.position, filter.transform.rotation);
                    candidate.transform.localScale = filter.transform.lossyScale;
                    MeshCollider mesh = candidate.AddComponent<MeshCollider>();
                    mesh.sharedMesh = filter.sharedMesh;
                    mesh.isTrigger = false;
                    Assert.IsFalse(Physics.ComputePenetration(capsuleHead, head.center, Quaternion.identity,
                        mesh, candidate.transform.position, candidate.transform.rotation, out _, out _),
                        "Head corridor intersects " + renderer.name + " at " + head.center);
                }
                finally
                {
                    Object.DestroyImmediate(probe);
                    Object.DestroyImmediate(candidate);
                }
            }
        }

        [UnityTest]
        public IEnumerator LegacyCleanupPreservesFloorTriggersAndSurvivesSceneReload()
        {
            AssertNoHiddenArtObstacles();
            GameObject blockout = GameObject.Find("C_TwoFloor_Blockout");
            Assert.IsTrue(blockout.activeSelf);
            Assert.IsTrue(blockout.transform.Find("Floor1_Additions").gameObject.activeSelf);
            Assert.IsTrue(blockout.transform.Find("FutureGameplayMarkers").gameObject.activeSelf);
            string[] leaves = { "Stairs/Stair_West_Up/Tread_01", "Stairs/Stair_East_Down/Tread_22",
                "Floor2_Platforms/F2_MainWalkway", "Railings/R_MainSouth", "Supports/S_Main_m17p5_16p5" };
            foreach (string path in leaves)
            {
                Transform item = blockout.transform.Find(path);
                Assert.IsFalse(item.GetComponent<Collider>().enabled, path);
                Assert.IsFalse(item.GetComponent<Renderer>().enabled, path);
            }
            GameObject proxies = GameObject.Find("C_CollisionProxies");
            foreach (Collider collider in proxies.GetComponentsInChildren<Collider>())
                Assert.IsTrue(collider.enabled, "Required collision proxy disabled: " + collider.name);
            Component exit = Find("C3TreasurePuzzle.C3ExitTrigger");
            Assert.IsNotNull(exit);
            Assert.IsTrue(exit.GetComponent<Collider>().enabled && exit.GetComponent<Collider>().isTrigger);
            Component circulation = Find("C3TreasurePuzzle.C3V3Circulation");
            int before = GameObject.Find("C3_V3_VerifiedCirculation").GetComponentsInChildren<Collider>().Length;
            ((Behaviour)circulation).enabled = false;
            ((Behaviour)circulation).enabled = true;
            yield return null;
            Assert.AreEqual(before, GameObject.Find("C3_V3_VerifiedCirculation").GetComponentsInChildren<Collider>().Length);
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            yield return null;
            Assert.AreEqual(1, Object.FindObjectsOfType<Transform>().Count(t => t.name == "C3_V3_VerifiedCirculation"));
            Assert.AreEqual(before, GameObject.Find("C3_V3_VerifiedCirculation").GetComponentsInChildren<Collider>().Length);
            AssertNoHiddenArtObstacles();
        }

        private static void AssertNoHiddenArtObstacles()
        {
            Transform art = GameObject.Find("C_Factory_Art").transform;
            Transform bench = art.Find("Floor2_Industrial/UpperChipMaintenanceBench");
            Transform distribution = art.Find("Floor2_Industrial/UpperDistributionBox");
            // Enumerate every descendant, NOT the production retirement allowlist.
            // An unknown active collider requires classification; do not auto-disable it.
            foreach (Collider collider in art.GetComponentsInChildren<Collider>(true))
                if (collider.enabled && collider.gameObject.activeInHierarchy && !collider.isTrigger)
                    Assert.IsTrue(collider.transform.IsChildOf(bench) || collider.transform.IsChildOf(distribution),
                        "Unclassified active collider in replaced factory art: " + collider.name);
            foreach (Collider collider in Object.FindObjectsOfType<Collider>())
            {
                if (!collider.enabled || collider.isTrigger || collider.transform.IsChildOf(art)) continue;
                Renderer[] visuals = collider.GetComponentsInChildren<Renderer>(true);
                if (visuals.Length == 0) continue; // dedicated floor/wall proxies and visible bar assemblies are checked separately
                Assert.IsTrue(visuals.Any(r => r.enabled && r.gameObject.activeInHierarchy),
                    "Solid collider with only hidden visuals: " + collider.name);
            }
            GameObject originalTest = GameObject.Find("M2_TestInteractable");
            Assert.IsNotNull(originalTest);
            Assert.IsTrue(originalTest.GetComponent<Renderer>().enabled);
            Component interactable = originalTest.GetComponent(RuntimeType("C3TreasurePuzzle.C3TestInteractable"));
            Assert.IsNotNull(interactable, "Do not remove the original test interaction to remove its obstacle");
        }

        [UnityTest]
        public IEnumerator OriginalPartsConsoleSortingBenchesAndWeaponStillOperate()
        {
            Component console = GameObject.Find("M4_PuzzleConsole").GetComponent(RuntimeType("C3TreasurePuzzle.C3PuzzleConsole"));
            Component device = Find("C3TreasurePuzzle.C3DeviceState");
            Assert.IsFalse((bool)Call(console, "OpenPanel"), "Console bypassed missing parts");
            Assert.IsFalse((bool)Call(device, "TryActivate"));
            Component[] parts = Parts();
            Assert.AreEqual(3, parts.Length);
            foreach (Component part in parts)
            {
                string id = (string)Property(part, "PartId");
                Call(part, "Interact", player);
                Call(part, "Interact", player);
                Assert.AreEqual(1, Quantity(id), id + " duplicated inventory grant");
                Assert.IsTrue((bool)Call(progress, "HasPart", id));
            }
            Assert.AreEqual(3, Property(progress, "CollectedCount"));
            Assert.IsTrue((bool)Call(console, "OpenPanel"));
            Type step = RuntimeType("C3TreasurePuzzle.C3ProcessingStep");
            foreach (string name in new[] { "Sorting", "Recycling", "PollutionTreatment" })
                Assert.IsTrue((bool)Call(console, "SelectStep", Enum.Parse(step, name)));
            Assert.IsTrue((bool)Call(console, "Confirm"));
            Assert.IsTrue((bool)Call(console, "ConfirmActivation"));
            Call(device, "NotifyStartupFinished");
            yield return null;
            Assert.IsTrue((bool)Field(runtime, "factoryPowered"));
            for (int lane = 0; lane < 3; lane++)
            {
                Assert.IsTrue(GameObject.Find("SORTER_MAIN_BODY_" + lane).GetComponent<Collider>().enabled);
                Assert.IsNotNull(GameObject.Find("V3_CONNECTED_DIVERT_TO_HOPPER_" + lane));
                Assert.IsNotNull(GameObject.Find("CONNECTED_LOWER_OUTFEED_" + lane));
            }
            // Wait for the real presentation -> workshop sorting event path, rather
            // than injecting sorted materials or treating machine presence as success.
            Component presentation = Find("C3TreasurePuzzle.C3FactoryPresentation");
            float deadline = Time.realtimeSinceStartup + 35f;
            while ((int)Property(presentation, "CompletedCycles") < 3 && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.GreaterOrEqual((int)Property(presentation, "CompletedCycles"), 3);
            foreach (string cache in new[] { "Distributed_RecycledPlastic_F", "Distributed_RecycledMetal_F", "Hidden_ElectronicScrapCache_F" })
            {
                Component target = GameObject.Find(cache).GetComponent(RuntimeType("EcoWorkshopInteractable"));
                Call(target, "Interact");
                Assert.IsFalse(target.gameObject.activeSelf, cache + " no longer dispenses sorted material");
            }
            Component inspection = GameObject.Find("Secondary_RecyclingInspectionWorkbench_F").GetComponent(RuntimeType("EcoWorkshopInteractable"));
            Call(inspection, "Interact");
            Component assembly = GameObject.Find("Hidden_WeaponAssemblyBench_F").GetComponent(RuntimeType("EcoWorkshopInteractable"));
            Call(assembly, "Interact");
            Assert.IsTrue((bool)Field(runtime, "weaponCrafted"));
            Assert.AreEqual(1, Quantity("eco_quantum_projector"));
            Assert.AreEqual(0, Quantity("eco_recycled_metal"));
            Assert.AreEqual(0, Quantity("eco_recycled_plastic"));
            Assert.AreEqual(0, Quantity("eco_recovered_circuit"));
            string[] slots = (string[])Field(runtime, "quickSlots");
            Assert.Contains("eco_quantum_projector", slots);
            Assert.IsTrue((bool)Property(runtime, "IsQuantumWeaponEquipped"));
            int ammo = (int)Field(runtime, "ammoInMagazine");
            Call(runtime, "Shoot");
            Assert.AreEqual(ammo - 1, Field(runtime, "ammoInMagazine"));
            Assert.IsNotNull(GameObject.Find("EcoQuantumTracer"));
            // Persistent inventory and equipment survive an actual scene reload.
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            yield return null;
            Assert.AreEqual(1, Quantity("eco_quantum_projector"));
            Assert.AreEqual(ammo - 1, Field(runtime, "ammoInMagazine"));
            Assert.Contains("eco_quantum_projector", (string[])Field(runtime, "quickSlots"));
        }
    }
}
