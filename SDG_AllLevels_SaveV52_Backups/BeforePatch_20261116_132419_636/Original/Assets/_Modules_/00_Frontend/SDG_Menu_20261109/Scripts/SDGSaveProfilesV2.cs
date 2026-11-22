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
            if (slot < 0 || slot > 3) { message = "無效欄位"; return false; }
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
