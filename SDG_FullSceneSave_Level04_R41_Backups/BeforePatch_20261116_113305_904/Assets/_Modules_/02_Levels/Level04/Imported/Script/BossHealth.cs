
using UnityEngine;

public class BossHealth : MonoBehaviour
{
    [Header("Boss血量")]
    public int maxHealth = 5;

    private int currentHealth;
    private bool isDead = false;
    public bool IsDead => isDead;
    public int CurrentHealth => currentHealth;
    public event System.Action Died;
    public event System.Action HealthChanged;

    [Header("Boss AI")]
    public ToiletMechBossAI bossAI;

    [Header("玩家回收控制")]
    public PlayerRecycleController playerRecycleController;

    [Header("死亡後出現的回收物品")]
    public GameObject recycleParts;

    void Start()
    {
        currentHealth = maxHealth;
        HealthChanged?.Invoke();

        if (bossAI == null)
            bossAI = GetComponent<ToiletMechBossAI>();

        if (recycleParts != null)
            recycleParts.SetActive(false);
    }

    // Restores a fresh scene AFTER Start() has initialized the boss. Does not award
    // duplicate damage, and never lets an already-dead boss enter its attack AI.
    public void RestoreFromSave(int savedHealth, bool savedDead, bool partsVisible)
    {
        CancelInvoke(nameof(ShowRecycleParts));
        currentHealth = Mathf.Clamp(savedHealth, 0, Mathf.Max(1, maxHealth));
        if (savedDead) currentHealth = 0;

        if (currentHealth == 0 && !isDead)
        {
            isDead = true;
            if (bossAI == null) bossAI = GetComponent<ToiletMechBossAI>();
            if (bossAI != null) bossAI.OnBossDeath();
            if (playerRecycleController != null) playerRecycleController.EnableRecycleMode();
            Died?.Invoke(); // Level04RecycleController and level HUD must receive this.
        }

        if (recycleParts != null)
        {
            recycleParts.SetActive(isDead && partsVisible);
            if (isDead && !partsVisible) Invoke(nameof(ShowRecycleParts), 3f);
        }
        HealthChanged?.Invoke();
    }

    public void TakeDamage(int damage)
    {
        if (isDead) return;

        currentHealth -= damage;
        currentHealth = Mathf.Max(currentHealth, 0);
        HealthChanged?.Invoke();

        Debug.Log("Boss 剩餘血量：" + currentHealth);

        if (currentHealth <= 0)
            Die();
    }

    void Die()
    {
        if (isDead) return;

        isDead = true;

        Debug.Log("Boss 已死亡！");

        // 通知Boss AI
        if (bossAI != null)
            bossAI.OnBossDeath();

        // 切換玩家操作模式
        if (playerRecycleController != null)
            playerRecycleController.EnableRecycleMode();
        else
            Debug.LogWarning("尚未指定 PlayerRecycleController！");

        Died?.Invoke();

        // 延遲出現回收物品
        if (recycleParts != null)
            Invoke(nameof(ShowRecycleParts), 3f);
    }

    void ShowRecycleParts()
    {
        if (recycleParts != null)
            recycleParts.SetActive(true);

        Debug.Log("Boss零件已出現！");
    }
}
