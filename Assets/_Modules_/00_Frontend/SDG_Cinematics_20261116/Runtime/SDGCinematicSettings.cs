using System;
using System.IO;
using UnityEngine;

namespace SDG.Cinematics
{
    [Serializable]
    public sealed class SDGCinematicLevel
    {
        public int levelNumber;
        public string scenePath;
        public string clipFile;
    }

    [Serializable]
    public sealed class SDGCinematicSettings
    {
        public bool enableStartup = true;
        public bool enableLevels = true;
        public bool skipWithSpace = true;
        public string[] startupFiles = { "startup.mp4", "storyboard_01.mp4", "storyboard_02.mp4" };
        public SDGCinematicLevel[] levels =
        {
            new SDGCinematicLevel { levelNumber = 1, scenePath = "", clipFile = "storyboard_03.mp4" },
            new SDGCinematicLevel { levelNumber = 2, scenePath = "", clipFile = "storyboard_04.mp4" },
            new SDGCinematicLevel { levelNumber = 3, scenePath = "", clipFile = "storyboard_05.mp4" },
            new SDGCinematicLevel { levelNumber = 4, scenePath = "", clipFile = "storyboard_06.mp4" }
        };

        public static string RootDirectory
        {
            get { return Path.Combine(Application.streamingAssetsPath, "SDGCinematics"); }
        }

        public static string ConfigPath
        {
            get { return Path.Combine(RootDirectory, "config.json"); }
        }

        public static SDGCinematicSettings Load()
        {
            try
            {
                if (!File.Exists(ConfigPath))
                {
                    Debug.LogWarning("[SDG Cinematics] Missing config: " + ConfigPath);
                    return null;
                }
                string json = File.ReadAllText(ConfigPath);
                SDGCinematicSettings result = JsonUtility.FromJson<SDGCinematicSettings>(json);
                return result;
            }
            catch (Exception ex)
            {
                Debug.LogError("[SDG Cinematics] Invalid config: " + ex.Message);
                return null;
            }
        }
    }
}
