using System;
using System.Collections.Generic;
using UnityEngine;

namespace C3TreasurePuzzle
{
    // Scene-owned state: loading C_Main creates a fresh manager; player respawn keeps it.
    public sealed class C3TreasureProgress : MonoBehaviour
    {
        public const string FuseId = "fuse";
        public const string ChipId = "control_chip";
        public const string FilterId = "filter_module";
        public const int RequiredCount = 3;
        private readonly HashSet<string> collected = new HashSet<string>();
        public event Action Changed;
        public bool PartsSubmitted { get; private set; }
        public int CollectedCount => collected.Count;
        public bool AllPartsCollected => collected.Contains(FuseId) && collected.Contains(ChipId) && collected.Contains(FilterId);
        public string Objective => AllPartsCollected ? "零件收集完成，請前往控制台" :
            !HasPart(FuseId) ? "線索：到 C 區西側尋寶標記附近找保險絲" :
            !HasPart(ChipId) ? "線索：由西側樓梯 A 上二樓，沿巡檢平台到後方維修工作桌取得控制晶片；東側樓梯 B 可返回一樓" :
            "線索：過濾模組位於工廠東北側過濾設備附近";
        public string LastFeedback { get; private set; } = "";

        public bool HasPart(string id) => collected.Contains(id);

        // Collection is a historical quest receipt; submission is a separate,
        // once-per-scene transition and requires the three items actually held.
        public bool TrySubmitParts(EcoAdventureRuntime inventoryOwner)
        {
            if (PartsSubmitted) return true;
            if (!AllPartsCollected || !inventoryOwner) return false;
            var sceneParts = new List<C3CollectiblePart>();
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
                foreach (C3CollectiblePart part in root.GetComponentsInChildren<C3CollectiblePart>(true))
                    if (part.PartId == FuseId || part.PartId == ChipId || part.PartId == FilterId)
                        sceneParts.Add(part);
            if (!inventoryOwner.ConsumeSubmittedParts()) return false;
            PartsSubmitted = true;
            foreach (C3CollectiblePart part in sceneParts) if (part) part.MarkSubmitted();
            inventoryOwner.RefreshInventoryPresentation();
            LastFeedback = "已提交三種任務零件";
            Changed?.Invoke();
            return true;
        }

        public bool TryCollect(string id, string displayName)
        {
            if (PartsSubmitted || (id != FuseId && id != ChipId && id != FilterId)) return false;
            if (!collected.Add(id)) return false;
            LastFeedback = "已取得：" + displayName;
            if (EcoAdventureRuntime.Instance != null)
                EcoAdventureRuntime.Instance.AddItem(id, displayName);
            Changed?.Invoke();
            return true;
        }
    }
}
