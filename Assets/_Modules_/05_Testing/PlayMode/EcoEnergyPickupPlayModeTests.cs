using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SDGAdventure.Testing
{
    // Uses reflection: tests have their own assembly, game classes are in Assembly-CSharp.
    public sealed class EcoEnergyPickupPlayModeTests
    {
        private const string ScenePath =
            "Assets/_Modules_/02_Levels/Level03/Scenes/C_Main.unity";
        private const string GunId = "eco_quantum_projector";
        private static Type RuntimeType => Type.GetType("EcoAdventureRuntime, Assembly-CSharp", true);
        private static Type PickupType => Type.GetType("C3TreasurePuzzle.EcoEnergyPickup, Assembly-CSharp", true);
        private static object Runtime => RuntimeType.GetProperty("Instance").GetValue(null);
        private static FieldInfo MagazineField => RuntimeType.GetField("ammoInMagazine");
        private static FieldInfo ReserveField => RuntimeType.GetField("reserveAmmo");

        private static bool HasGun(object game) => (bool)RuntimeType.GetMethod("Has")
            .Invoke(game, new object[] { GunId, 1 });

        private sealed class EquipmentScope : IDisposable
        {
            private readonly object game;
            private readonly string[] hotbar;
            private readonly string[] originalHotbar;
            private readonly int selected;
            private readonly bool addedGun;

            public EquipmentScope(bool equipped)
            {
                game = Runtime;
                hotbar = (string[])RuntimeType.GetField("quickSlots").GetValue(game);
                originalHotbar = (string[])hotbar.Clone();
                selected = (int)RuntimeType.GetField("selectedSlot").GetValue(game);
                if (equipped && !HasGun(game))
                {
                    RuntimeType.GetMethod("AddItem")
                        .Invoke(game, new object[] { GunId, "Test Quantum Projector", 1 });
                    addedGun = true;
                }
                hotbar[0] = equipped ? GunId : null;
                RuntimeType.GetField("selectedSlot").SetValue(game, 0);
            }

            public void Dispose()
            {
                Array.Copy(originalHotbar, hotbar, hotbar.Length);
                RuntimeType.GetField("selectedSlot").SetValue(game, selected);
                if (addedGun)
                    RuntimeType.GetMethod("Consume")
                        .Invoke(game, new object[] { GunId, 1 });
            }
        }

        private static Component Pickup(string name) =>
            GameObject.Find(name).GetComponent(PickupType) as Component;

        private static bool TryCollect(Component pickup) =>
            (bool)PickupType.GetMethod("TryCollect").Invoke(pickup, null);

        [UnityTest]
        public IEnumerator NineEnergyItemsAreScatteredAndMaterialsAreSeparated()
        {
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            yield return null;
            var pickups = UnityEngine.Object.FindObjectsOfType(PickupType).Cast<Component>().ToArray();
            Assert.AreEqual(9, pickups.Length, "Nine one-time energy props should spawn");
            var valueCounts = pickups.Select(p => (int)PickupType.GetProperty("EnergyValue").GetValue(p))
                .GroupBy(n => n).ToDictionary(g => g.Key, g => g.Count());
            Assert.AreEqual(3, valueCounts[5]);
            Assert.AreEqual(3, valueCounts[8]);
            Assert.AreEqual(3, valueCounts[3]);
            float spreadX = pickups.Max(p => p.transform.position.x) - pickups.Min(p => p.transform.position.x);
            float spreadZ = pickups.Max(p => p.transform.position.z) - pickups.Min(p => p.transform.position.z);
            Assert.Greater(spreadX, 12f, "Pickups must span the factory width");
            Assert.Greater(spreadZ, 12f, "Pickups must span the factory length");
            var metal = GameObject.Find("Distributed_RecycledMetal_F");
            var plastic = GameObject.Find("Distributed_RecycledPlastic_F");
            var circuit = GameObject.Find("Hidden_ElectronicScrapCache_F");
            Assert.IsNotNull(metal);
            Assert.IsNotNull(plastic);
            Assert.IsNotNull(circuit);
            Assert.Greater(Vector3.Distance(metal.transform.position, plastic.transform.position), 8f);
            Assert.Greater(Vector3.Distance(circuit.transform.position, metal.transform.position), 8f);
            Assert.IsNotNull(GameObject.Find("WasteSortingLine"));
        }

        [UnityTest]
        public IEnumerator UnarmedPlayerCannotRechargeButEquippedPlayerCan()
        {
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            yield return null;
            object game = Runtime;
            int oldLoaded = (int)MagazineField.GetValue(game);
            int oldReserve = (int)ReserveField.GetValue(game);
            Type controllerType = Type.GetType(
                "C3TreasurePuzzle.C3FirstPersonController, Assembly-CSharp", true);
            Component actor = UnityEngine.Object.FindObjectOfType(controllerType) as Component;
            Assert.IsNotNull(actor);
            var prop = new GameObject("Pickup_EquipmentGate_Test");
            prop.transform.position = actor.transform.position + actor.transform.right * 0.65f;
            prop.AddComponent<BoxCollider>().isTrigger = true;
            Component pickup = prop.AddComponent(PickupType) as Component;
            PickupType.GetMethod("Configure").Invoke(pickup,
                new object[] { Enum.Parse(PickupType.GetNestedType("SalvageKind"), "PlasticBottle") });
            try
            {
                MagazineField.SetValue(game, 10);
                ReserveField.SetValue(game, 50);
                using (new EquipmentScope(false))
                {
                    yield return null;
                    yield return null;
                    Assert.AreEqual(10, (int)MagazineField.GetValue(game),
                        "Walking over salvage without the gun must not increase loaded energy");
                    Assert.AreEqual(50, (int)ReserveField.GetValue(game));
                    Assert.IsTrue(prop.activeSelf);
                    Assert.IsFalse(TryCollect(pickup), "Manual interact must also respect the equipment gate");
                }
                using (new EquipmentScope(true))
                {
                    yield return null;
                    yield return null;
                    Assert.AreEqual(15, (int)MagazineField.GetValue(game));
                    Assert.AreEqual(50, (int)ReserveField.GetValue(game),
                        "Salvage must leave the reserve energy unchanged");
                    Assert.IsFalse(prop.activeSelf, "Consumed item should disappear");
                    Assert.IsFalse(TryCollect(pickup), "Same prop cannot award energy twice");
                }
            }
            finally
            {
                MagazineField.SetValue(game, oldLoaded);
                ReserveField.SetValue(game, oldReserve);
                UnityEngine.Object.Destroy(prop);
            }
        }

        [UnityTest]
        public IEnumerator FullMagazineNeverConsumesPickupEvenWithWeaponEquipped()
        {
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            yield return null;
            object game = Runtime;
            int oldLoaded = (int)MagazineField.GetValue(game);
            int oldReserve = (int)ReserveField.GetValue(game);
            Component pickup = Pickup("EnergySalvage_MetalCan_1");
            Assert.IsNotNull(pickup);
            using (new EquipmentScope(true))
            {
                try
                {
                    MagazineField.SetValue(game, 24);
                    ReserveField.SetValue(game, 93);
                    Assert.IsFalse(TryCollect(pickup));
                    Assert.IsTrue(pickup.gameObject.activeSelf,
                        "At max loaded energy, the pickup must remain for later");
                    MagazineField.SetValue(game, 21);
                    Assert.IsTrue(TryCollect(pickup));
                    Assert.AreEqual(24, (int)MagazineField.GetValue(game),
                        "Energy must be clamped to the 24-unit magazine capacity");
                    Assert.AreEqual(93, (int)ReserveField.GetValue(game),
                        "Salvage must not modify reserve energy");
                    Assert.IsFalse(pickup.gameObject.activeSelf);
                }
                finally
                {
                    MagazineField.SetValue(game, oldLoaded);
                    ReserveField.SetValue(game, oldReserve);
                }
            }
        }

        [UnityTest]
        public IEnumerator QuestionOptionsChangeBetweenLevelEntries()
        {
            const string previousOrderKey = "SDG_C3_LastPuzzleOptionOrder";
            bool hadPreviousOrder = PlayerPrefs.HasKey(previousOrderKey);
            string savedPreviousOrder = PlayerPrefs.GetString(previousOrderKey);
            Type viewType = Type.GetType("C3TreasurePuzzle.C3UiView, Assembly-CSharp", true);
            MethodInfo option = viewType.GetMethod("GetOptionAt");
            string previous = null;
            try
            {
                for (int load = 0; load < 3; load++)
                {
                    yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
                    yield return null;
                    Component view = UnityEngine.Object.FindObjectOfType(viewType) as Component;
                    Assert.IsNotNull(view);
                    string order = string.Join("", Enumerable.Range(0, 3)
                        .Select(i => Convert.ToInt32(option.Invoke(view, new object[] { i })).ToString()).ToArray());
                    Assert.AreEqual(3, order.Distinct().Count(), "All three steps must appear once");
                    if (previous != null) Assert.AreNotEqual(previous, order,
                        "Re-entering Level03 must change the displayed option order");
                    previous = order;
                }
            }
            finally
            {
                if (hadPreviousOrder) PlayerPrefs.SetString(previousOrderKey, savedPreviousOrder);
                else PlayerPrefs.DeleteKey(previousOrderKey);
                PlayerPrefs.Save();
            }
        }
    }
}
