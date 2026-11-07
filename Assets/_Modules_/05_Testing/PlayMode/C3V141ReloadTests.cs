using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
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
    }
}
