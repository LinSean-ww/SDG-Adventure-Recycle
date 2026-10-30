using System.Collections.Generic;
using SDGAdventure.SharedPlayer;
using UnityEngine;

namespace SDGAdventure.CompletePlayer
{
    public sealed class CompletePlayerDemoTasks : MonoBehaviour, IPlayerTaskSource
    {
        [SerializeField] private SharedFirstPersonController player;
        [SerializeField] private CompletePlayerDemoInteractable target;
        private PlayerTaskJournal journal;

        public void Configure(SharedFirstPersonController controller, CompletePlayerDemoInteractable interactable)
        {
            player = controller;
            target = interactable;
        }

        public IReadOnlyList<PlayerTaskItem> GetTasks() => new[]
        {
            new PlayerTaskItem("move", "移動到通道", "使用 WASD 移動到前方通道。"),
            new PlayerTaskItem("crouch", "穿過低矮通道", "按住 Ctrl 蹲下，穿過通道。"),
            new PlayerTaskItem("interact", "啟動測試裝置", "靠近裝置並按 E。")
        };

        private void Start()
        {
            if (!player) player = FindFirstObjectByType<SharedFirstPersonController>();
            if (!target) target = FindFirstObjectByType<CompletePlayerDemoInteractable>();
            journal = player ? player.GetComponent<PlayerTaskJournal>() : null;
            if (journal) journal.Load(this);
        }

        private void Update()
        {
            if (!journal || !player) return;
            if (player.transform.position.z >= 1.8f) journal.SetCompleted("move");
            if (player.transform.position.z >= 8.5f) journal.SetCompleted("crouch");
            if (target && target.InteractionCount > 0) journal.SetCompleted("interact");
        }
    }
}
