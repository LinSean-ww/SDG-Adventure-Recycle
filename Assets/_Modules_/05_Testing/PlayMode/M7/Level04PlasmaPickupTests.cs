using System;
using System.Collections;
using NUnit.Framework;
using SDGAdventure.SharedPlayer;
using UnityEngine;
using UnityEngine.TestTools;

namespace M7SharedPlayerTests
{
    public sealed class Level04PlasmaPickupTests
    {
        [UnityTest]
        public IEnumerator PickupUsesSharedInventoryAndRespectsInputContext()
        {
            yield return null;
            var input = PlayerInputService.Instance;
            Assert.NotNull(input);
            var runtimeType = Type.GetType("EcoAdventureRuntime, Assembly-CSharp", true);
            var pickupType = Type.GetType("SDGAdventure.Level04.Level04PlasmaPickup, Assembly-CSharp", true);
            var inventory = runtimeType.GetProperty("Instance").GetValue(null);
            Assert.NotNull(inventory);
            var has = runtimeType.GetMethod("Has");
            var consume = runtimeType.GetMethod("Consume");
            const string id = "level04_plasma_gun";
            Assert.IsFalse((bool)has.Invoke(inventory, new object[] { id, 1 }), "Test requires no pre-existing plasma gun");
            var priorContext = input.Context;
            var actor = new GameObject("Level04TestActor");
            var worldObject = new GameObject("Level04TestPickup");
            var pickup = worldObject.AddComponent(pickupType);
            var target = (IPlayerInteractable)pickup;
            Func<bool> blocker = () => true;
            try
            {
                input.SetContext(PlayerInputContext.UI);
                Assert.IsFalse(target.CanInteract(actor));
                target.Interact(actor);
                Assert.IsFalse((bool)has.Invoke(inventory, new object[] { id, 1 }));
                input.SetContext(PlayerInputContext.Gameplay);
                input.SetGameplayActionBlocker(blocker);
                Assert.IsFalse(target.CanInteract(actor));
                input.ClearGameplayActionBlocker(blocker);
                Assert.IsTrue(target.CanInteract(actor));
                target.Interact(actor);
                Assert.IsTrue((bool)has.Invoke(inventory, new object[] { id, 1 }));
                Assert.IsFalse(worldObject.activeSelf);
                target.Interact(actor);
                Assert.IsFalse((bool)has.Invoke(inventory, new object[] { id, 2 }), "Repeated interactions must not duplicate the weapon");
            }
            finally
            {
                input.ClearGameplayActionBlocker(blocker);
                input.SetContext(priorContext);
                if ((bool)has.Invoke(inventory, new object[] { id, 1 })) consume.Invoke(inventory, new object[] { id, 1 });
                UnityEngine.Object.DestroyImmediate(actor);
                UnityEngine.Object.DestroyImmediate(worldObject);
            }
        }
    }
}
