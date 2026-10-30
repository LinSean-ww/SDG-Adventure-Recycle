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
        public int CollectedCount => collected.Count;
        public bool AllPartsCollected => collected.Contains(FuseId) && collected.Contains(ChipId) && collected.Contains(FilterId);
        public string Objective => AllPartsCollected ? "零件收集完成，請前往控制台" :
            !HasPart(FuseId) ? "線索：到 C 區西側尋寶標記附近找保險絲" :
            !HasPart(ChipId) ? "線索：控制晶片在 C 區二樓高架維修平台，從西側樓梯上去" :
            "線索：過濾模組位於工廠東北側過濾設備附近";
        public string LastFeedback { get; private set; } = "";

        public bool HasPart(string id) => collected.Contains(id);

        public bool TryCollect(string id, string displayName)
        {
            if (id != FuseId && id != ChipId && id != FilterId) return false;
            if (!collected.Add(id)) return false;
            LastFeedback = "已取得：" + displayName;
            if (EcoAdventureRuntime.Instance != null)
                EcoAdventureRuntime.Instance.AddItem(id, displayName);
            Changed?.Invoke();
            return true;
        }
    }
}
