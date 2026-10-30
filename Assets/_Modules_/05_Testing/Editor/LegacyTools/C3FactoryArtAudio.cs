using System;
using System.IO;
using System.Linq;
using System.Text;
using C3TreasurePuzzle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class C3FactoryArtAudio
{
    private const string ScenePath = "Assets/_Modules_/02_Levels/Level03/Scenes/C_Main.unity";
    private const string AudioPath = "Assets/_Modules_/02_Levels/Level03/ArtFull/Audio";
    private static readonly string EvidencePath = SDGAdventure.Testing.ReportOutputPaths.Resolve("C3_FullArt_Evidence");

    public static void StageF_OriginalSoundscape()
    {
        Directory.CreateDirectory(AudioPath);
        MakeWav("A_DormantFactoryHum", 8f, AmbientSample);
        MakeWav("A_PurifierActivation", 3f, ActivationSample);
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Transform audio = scene.GetRootGameObjects().Single(go => go.name == "C_Factory_Art").transform.Find("Audio");
        if (audio.childCount != 0) throw new Exception("Audio nodes already exist; refusing duplicate construction.");
        C3DeviceState device = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<C3DeviceState>(true)).Single();
        GameObject hum = new GameObject("DormantFactoryAmbientHum");
        hum.transform.SetParent(audio, false);
        hum.transform.position = new Vector3(-12.5f, 2.8f, 12.5f);
        AudioSource humSource = hum.AddComponent<AudioSource>();
        humSource.clip = Clip("A_DormantFactoryHum");
        humSource.playOnAwake = true;
        humSource.loop = true;
        humSource.volume = 0.12f;
        humSource.spatialBlend = 0f;
        GameObject activation = new GameObject("PurifierStartupSound");
        activation.transform.SetParent(audio, false);
        activation.transform.position = new Vector3(-12.5f, 2.15f, 9.5f);
        AudioSource startup = activation.AddComponent<AudioSource>();
        startup.clip = Clip("A_PurifierActivation");
        startup.playOnAwake = false;
        startup.loop = false;
        startup.volume = 0.5f;
        startup.spatialBlend = 0.72f;
        startup.minDistance = 3f;
        startup.maxDistance = 16f;
        activation.AddComponent<C3FactoryAudioCue>().Configure(device);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Failed to save factory audio stage.");
        ValidateStageF();
        Debug.Log("C3_FULLART_STAGE_F_PASS");
    }

    public static void ValidateStageF()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Transform audio = scene.GetRootGameObjects().Single(go => go.name == "C_Factory_Art").transform.Find("Audio");
        AudioSource[] sources = audio.GetComponentsInChildren<AudioSource>(true);
        if (sources.Length != 2 || sources.Any(source => source.clip == null))
            throw new Exception("Factory audio node or clip missing.");
        if (audio.GetComponentsInChildren<C3FactoryAudioCue>(true).Length != 1)
            throw new Exception("Activation cue hook missing.");
        Directory.CreateDirectory(EvidencePath);
        File.WriteAllText(EvidencePath + "/StageF_Audio.txt",
            "source=original procedural synthesis by this project\nambientClip=" + sources.Single(s => s.loop).clip.name +
            "\nactivationClip=" + sources.Single(s => !s.loop).clip.name + "\nactivationHook=C3DeviceState.Activated\n",
            new UTF8Encoding(false));
        Debug.Log("C3_FULLART_STAGE_F_VERIFY_PASS");
    }

    private static AudioClip Clip(string name)
    {
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioPath + "/" + name + ".wav");
        if (clip == null) throw new Exception("Missing generated audio clip " + name);
        return clip;
    }

    private static void MakeWav(string name, float seconds, Func<float, float> sample)
    {
        string path = AudioPath + "/" + name + ".wav";
        if (File.Exists(path)) throw new Exception("Refusing to overwrite audio asset: " + path);
        const int sampleRate = 22050;
        int count = Mathf.RoundToInt(seconds * sampleRate);
        using (FileStream file = new FileStream(path, FileMode.CreateNew))
        using (BinaryWriter writer = new BinaryWriter(file))
        {
            int bytes = count * 2;
            writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + bytes);
            writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
            writer.Write((short)1); writer.Write((short)1); writer.Write(sampleRate);
            writer.Write(sampleRate * 2); writer.Write((short)2); writer.Write((short)16);
            writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(bytes);
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)sampleRate;
                writer.Write((short)Mathf.RoundToInt(Mathf.Clamp(sample(t), -1f, 1f) * 32767f));
            }
        }
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
    }

    private static float AmbientSample(float t)
    {
        float beat = Mathf.Sin(2f * Mathf.PI * 0.75f * t) * 0.045f;
        float baseHum = Mathf.Sin(2f * Mathf.PI * 54f * t) * 0.31f +
            Mathf.Sin(2f * Mathf.PI * 108f * t) * 0.12f +
            Mathf.Sin(2f * Mathf.PI * 27f * t) * 0.08f;
        float rattle = Mathf.Sin(2f * Mathf.PI * (8f + beat) * t) * Mathf.Sin(2f * Mathf.PI * 713f * t) * 0.025f;
        return (baseHum + rattle) * (0.60f + 0.12f * Mathf.Sin(2f * Mathf.PI * 0.125f * t));
    }

    private static float ActivationSample(float t)
    {
        float rise = Mathf.Clamp01(t * 2.2f);
        float decay = Mathf.Exp(-t * 1.30f);
        float sweep = Mathf.Sin(2f * Mathf.PI * (78f * t + 53f * t * t)) * 0.34f;
        float power = Mathf.Sin(2f * Mathf.PI * 53f * t) * 0.22f;
        float bell = Mathf.Sin(2f * Mathf.PI * 486f * t) * Mathf.Exp(-t * 2.9f) * 0.15f;
        return (sweep + power + bell) * rise * decay;
    }
}
