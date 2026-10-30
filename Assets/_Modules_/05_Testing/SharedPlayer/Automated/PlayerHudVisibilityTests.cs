using NUnit.Framework;
using SDGAdventure.SharedPlayer;
using UnityEngine;

namespace SDGAdventure.PlayerControlTests
{
    public class PlayerHudVisibilityTests
    {
        [Test]
        public void StatusPanelOnlyAppearsForContent()
        {
            var host = new GameObject("HudTest");
            try
            {
                var hud = host.AddComponent<PlayerHudCanvas>();
                hud.Build();
                var panel = host.transform.Find("PlayerHudCanvas/StatusPanel").gameObject;
                Assert.IsFalse(panel.activeSelf);
                hud.SetStatus("Player status");
                Assert.IsTrue(panel.activeSelf);
                Assert.AreEqual("Player status", hud.StatusText);
                hud.SetStatus("  ");
                Assert.IsFalse(panel.activeSelf);
                hud.SetStatus(null);
                Assert.IsFalse(panel.activeSelf);
            }
            finally { Object.DestroyImmediate(host); }
        }
    }
}
