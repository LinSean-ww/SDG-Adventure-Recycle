using UnityEngine;

public class ModuleAdapter : MonoBehaviour
{
    public void InitializeModule()
    {
        // TEST 專案目前不需要額外初始化
    }

    public void StartModule()
    {
        // TEST 專案進入場景後即可直接操作
    }

    public void PauseModule()
    {
        Time.timeScale = 0f;
    }

    public void ResumeModule()
    {
        Time.timeScale = 1f;
    }

    public void ExitModule()
    {
        // 目前先不處理返回母專案
    }

    public int GetResult()
    {
        // TEST 專案沒有分數系統
        return 0;
    }
}