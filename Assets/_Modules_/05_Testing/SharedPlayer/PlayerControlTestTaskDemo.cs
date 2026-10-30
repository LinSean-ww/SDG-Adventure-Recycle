using System.Collections.Generic;
using SDGAdventure.SharedPlayer;
using UnityEngine;

namespace SDGAdventure.PlayerTest
{
    // Sample data lives in the standalone test scene, not in the reusable player module.
    public sealed class PlayerControlTestTaskDemo : MonoBehaviour, IPlayerTaskSource
    {
        [SerializeField] private SharedFirstPersonController player;
        [SerializeField] private PlayerControlTestInteractable interactable;
        private PlayerTaskJournal journal;

        public void Configure(SharedFirstPersonController controller, PlayerControlTestInteractable target)
        {
            player = controller;
            interactable = target;
        }

        public IReadOnlyList<PlayerTaskItem> GetTasks() => new[]
        {
            new PlayerTaskItem("reach_tunnel", "找到黃色低矮通道", "走到前方黃色通道入口。"),
            new PlayerTaskItem("cross_tunnel", "蹲下穿過通道", "按住 Ctrl 蹲下，穿過整條通道。"),
            new PlayerTaskItem("interact", "與測試物件互動", "走近藍色物件並按 E。")
        };

        private void Start()
        {
            if (player == null) player = FindFirstObjectByType<SharedFirstPersonController>();
            if (interactable == null) interactable = FindFirstObjectByType<PlayerControlTestInteractable>();
            if (player == null) { enabled = false; return; }
            journal = player.GetComponent<PlayerTaskJournal>();
            if (journal == null) { enabled = false; return; }
            journal.Load(this);
        }

        private void Update()
        {
            if (journal == null) return;
            if (player.transform.position.z >= 1.8f) journal.SetCompleted("reach_tunnel");
            if (player.transform.position.z >= 8.5f) journal.SetCompleted("cross_tunnel");
            if (interactable != null && interactable.InteractionCount > 0)
                journal.SetCompleted("interact");
        }
    }
}
