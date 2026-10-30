using UnityEngine;

namespace C3TreasurePuzzle
{
    // M2 專用測試物件；不收集零件，也不修改解謎或通關狀態。
    public sealed class C3TestInteractable : C3Interactable
    {
        [SerializeField] private string displayName = "測試互動物件";
        private int interactionCount;

        public override string DisplayName => displayName;

        public override void Interact(GameObject actor)
        {
            interactionCount++;
            Debug.Log("M2 測試互動：" + displayName + "，次數 " + interactionCount, this);
        }
    }
}
