using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using SDGAdventure.SharedPlayer;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine;
using UnityEngine.TestTools;

namespace M6IntegrationTests
{
    public sealed class C3V141ReloadTests
    {
        private C3V11Phase2RecyclingLineTests scope;
        private Component game;
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private object Get(string name) => game.GetType().GetField(name, Flags).GetValue(game);
        private void Set(string name, object value) => game.GetType().GetField(name, Flags).SetValue(game, value);
        private object Call(string name) => game.GetType().GetMethod(name, Flags).Invoke(game, null);

        [UnitySetUp] public IEnumerator Open()
        {
            scope = new C3V11Phase2RecyclingLineTests();
            yield return scope.Open();
            game = C3V11Phase2RecyclingLineTests.GameType("EcoAdventureRuntime").GetProperty("Instance").GetValue(null) as Component;
            C3V11Phase2RecyclingLineTests.Call(game, "AddItem", "eco_quantum_projector", "test weapon", 1);
            ((string[])Get("quickSlots"))[0] = "eco_quantum_projector";
            Set("selectedSlot", 0);
            Set("ammoInMagazine", 10);
            Set("reserveAmmo", 20);
            Call("BindCamera");
            Call("BuildHeldModel");
        }
        [UnityTearDown] public IEnumerator Close() { yield return scope.Close(); }

        [UnityTest] public IEnumerator QuestPartDropAndRepickKeepsOneItemAndQuestReceipt()
        {
            Type partType = C3V11Phase2RecyclingLineTests.GameType("C3TreasurePuzzle.C3CollectiblePart");
            Component[] parts = System.Array.ConvertAll(UnityEngine.Object.FindObjectsOfType(partType), o => (Component)o);
            Component part = System.Array.Find(parts, p => (string)p.GetType().GetProperty("PartId").GetValue(p) == "fuse");
            Assert.IsNotNull(part);
            var player = UnityEngine.Object.FindObjectOfType(C3V11Phase2RecyclingLineTests.GameType("C3TreasurePuzzle.C3FirstPersonController")) as Component;
            Assert.IsNotNull(player);
            Component progress = C3V11Phase2RecyclingLineTests.Find("C3TreasurePuzzle.C3TreasureProgress");
            int before = (int)C3V11Phase2RecyclingLineTests.Member(progress, "CollectedCount");
            C3V11Phase2RecyclingLineTests.Call(part, "Interact", player.gameObject);
            Assert.IsFalse(part.gameObject.activeSelf);
            Assert.AreEqual(before + 1, C3V11Phase2RecyclingLineTests.Member(progress, "CollectedCount"));
            ((string[])Get("quickSlots"))[2] = "fuse";
            Set("selectedSlot", 2);
            var motor = player.GetComponent<CharacterController>();
            motor.enabled = false;
            player.transform.position = new Vector3(100, 21, 100);
            motor.enabled = true;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = new Vector3(100, 20, 100);
            floor.transform.localScale = new Vector3(12, .2f, 12);
            Physics.SyncTransforms();
            try
            {
                Assert.IsTrue((bool)Call("TryDropCurrent"));
                Assert.IsTrue(part.gameObject.activeSelf);
                Assert.IsFalse((bool)C3V11Phase2RecyclingLineTests.Call(game, "Has", "fuse", 1));
                var body = part.GetComponent<Rigidbody>();
                Assert.IsNotNull(body);
                Assert.IsTrue(body.useGravity);
                Assert.IsFalse(body.isKinematic);
                float start = part.transform.position.y;
                yield return new WaitForSeconds(2f);
                Assert.Less(part.transform.position.y, start);
                Assert.Greater(part.GetComponent<Collider>().bounds.min.y, 20f);
                C3V11Phase2RecyclingLineTests.Call(part, "Interact", player.gameObject);
                Assert.IsFalse(part.gameObject.activeSelf);
                Assert.IsTrue((bool)C3V11Phase2RecyclingLineTests.Call(game, "Has", "fuse", 1));
                Assert.AreEqual(before + 1, C3V11Phase2RecyclingLineTests.Member(progress, "CollectedCount"));
                Assert.AreEqual("fuse", ((string[])Get("quickSlots"))[2]);
            }
            finally { UnityEngine.Object.Destroy(floor); }
        }

        [UnityTest] public IEnumerator QuickSlotsSwapMoveReplaceRepairAndPreserveStacks()
        {
            var slots = (string[])Get("quickSlots");
            System.Array.Clear(slots, 0, slots.Length);
            C3V11Phase2RecyclingLineTests.Call(game, "AddItem", "a", "A", 2);
            C3V11Phase2RecyclingLineTests.Call(game, "AddItem", "b", "B", 1);
            C3V11Phase2RecyclingLineTests.Call(game, "AddItem", "c", "C", 1);
            C3V11Phase2RecyclingLineTests.Call(game, "AddItem", "d", "D", 1);
            C3V11Phase2RecyclingLineTests.Call(game, "AddItem", "e", "E", 1);
            C3V11Phase2RecyclingLineTests.Call(game, "AddItem", "f", "F", 1);
            Assert.IsTrue((bool)C3V11Phase2RecyclingLineTests.Call(game, "AssignQuickSlot", 2, "a"));
            CollectionAssert.AreEqual(new[] { "c", "b", "a", "d", "e" }, slots);
            Assert.IsTrue((bool)C3V11Phase2RecyclingLineTests.Call(game, "AssignQuickSlot", 2, "a"));
            Assert.IsTrue((bool)C3V11Phase2RecyclingLineTests.Call(game, "AssignQuickSlot", 1, "f"));
            CollectionAssert.AreEqual(new[] { "c", "f", "a", "d", "e" }, slots);
            Assert.IsTrue((bool)C3V11Phase2RecyclingLineTests.Call(game, "Has", "b", 1));
            Assert.IsTrue((bool)C3V11Phase2RecyclingLineTests.Call(game, "Has", "a", 2));
            Assert.IsTrue((bool)C3V11Phase2RecyclingLineTests.Call(game, "Consume", "d", 1));
            Assert.IsTrue((bool)C3V11Phase2RecyclingLineTests.Call(game, "AssignQuickSlot", 3, "a"));
            CollectionAssert.AreEqual(new[] { "c", "f", null, "a", "e" }, slots);
            slots[2] = "a"; // simulate an old duplicate without touching inventory
            Set("selectedSlot", 3);
            Call("RepairQuickSlots");
            Assert.IsNull(slots[2]); Assert.AreEqual("a", slots[3]);
            Assert.IsTrue((bool)C3V11Phase2RecyclingLineTests.Call(game, "Consume", "a", 2));
            Assert.IsNull(slots[3]);
            yield return null;
        }

        [UnityTest] public IEnumerator ConsoleRequiresHeldPartsThenSubmitsOnceAndClearsHands()
        {
            Type partType = C3V11Phase2RecyclingLineTests.GameType("C3TreasurePuzzle.C3CollectiblePart");
            var parts = System.Array.ConvertAll(UnityEngine.Object.FindObjectsOfType(partType), o => (Component)o);
            var player = UnityEngine.Object.FindObjectOfType(C3V11Phase2RecyclingLineTests.GameType("C3TreasurePuzzle.C3FirstPersonController")) as Component;
            Component progress = C3V11Phase2RecyclingLineTests.Find("C3TreasurePuzzle.C3TreasureProgress");
            Component console = C3V11Phase2RecyclingLineTests.Find("C3TreasurePuzzle.C3PuzzleConsole");
            foreach (Component part in parts) C3V11Phase2RecyclingLineTests.Call(part, "Interact", player.gameObject);
            Assert.AreEqual(3, C3V11Phase2RecyclingLineTests.Member(progress, "CollectedCount"));
            Assert.IsTrue((bool)C3V11Phase2RecyclingLineTests.Call(game, "Consume", "fuse", 1));
            Assert.IsFalse((bool)C3V11Phase2RecyclingLineTests.Call(console, "OpenPanel"));
            Assert.IsFalse((bool)C3V11Phase2RecyclingLineTests.Member(progress, "PartsSubmitted"));
            Assert.IsTrue((bool)C3V11Phase2RecyclingLineTests.Call(game, "Has", "control_chip", 1));
            C3V11Phase2RecyclingLineTests.Call(game, "AddItem", "fuse", "Fuse", 2);
            C3V11Phase2RecyclingLineTests.Call(game, "AssignQuickSlot", 2, "fuse");
            Set("selectedSlot", 2); Call("BuildHeldModel");
            Assert.IsNotNull(Get("weaponModel"));
            Assert.IsTrue((bool)C3V11Phase2RecyclingLineTests.Call(console, "OpenPanel"));
            Assert.IsTrue((bool)C3V11Phase2RecyclingLineTests.Member(progress, "PartsSubmitted"));
            foreach (string id in new[] { "fuse", "control_chip", "filter_module" })
            {
                Assert.IsFalse((bool)C3V11Phase2RecyclingLineTests.Call(game, "Has", id, 1));
                CollectionAssert.DoesNotContain((string[])Get("quickSlots"), id);
                Assert.IsFalse((bool)C3V11Phase2RecyclingLineTests.Call(progress, "TryCollect", id, id));
            }
            Assert.IsNull(Get("weaponModel"));
            Assert.IsTrue((bool)C3V11Phase2RecyclingLineTests.Call(game, "Has", "eco_quantum_projector", 1));
            Assert.IsFalse((bool)C3V11Phase2RecyclingLineTests.Call(console, "OpenPanel"));
            foreach (Component part in parts)
            {
                Assert.IsFalse(part.gameObject.activeSelf);
                part.gameObject.SetActive(true);
                Assert.IsFalse((bool)C3V11Phase2RecyclingLineTests.Call(part, "CanInteract", player.gameObject));
                part.gameObject.SetActive(false);
            }
            Assert.AreEqual(3, C3V11Phase2RecyclingLineTests.Member(progress, "CollectedCount"));
            yield return null;
        }

        [UnityTest] public IEnumerator WorkshopMaterialPresentationUsesExistingCacheVisuals()
        {
            string[] ids = { "eco_recycled_metal", "eco_recovered_circuit", "eco_recycled_plastic" };
            foreach (string id in ids)
            {
                C3V11Phase2RecyclingLineTests.Call(game, "AddItem", id, id, 1);
                Set("selectedSlot", 2);
                Assert.IsTrue((bool)C3V11Phase2RecyclingLineTests.Call(game, "AssignQuickSlot", 2, id));
                Call("BuildHeldModel");
                var held = (GameObject)Get("weaponModel");
                Assert.IsNotNull(held, id);
                Transform visual = held.transform.Find("OriginalWorkshopMaterialVisual");
                Assert.IsNotNull(visual, id);
                Assert.Greater(visual.GetComponentsInChildren<MeshRenderer>(true).Length, 0, id);
                Assert.AreEqual(0, visual.GetComponentsInChildren<Collider>(true).Length, id);
                Assert.AreEqual(0, visual.GetComponentsInChildren<Rigidbody>(true).Length, id);
            }
            yield return null;
        }

        [UnityTest] public IEnumerator QuantumWeaponRepeatedDropPreservesAmmoAndCancelsReload()
        {
            var player = UnityEngine.Object.FindObjectOfType(C3V11Phase2RecyclingLineTests.GameType("C3TreasurePuzzle.C3FirstPersonController")) as Component;
            var motor = player.GetComponent<CharacterController>();
            motor.enabled = false; player.transform.position = new Vector3(100, 21, 100); motor.enabled = true;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = new Vector3(100, 20, 100);
            floor.transform.localScale = new Vector3(12, .2f, 12);
            Physics.SyncTransforms();
            Set("ammoInMagazine", 21); Set("reserveAmmo", 72);
            Set("reloadRoutine", ((MonoBehaviour)game).StartCoroutine((IEnumerator)Call("Reload")));
            Assert.IsTrue((bool)Get("reloading"));
            try
            {
                for (int cycle = 0; cycle < 2; cycle++)
                {
                    Assert.IsTrue((bool)Call("TryDropCurrent"));
                    Assert.IsFalse((bool)Get("reloading"));
                    Assert.IsFalse((bool)C3V11Phase2RecyclingLineTests.Call(game, "Has", "eco_quantum_projector", 1));
                    var pickup = (Component)Get("droppedWeapon");
                    Assert.IsNotNull(pickup);
                    Assert.IsTrue(pickup.GetComponent<Rigidbody>().useGravity);
                    Assert.IsFalse(pickup.GetComponent<Rigidbody>().isKinematic);
                    Assert.IsNotNull(pickup.GetComponent<Collider>());
                    Assert.IsNotNull(pickup.transform.Find("ReclaimedQuantumRifle_Visual"));
                    Call("Shoot");
                    Assert.IsFalse(((IEnumerator)Call("Reload")).MoveNext());
                    Assert.AreEqual(21, Get("ammoInMagazine")); Assert.AreEqual(72, Get("reserveAmmo"));
                    float initialY = pickup.transform.position.y;
                    yield return new WaitForSeconds(1f);
                    Assert.Less(pickup.transform.position.y, initialY);
                    Assert.Greater(pickup.GetComponent<Collider>().bounds.min.y, 19.95f);
                    C3V11Phase2RecyclingLineTests.Call(pickup, "Interact", player.gameObject);
                    Assert.IsTrue((bool)C3V11Phase2RecyclingLineTests.Call(game, "Has", "eco_quantum_projector", 1));
                    Assert.AreEqual(21, Get("ammoInMagazine")); Assert.AreEqual(72, Get("reserveAmmo"));
                    Assert.AreEqual("eco_quantum_projector", ((string[])Get("quickSlots"))[0]);
                    yield return null;
                }
                // Destruction on scene unload must return the sole weapon to its owner.
                Assert.IsTrue((bool)Call("TryDropCurrent"));
                var lostWorldObject = (Component)Get("droppedWeapon");
                UnityEngine.Object.Destroy(lostWorldObject.gameObject);
                yield return null;
                Assert.IsTrue((bool)C3V11Phase2RecyclingLineTests.Call(game, "Has", "eco_quantum_projector", 1));
                Assert.AreEqual(21, Get("ammoInMagazine")); Assert.AreEqual(72, Get("reserveAmmo"));
            }
            finally { UnityEngine.Object.Destroy(floor); }
        }

        [UnityTest] public IEnumerator WorkshopMaterialsShareVisualAndTransferOneUnitAtATime()
        {
            var player = UnityEngine.Object.FindObjectOfType(C3V11Phase2RecyclingLineTests.GameType("C3TreasurePuzzle.C3FirstPersonController")) as Component;
            var motor = player.GetComponent<CharacterController>();
            motor.enabled = false; player.transform.position = new Vector3(100, 21, 100); motor.enabled = true;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = new Vector3(100, 20, 100);
            floor.transform.localScale = new Vector3(60, .2f, 60);
            Type pickupType = C3V11Phase2RecyclingLineTests.GameType("EcoMaterialWorldPickup");
            string[] ids = { "eco_recycled_plastic", "eco_recovered_circuit", "eco_recycled_metal" };
            try
            {
                foreach (string id in ids)
                {
                    var source = (Transform)C3V11Phase2RecyclingLineTests.Call(game, "FindMaterialVisual", id);
                    Assert.IsNotNull(source, id);
                    var old = source.parent.Find("VisibleSalvagedMaterial");
                    Assert.IsNotNull(old, id);
                    Assert.IsFalse(old.GetComponent<Renderer>().enabled, id + " old material visual");
                    C3V11Phase2RecyclingLineTests.Call(game, "AddItem", id, id, 3);
                    Set("selectedSlot", 2);
                    Assert.IsTrue((bool)C3V11Phase2RecyclingLineTests.Call(game, "AssignQuickSlot", 2, id));
                    Call("BuildHeldModel");
                    var held = (GameObject)Get("weaponModel");
                    Assert.IsNotNull(held);
                    var heldVisual = held.transform.Find("OriginalWorkshopMaterialVisual");
                    Assert.IsNotNull(heldVisual);
                    Physics.SyncTransforms();
                    var drops = new System.Collections.Generic.List<Component>();
                    for (int n = 0; n < 3; n++)
                    {
                        Assert.IsTrue((bool)Call("TryDropCurrent"), id + " drop " + n);
                        Assert.AreEqual(n < 2, (bool)C3V11Phase2RecyclingLineTests.Call(game, "Has", id, 2 - n));
                        Component pickup = null;
                        foreach (UnityEngine.Object candidate in UnityEngine.Object.FindObjectsOfType(pickupType))
                        {
                            var component = (Component)candidate;
                            if (!drops.Contains(component)) { pickup = component; break; }
                        }
                        Assert.IsNotNull(pickup);
                        drops.Add(pickup);
                        Assert.IsTrue(pickup.GetComponent<Rigidbody>().useGravity);
                        Assert.IsFalse(pickup.GetComponent<Rigidbody>().isKinematic);
                        Assert.IsNotNull(pickup.GetComponent<Collider>());
                        var worldVisual = pickup.transform.Find("OriginalWorkshopMaterialVisual");
                        Assert.IsNotNull(worldVisual);
                        var heldMeshes = heldVisual.GetComponentsInChildren<MeshFilter>(true);
                        var worldMeshes = worldVisual.GetComponentsInChildren<MeshFilter>(true);
                        Assert.AreEqual(heldMeshes.Length, worldMeshes.Length);
                        for (int k = 0; k < heldMeshes.Length; k++)
                        {
                            Assert.AreSame(heldMeshes[k].sharedMesh, worldMeshes[k].sharedMesh);
                            CollectionAssert.AreEqual(heldMeshes[k].GetComponent<Renderer>().sharedMaterials,
                                worldMeshes[k].GetComponent<Renderer>().sharedMaterials);
                        }
                        pickup.transform.position = new Vector3(110 + n * 2, 21, 100);
                        Physics.SyncTransforms();
                    }
                    Assert.IsNull(((string[])Get("quickSlots"))[2]);
                    foreach (Component pickup in drops)
                    {
                        C3V11Phase2RecyclingLineTests.Call(pickup, "Interact", player.gameObject);
                        C3V11Phase2RecyclingLineTests.Call(pickup, "Interact", player.gameObject);
                    }
                    Assert.IsTrue((bool)C3V11Phase2RecyclingLineTests.Call(game, "Has", id, 3));
                    Assert.IsFalse((bool)C3V11Phase2RecyclingLineTests.Call(game, "Has", id, 4));
                    Assert.IsTrue((bool)C3V11Phase2RecyclingLineTests.Call(game, "Consume", id, 3));
                    yield return null;
                }
            }
            finally { UnityEngine.Object.Destroy(floor); }
        }

        [UnityTest] public IEnumerator WorkshopCraftRequiresHeldMaterialAndWorldDestructionRecoversUnit()
        {
            Assert.IsTrue((bool)C3V11Phase2RecyclingLineTests.Call(game, "Consume", "eco_quantum_projector", 1));
            Set("factoryPowered", true);
            C3V11Phase2RecyclingLineTests.Call(game, "ReceiveSortedMaterial", 0);
            C3V11Phase2RecyclingLineTests.Call(game, "ReceiveSortedMaterial", 1);
            Assert.IsTrue((bool)C3V11Phase2RecyclingLineTests.Call(game, "RecoverSortedMaterial", 0));
            Assert.IsTrue((bool)C3V11Phase2RecyclingLineTests.Call(game, "RecoverSortedMaterial", 1));
            Assert.IsTrue((bool)C3V11Phase2RecyclingLineTests.Call(game, "RecoverCircuit"));
            Set("selectedSlot", 2);
            Assert.IsTrue((bool)C3V11Phase2RecyclingLineTests.Call(game, "AssignQuickSlot", 2, "eco_recovered_circuit"));
            var player = UnityEngine.Object.FindObjectOfType(C3V11Phase2RecyclingLineTests.GameType("C3TreasurePuzzle.C3FirstPersonController")) as Component;
            var motor = player.GetComponent<CharacterController>();
            motor.enabled = false; player.transform.position = new Vector3(100, 21, 100); motor.enabled = true;
            Physics.SyncTransforms();
            Assert.IsTrue((bool)Call("TryDropCurrent"));
            Assert.IsFalse((bool)C3V11Phase2RecyclingLineTests.Call(game, "CraftWeapon"));
            Assert.IsFalse((bool)C3V11Phase2RecyclingLineTests.Call(game, "RecoverCircuit"));
            Assert.IsTrue((bool)C3V11Phase2RecyclingLineTests.Call(game, "Has", "eco_recycled_metal", 1));
            var pickup = UnityEngine.Object.FindObjectOfType(C3V11Phase2RecyclingLineTests.GameType("EcoMaterialWorldPickup")) as Component;
            Assert.IsNotNull(pickup);
            UnityEngine.Object.Destroy(pickup.gameObject);
            yield return null; // same recovery path as a scene object being destroyed
            Assert.IsTrue((bool)C3V11Phase2RecyclingLineTests.Call(game, "Has", "eco_recovered_circuit", 1));
            Assert.IsTrue((bool)C3V11Phase2RecyclingLineTests.Call(game, "CraftWeapon"));
            Assert.IsFalse((bool)C3V11Phase2RecyclingLineTests.Call(game, "Has", "eco_recovered_circuit", 1));
            Assert.IsFalse((bool)C3V11Phase2RecyclingLineTests.Call(game, "CraftWeapon"));
        }

        [UnityTest] public IEnumerator ReloadBlocksShotsAndTransfersAmmoOnce()
        {
            var routine = (IEnumerator)Call("Reload");
            ((MonoBehaviour)game).StartCoroutine(routine);
            Assert.IsTrue((bool)Get("reloading"));
            Call("Shoot");
            Assert.AreEqual(10, Get("ammoInMagazine"));
            yield return new WaitForSeconds(1.5f);
            Assert.IsFalse((bool)Get("reloading"));
            Assert.AreEqual(24, Get("ammoInMagazine"));
            Assert.AreEqual(6, Get("reserveAmmo"));
            yield return new WaitForSeconds(.2f);
            Assert.AreEqual(24, Get("ammoInMagazine"));
            Assert.AreEqual(6, Get("reserveAmmo"));
        }

        [UnityTest] public IEnumerator SwitchingWeaponCancelsWithoutTransferringAmmo()
        {
            ((MonoBehaviour)game).StartCoroutine((IEnumerator)Call("Reload"));
            yield return null;
            Set("selectedSlot", 1);
            yield return null;
            Assert.IsFalse((bool)Get("reloading"));
            Assert.AreEqual(10, Get("ammoInMagazine"));
            Assert.AreEqual(20, Get("reserveAmmo"));
        }
        [UnityTest] public IEnumerator ReboundInputDrivesCMainReloadAndUiBlocksIt()
        {
            var previousBackground = InputSystem.settings.backgroundBehavior;
            var previousEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var input = PlayerInputService.Instance;
            string saved = input.Actions.SaveBindingOverridesAsJson();
            string testSettingsKey = "SDGAdventure.C3.ReloadRebindTest." + Guid.NewGuid().ToString("N");
            PlayerCursor.BeginSimulation(CursorLockMode.Locked);
            try
            {
                // Use the real collision checks and persistence path with an isolated
                // test key; never write the player's normal settings.
                var bindings = new PlayerKeybindingController(input.Actions, new PlayerSettingsStore(testSettingsKey));
                var reload = input.Actions.FindAction("Gameplay/Reload", true);
                var drop = input.Actions.FindAction("Gameplay/Drop", true);
                Assert.AreEqual("<Keyboard>/r", reload.bindings[0].effectivePath);
                Assert.AreEqual("<Keyboard>/g", drop.bindings[0].effectivePath);
                input.SetContext(PlayerInputContext.Gameplay);
                InputSystem.Update();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R)); InputSystem.Update(); Call("Update");
                Assert.IsTrue((bool)Get("reloading"), "Default R must start the real reload coroutine.");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
                yield return new WaitForSeconds(1.5f);
                Assert.AreEqual(24, Get("ammoInMagazine")); Assert.AreEqual(6, Get("reserveAmmo"));
                Set("ammoInMagazine", 10); Set("reserveAmmo", 20);
                Assert.IsFalse(bindings.TryApply("Gameplay", "Reload", 0, "<Keyboard>/g", out string conflict));
                StringAssert.Contains("Gameplay/Drop", conflict);
                Assert.AreEqual("<Keyboard>/r", reload.bindings[0].effectivePath, "Rejected G must leave Reload unchanged.");
                Assert.AreEqual("<Keyboard>/g", drop.bindings[0].effectivePath, "Rejected G must leave Drop unchanged.");
                Assert.IsTrue(bindings.TryApply("Gameplay", "Reload", 0, "<Keyboard>/n", out string error), error);
                Assert.AreEqual("<Keyboard>/N", reload.bindings[0].effectivePath);
                Assert.AreEqual("<Keyboard>/g", drop.bindings[0].effectivePath);
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R)); InputSystem.Update(); Call("Update");
                Assert.IsFalse((bool)Get("reloading"), "Old R must not start reload after rebinding.");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update(); yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.N)); InputSystem.Update(); Call("Update");
                Assert.IsTrue((bool)Get("reloading"), "The new key must start the real reload coroutine.");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
                yield return new WaitForSeconds(1.5f);
                Assert.AreEqual(24, Get("ammoInMagazine")); Assert.AreEqual(6, Get("reserveAmmo"));
                Set("ammoInMagazine", 10); Set("reserveAmmo", 20);
                var menu = UnityEngine.Object.FindFirstObjectByType<PlayerPauseMenu>();
                Assert.NotNull(menu); menu.Pause(); Assert.AreEqual(PlayerInputContext.UI, input.Context);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.N)); InputSystem.Update(); Call("Update");
                Assert.IsFalse((bool)Get("reloading"), "Reload must not start while paused.");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update(); yield return null;
                menu.OpenSettings();
                var row = menu.View.Panel.Find("ScrollViewport/ScrollContent/Binding_Reload_0");
                Assert.NotNull(row, "Reload row must exist in the real settings UI.");
                Assert.AreEqual(input.Keybindings.BindingDisplay("Gameplay", "Reload", 0),
                    row.GetComponentInChildren<UnityEngine.UI.Button>().GetComponentInChildren<UnityEngine.UI.Text>().text);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.N)); InputSystem.Update(); Call("Update");
                Assert.IsFalse((bool)Get("reloading"), "Reload must not start in settings.");
                Assert.AreEqual(10, Get("ammoInMagazine")); Assert.AreEqual(20, Get("reserveAmmo"));
                menu.BackToPause(); menu.Resume();
            }
            finally
            {
                input.Actions.RemoveAllBindingOverrides(); input.Actions.LoadBindingOverridesFromJson(saved);
                PlayerPrefs.DeleteKey(testSettingsKey); PlayerPrefs.Save();
                PlayerCursor.EndSimulation(); InputSystem.RemoveDevice(keyboard);
                InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInput;
                InputSystem.settings.backgroundBehavior = previousBackground;
            }
        }

    }
}
