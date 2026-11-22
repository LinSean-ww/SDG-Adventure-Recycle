using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SDGAdventure.FrontEnd
{
    [Serializable]
    public sealed class SDGSaveOwnerV2
    {
        public string id;
        public string name;
        public string avatar;
        public int totalPlaySeconds;
        public int completedMask;
        public int xp;
        public int xpMax = 100;
        public int health = 100;
        public int healthMax = 100;
    }

    [Serializable]
    public sealed class SDGSaveOwnerCatalogV2
    {
        public int version = 1;
        public string activeId = "original";
        public List<SDGSaveOwnerV2> owners = new List<SDGSaveOwnerV2>();
    }

    /// <summary>Stable player identity; a portrait is display data, never a storage key.</summary>
    public static class SDGSaveProfilesV2
    {
        private static SDGSaveOwnerCatalogV2 catalog;
        private static readonly object Gate = new object();
        private static string CatalogPath { get { return Path.Combine(Application.persistentDataPath, "SDG_SaveOwners_v2.json"); } }
        private static string SaveDirectory { get { return Path.Combine(Application.persistentDataPath, "SDG_SaveOwners_v2"); } }
        public static string ActiveId { get { Ensure(); return catalog.activeId; } }
        public static List<SDGSaveOwnerV2> Owners { get { Ensure(); return new List<SDGSaveOwnerV2>(catalog.owners); } }
        public static SDGSaveOwnerV2 Active { get { Ensure(); return Find(catalog.activeId); } }

        private static SDGSaveOwnerV2 Find(string id)
        {
            return catalog.owners.Find(p => p != null && p.id == id);
        }

        public static void Ensure()
        {
            if (catalog != null) return;
            lock (Gate)
            {
                if (catalog != null) return;
                try
                {
                    if (File.Exists(CatalogPath))
                    {
                        var info = new FileInfo(CatalogPath);
                        if (info.Length > 0 && info.Length < 1024 * 1024)
                            catalog = JsonUtility.FromJson<SDGSaveOwnerCatalogV2>(File.ReadAllText(CatalogPath));
                    }
                }
                catch (Exception ex) { Debug.LogWarning("[SDG Save Owners] Read: " + ex.Message); }
                if (catalog == null) catalog = new SDGSaveOwnerCatalogV2();
                if (catalog.owners == null) catalog.owners = new List<SDGSaveOwnerV2>();
                if (catalog.owners.Count == 0)
                {
                    // Original identity exclusively owns historical unscoped v2 save files.
                    catalog.owners.Add(new SDGSaveOwnerV2
                    {
                        id = "original", name = PlayerPrefs.GetString("SDG_FE_PlayerName", "回收探險者"),
                        avatar = PlayerPrefs.GetString("SDG_FE_SelectedAvatar", "builtin:0"),
                        totalPlaySeconds = Mathf.Max(0, PlayerPrefs.GetInt(SDGSaveProgressRuntimeV1.SecondsPref, 0)),
                        completedMask = PlayerPrefs.GetInt(SDGSaveProgressRuntimeV1.CompletedPref, 0) & 15,
                        xp = PlayerPrefs.GetInt("SDG_FE_Experience", 0), xpMax = Mathf.Max(1, PlayerPrefs.GetInt("SDG_FE_ExperienceMax", 100)),
                        health = PlayerPrefs.GetInt("SDG_FE_PlayerHealth", 100),
                        healthMax = Mathf.Max(1, PlayerPrefs.GetInt("SDG_FE_PlayerHealthMax", 100))
                    });
                    catalog.activeId = "original";
                    Write();
                }
                if (Find(catalog.activeId) == null) { catalog.activeId = catalog.owners[0].id; Write(); }
            }
        }

        public static void CaptureActive(int seconds, int completed)
        {
            Ensure();
            var p = Active;
            if (p == null) return;
            p.name = PlayerPrefs.GetString("SDG_FE_PlayerName", p.name);
            p.avatar = PlayerPrefs.GetString("SDG_FE_SelectedAvatar", p.avatar);
            p.totalPlaySeconds = Mathf.Max(0, seconds);
            p.completedMask = completed & 15;
            p.xp = Mathf.Max(0, PlayerPrefs.GetInt("SDG_FE_Experience", 0));
            p.xpMax = Mathf.Max(1, PlayerPrefs.GetInt("SDG_FE_ExperienceMax", 100));
            p.health = Mathf.Max(0, PlayerPrefs.GetInt("SDG_FE_PlayerHealth", 100));
            p.healthMax = Mathf.Max(1, PlayerPrefs.GetInt("SDG_FE_PlayerHealthMax", 100));
            Write();
        }

        public static SDGSaveOwnerV2 CreateFromCurrentPlayer()
        {
            Ensure();
            if (catalog.owners.Count >= 16) return null;
            var owner = new SDGSaveOwnerV2
            {
                id = Guid.NewGuid().ToString("N"),
                name = PlayerPrefs.GetString("SDG_FE_PlayerName", "回收探險者"),
                avatar = PlayerPrefs.GetString("SDG_FE_SelectedAvatar", "builtin:0")
            };
            catalog.owners.Add(owner);
            Write();
            return owner;
        }

        public static bool SetActive(string id)
        {
            Ensure();
            var p = Find(id);
            if (p == null) return false;
            catalog.activeId = p.id;
            PlayerPrefs.SetString("SDG_FE_PlayerName", p.name ?? "回收探險者");
            PlayerPrefs.SetString("SDG_FE_SelectedAvatar", p.avatar ?? "builtin:0");
            PlayerPrefs.SetInt("SDG_FE_Experience", p.xp);
            PlayerPrefs.SetInt("SDG_FE_ExperienceMax", Mathf.Max(1, p.xpMax));
            PlayerPrefs.SetInt("SDG_FE_PlayerHealth", p.health);
            PlayerPrefs.SetInt("SDG_FE_PlayerHealthMax", Mathf.Max(1, p.healthMax));
            PlayerPrefs.SetInt(SDGSaveProgressRuntimeV1.SecondsPref, p.totalPlaySeconds);
            PlayerPrefs.SetInt(SDGSaveProgressRuntimeV1.CompletedPref, p.completedMask & 15);
            PlayerPrefs.Save();
            Write();
            return true;
        }

        public static string SlotPath(int slot)
        {
            Ensure();
            string name = slot == 0 ? "Auto" : "Manual" + slot;
            // Backwards compatibility: old saves remain available under the original player only.
            if (ActiveId == "original")
                return Path.Combine(Application.persistentDataPath, "SDG_Progress_v2_" + name + ".json");
            return Path.Combine(SaveDirectory, ActiveId + "_" + name + ".json");
        }

        public static bool DeleteSlot(int slot, out string message)
        {
            message = "";
            if (slot < 0 || slot > 12) { message = "無效欄位"; return false; }
            string path = SlotPath(slot);
            if (!File.Exists(path)) { message = "此欄位沒有存檔"; return false; }
            try
            {
                string trash = Path.Combine(Application.persistentDataPath, "SDG_SaveDeletedBackups");
                Directory.CreateDirectory(trash);
                File.Move(path, Path.Combine(trash, Path.GetFileName(path) + "." + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + ".bak"));
                message = "已移至存檔備份資料夾";
                return true;
            }
            catch (Exception ex) { message = "刪除失敗：" + ex.Message; return false; }
        }

        /// <summary>
        /// Archives a non-original player together with every manual/auto save before
        /// removing the player from the catalog. No permanent file deletion occurs.
        /// The active player must be switched away first by the calling UI.
        /// </summary>
        public static bool ArchiveOwner(string ownerId, out string message)
        {
            message = "";
            Ensure();
            if (string.IsNullOrEmpty(ownerId) || ownerId == "original")
            {
                message = "原始玩家及歷史進度受保護";
                return false;
            }
            if (ownerId == ActiveId)
            {
                message = "請先切換至其他玩家";
                return false;
            }
            // Only IDs created by this save system may be used as file-name components.
            if (ownerId.Length != 32 || !System.Text.RegularExpressions.Regex.IsMatch(ownerId, "\\A[0-9a-fA-F]{32}\\z"))
            {
                message = "玩家 ID 格式異常，已拒絕封存";
                return false;
            }
            SDGSaveOwnerV2 owner = Find(ownerId);
            if (owner == null) { message = "找不到這位玩家"; return false; }
            if (catalog.owners.Count <= 1) { message = "至少必須保留一位玩家"; return false; }

            string archive = Path.Combine(Application.persistentDataPath, "SDG_ArchivedPlayers_v2");
            archive = Path.Combine(archive, ownerId + "_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff"));
            List<string> moved = new List<string>();
            int oldIndex = catalog.owners.IndexOf(owner);
            try
            {
                Directory.CreateDirectory(archive);
                if (File.Exists(CatalogPath)) File.Copy(CatalogPath, Path.Combine(archive, "OwnerCatalog.before.json"), true);
                foreach (string suffix in new[] { "Auto", "Manual1", "Manual2", "Manual3", "Manual4", "Manual5", "Manual6",
                                                  "Manual7", "Manual8", "Manual9", "Manual10", "Manual11", "Manual12" })
                {
                    string file = ownerId + "_" + suffix + ".json";
                    string source = Path.Combine(SaveDirectory, file);
                    if (!File.Exists(source)) continue;
                    File.Move(source, Path.Combine(archive, file));
                    moved.Add(file);
                }
                catalog.owners.RemoveAt(oldIndex);
                Write();
                // Verify the file on disk: Write() reports filesystem exceptions by log.
                SDGSaveOwnerCatalogV2 verify = JsonUtility.FromJson<SDGSaveOwnerCatalogV2>(File.ReadAllText(CatalogPath));
                if (verify == null || verify.owners == null || verify.owners.Exists(p => p != null && p.id == ownerId))
                    throw new IOException("儲存玩家名單失敗");
                message = "玩家及 " + moved.Count + " 筆存檔已封存，可於 SDG_ArchivedPlayers_v2 找回";
                return true;
            }
            catch (Exception ex)
            {
                if (Find(ownerId) == null) catalog.owners.Insert(Mathf.Clamp(oldIndex, 0, catalog.owners.Count), owner);
                try { Write(); } catch (Exception) { }
                foreach (string file in moved)
                {
                    try
                    {
                        string from = Path.Combine(archive, file);
                        string to = Path.Combine(SaveDirectory, file);
                        if (File.Exists(from) && !File.Exists(to)) File.Move(from, to);
                    }
                    catch (Exception rollbackEx) { Debug.LogError("[SDG Owners] Restore save: " + rollbackEx.Message); }
                }
                message = "封存失敗，已嘗試還原存檔：" + ex.Message;
                return false;
            }
        }

        private static void Write()
        {
            try
            {
                Directory.CreateDirectory(Application.persistentDataPath);
                string temp = CatalogPath + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(catalog, true), new System.Text.UTF8Encoding(false));
                if (!File.Exists(CatalogPath)) File.Move(temp, CatalogPath);
                else
                {
                    try { File.Replace(temp, CatalogPath, CatalogPath + ".bak"); }
                    catch (PlatformNotSupportedException)
                    {
                        File.Copy(CatalogPath, CatalogPath + ".bak", true);
                        File.Delete(CatalogPath);
                        File.Move(temp, CatalogPath);
                    }
                }
            }
            catch (Exception ex) { Debug.LogError("[SDG Save Owners] Write failed: " + ex); }
        }
    }
}
