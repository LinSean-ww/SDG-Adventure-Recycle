using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace M7SharedPlayerTests
{
    public sealed class Level04TransitionTests
    {
        [UnityTest]
        public IEnumerator SingleSceneTransitionsPreserveInventoryAmmoAndSelection()
        {
            yield return null;
            var runtimeType = Type.GetType("EcoAdventureRuntime, Assembly-CSharp", true);
            var runtime = runtimeType.GetProperty("Instance").GetValue(null);
            Assert.NotNull(runtime);
            var coordinatorType = Type.GetType("SDGAdventure.ModuleSystem.ModuleCoordinator, Assembly-CSharp", true);
            var coordinator = coordinatorType.GetProperty("Instance").GetValue(null);
            Assert.NotNull(coordinator);
            var slotsField = runtimeType.GetField("quickSlots");
            var selectionField = runtimeType.GetField("selectedSlot");
            var ammoField = runtimeType.GetField("ammoInMagazine");
            var reserveField = runtimeType.GetField("reserveAmmo");
            var slots = (string[])slotsField.GetValue(runtime);
            var originalSlots = (string[])slots.Clone();
            int originalSelection = (int)selectionField.GetValue(runtime);
            int originalAmmo = (int)ammoField.GetValue(runtime);
            int originalReserve = (int)reserveField.GetValue(runtime);
            string id = "level04_test_" + Guid.NewGuid().ToString("N");
            try
            {
                runtimeType.GetMethod("AddItem").Invoke(runtime, new object[] { id, "Test loot", 1 });
                slots[0] = id;
                selectionField.SetValue(runtime, 0);
                ammoField.SetValue(runtime, 13);
                reserveField.SetValue(runtime, 37);
                object[] args = { "level01", null };
                Assert.IsTrue((bool)coordinatorType.GetMethod("TryLaunch").Invoke(coordinator, args), args[1]?.ToString());
                yield return null;
                Assert.AreEqual("WTF", SceneManager.GetActiveScene().name);
                Assert.AreSame(runtime, runtimeType.GetProperty("Instance").GetValue(null));
                Assert.IsTrue((bool)runtimeType.GetMethod("Has").Invoke(runtime, new object[] { id, 1 }));
                Assert.AreEqual(13, ammoField.GetValue(runtime));
                Assert.AreEqual(37, reserveField.GetValue(runtime));
                Assert.AreEqual(id, ((string[])slotsField.GetValue(runtime))[0]);
                Assert.AreEqual(0, selectionField.GetValue(runtime));
                args = new object[] { null };
                Assert.IsTrue((bool)coordinatorType.GetMethod("RestartCurrent").Invoke(coordinator, args), args[0]?.ToString());
                yield return null;
                Assert.AreSame(runtime, runtimeType.GetProperty("Instance").GetValue(null));
                Assert.IsTrue((bool)runtimeType.GetMethod("Has").Invoke(runtime, new object[] { id, 1 }));
                Assert.AreEqual(13, ammoField.GetValue(runtime));
                Assert.AreEqual(37, reserveField.GetValue(runtime));
                Assert.AreEqual(id, ((string[])slotsField.GetValue(runtime))[0]);
                Assert.AreEqual(0, selectionField.GetValue(runtime));
            }
            finally
            {
                runtimeType.GetMethod("Consume").Invoke(runtime, new object[] { id, 1 });
                Array.Copy(originalSlots, (string[])slotsField.GetValue(runtime), originalSlots.Length);
                selectionField.SetValue(runtime, originalSelection);
                ammoField.SetValue(runtime, originalAmmo);
                reserveField.SetValue(runtime, originalReserve);
            }
        }
    }
}
