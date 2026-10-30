using SDGAdventure.SharedPlayer;
using UnityEngine;

namespace SDGAdventure.PlayerTest
{
    public sealed class PlayerControlTestHud : MonoBehaviour
    {
        [SerializeField] private SharedFirstPersonController player;
        [SerializeField] private PlayerControlTestInteractable interactable;
        private PlayerHudCanvas hud;

        public void Configure(SharedFirstPersonController controller, PlayerControlTestInteractable target)
        {
            player = controller;
            interactable = target;
        }

        private void LateUpdate()
        {
            PlayerInputService input = PlayerInputService.Instance;
            if (player == null || input == null) return;
            if (hud == null) hud = player.GetComponent<PlayerHudCanvas>();
            if (hud == null) return;
            string jump = input.Keybindings.BindingDisplay("Gameplay", "Jump", 0);
            string move = input.Keybindings.BindingDisplay("Gameplay", "Move", 1) + "/" +
                input.Keybindings.BindingDisplay("Gameplay", "Move", 3) + "/" +
                input.Keybindings.BindingDisplay("Gameplay", "Move", 2) + "/" +
                input.Keybindings.BindingDisplay("Gameplay", "Move", 4);
            string sprint = input.Keybindings.BindingDisplay("Gameplay", "Sprint", 0);
            string crouch = input.Keybindings.BindingDisplay("Gameplay", "Crouch", 0);
            string interact = input.Keybindings.BindingDisplay("Gameplay", "Interact", 0);
            string tasks = input.Keybindings.BindingDisplay("Gameplay", "TaskList", 0);
            string pause = input.Keybindings.BindingDisplay("Global", "Pause", 0);
            string text = "共用第一人稱控制測試｜遊玩中\n" +
                move + " 移動　滑鼠視角　" + jump + " 跳躍\n" +
                sprint + " 奔跑　" + crouch + " 蹲下　" + interact + " 互動\n" +
                tasks + " 任務　" + pause + " 暫停\n" +
                "前方低通道需蹲下通過；右側平台可測試跳躍。\n" +
                "角色高度：" + player.CurrentHeight.ToString("0.00") +
                "　姿態：" + (player.IsCrouching ? "蹲下" : "站立") +
                "　速度：" + player.CurrentSpeed.ToString("0.0") + "\n" +
                (interactable == null ? "" : interactable.LastFeedback);
            hud.SetStatus(text);
        }
    }
}
