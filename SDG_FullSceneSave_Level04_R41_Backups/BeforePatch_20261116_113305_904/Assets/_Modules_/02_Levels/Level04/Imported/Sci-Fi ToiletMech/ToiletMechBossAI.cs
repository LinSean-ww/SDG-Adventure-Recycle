
using UnityEngine;
using UnityEngine.AI;

public class ToiletMechBossAI : MonoBehaviour
{
    // Boss目前的狀態
    private enum BossPhase
    {
        Dormant,    // 地底休眠
        Emerging,   // 從地底爬出
        Roaring,    // 吼叫
        Chasing,    // 追逐玩家
        Attacking,  // 攻擊玩家
        Dead        // 死亡
    }

    private BossPhase phase = BossPhase.Dormant;

    [Header("目標")]
    public Transform player;

    [Header("Boss元件")]
    public Animator animator;
    public NavMeshAgent agent;

    [Header("距離設定")]
    public float wakeDistance = 8f;
    public float attackDistance = 3.5f;

    [Header("攻擊設定")]
    public float attackCooldown = 2.5f;
    [Header("第四關命中設定")]
    [SerializeField, Min(0)] private int attackDamage = 20;
    [SerializeField, Range(0f, 1f)] private float hitNormalizedTime = .75f;
    [SerializeField, Range(1f, 180f)] private float attackHalfAngle = 55f;
    private bool hitAttempted;
    private float attackRequestTime;
    private float attackClipLength = 3.333333f;

    private float nextAttackTime = 0f;
    private bool attackStateStarted = false;

    void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (agent == null)
            agent = GetComponent<NavMeshAgent>();
    }

    void Start()
    {
        if (animator == null || agent == null)
        {
            Debug.LogError("Boss Animator 或 NavMesh Agent 未設定！");
            enabled = false;
            return;
        }

        animator.applyRootMotion = false;
        agent.updateRotation = false;
        if (animator.runtimeAnimatorController != null)
            foreach (var clip in animator.runtimeAnimatorController.animationClips)
                if (clip && clip.name == "Attack1") { attackClipLength = clip.length; break; }
        if (agent.enabled && !agent.isOnNavMesh && NavMesh.SamplePosition(transform.position, out NavMeshHit nearby, 1f, agent.areaMask))
            agent.Warp(nearby.position);

        // Boss尚未甦醒，不允許移動
        if (agent.enabled)
        {
            if (agent.isOnNavMesh)
            {
                agent.isStopped = true;
            }
            else
            {
                Debug.LogError("Boss 初始位置不在 NavMesh 上！");
            }
        }

        animator.SetFloat("Speed", 0f);

        Debug.Log("Boss正在地底休眠...");
    }

    void Update()
    {
        if (player == null) return;


        switch (phase)
        {
            case BossPhase.Dormant:
                CheckPlayerDistance();
                break;

            case BossPhase.Emerging:
                CheckEmergence();
                break;

            case BossPhase.Roaring:
                CheckRoarFinished();
                break;

            case BossPhase.Chasing:
                ChasePlayer();
                break;

            case BossPhase.Attacking:
                CheckAttackFinished();
                break;

            case BossPhase.Dead:
                break;
        }

    }

    // 計算水平距離
    float GetDistance()
    {
        Vector3 a = transform.position;
        Vector3 b = player.position;

        a.y = 0f;
        b.y = 0f;

        return Vector3.Distance(a, b);
    }

    // 1. 玩家靠近，Boss甦醒
    void CheckPlayerDistance()
    {
        if (GetDistance() <= wakeDistance)
        {
            animator.SetTrigger("WakeUp");

            phase = BossPhase.Emerging;

            Debug.Log("Boss甦醒，開始爬出地面！");
        }
    }

    // 2. 爬出動畫結束，進入吼叫
    void CheckEmergence()
    {
        if (animator.GetCurrentAnimatorStateInfo(0)
            .IsName("Roar"))
        {
            phase = BossPhase.Roaring;

            Debug.Log("Boss開始吼叫！");
        }
    }

    // 3. 吼叫完成，開始追逐
    void CheckRoarFinished()
    {
        bool reachedRun =
            animator.GetCurrentAnimatorStateInfo(0)
            .IsName("Run");

        if (reachedRun && !animator.IsInTransition(0))
        {
            StartChasing();
        }
    }

    // 啟動NavMesh Agent
    void StartChasing()
    {
        if (!agent.enabled || !agent.isOnNavMesh)
        {
            Debug.LogError("Boss 無法在 NavMesh 上移動！");
            return;
        }

        agent.isStopped = false;
        agent.SetDestination(player.position);

        phase = BossPhase.Chasing;

        Debug.Log("Boss開始追逐玩家！");
    }

    // 4. 追逐玩家
    void ChasePlayer()
    {
        if (!agent.enabled || !agent.isOnNavMesh) return;

        Vector3 facing = Vector3.ProjectOnPlane(player.position - transform.position, Vector3.up);
        if (facing.sqrMagnitude > .001f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(facing), agent.angularSpeed * Time.deltaTime);
        agent.SetDestination(player.position);

        // 將移動速度傳給Animator
        animator.SetFloat(
            "Speed",
            agent.velocity.magnitude
        );

        // 玩家進入攻擊範圍
        if (GetDistance() <= attackDistance && facing.sqrMagnitude > .001f &&
            Vector3.Angle(transform.forward, facing) <= attackHalfAngle && Time.time >= nextAttackTime)
        {
            agent.isStopped = true;

            animator.SetFloat("Speed", 0f);
            animator.SetTrigger("Attack");

            attackStateStarted = false;
            hitAttempted = false;
            attackRequestTime = Time.time;
            phase = BossPhase.Attacking;

            Debug.Log("Boss發動攻擊！");
        }
    }

    // 5. 攻擊動畫結束





    void CheckAttackFinished()
    {
        if (!animator || !player) return;
        Vector3 facing = Vector3.ProjectOnPlane(player.position - transform.position, Vector3.up);
        if (facing.sqrMagnitude > .001f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(facing), agent.angularSpeed * Time.deltaTime);
        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
        if (!attackStateStarted)
        {
            if (current.IsName("Attack1")) attackStateStarted = true;
            else if (Time.time - attackRequestTime > attackClipLength) FinishAttack(false);
            return;
        }
        if (!current.IsName("Attack1")) { FinishAttack(false); return; }
        if (!hitAttempted && current.normalizedTime >= hitNormalizedTime)
        {
            hitAttempted = true;
            TryHitPlayer();
        }
        if (current.normalizedTime >= .9f) FinishAttack(true);
    }

    private void TryHitPlayer()
    {
        if (phase != BossPhase.Attacking || !player || GetDistance() > attackDistance) return;
        Vector3 direction = Vector3.ProjectOnPlane(player.position - transform.position, Vector3.up);
        if (direction.sqrMagnitude < .001f || Vector3.Angle(transform.forward, direction) > attackHalfAngle) return;
        var receiver = player.GetComponent<SDGAdventure.Level04.Level04PlayerHealth>();
        var health = GetComponent<BossHealth>();
        var capsule = player.GetComponent<CharacterController>();
        if (!receiver || !health || health.IsDead || !capsule) return;
        Vector3 origin = transform.position + Vector3.up * 1.4f;
        Vector3 target = player.TransformPoint(capsule.center);
        Vector3 delta = target - origin;
        if (delta.sqrMagnitude < .0001f) return;
        var hits = Physics.RaycastAll(origin, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a,b) => a.distance.CompareTo(b.distance));
        foreach (var hit in hits)
        {
            if (hit.collider.transform.IsChildOf(transform)) continue;
            if (hit.collider.transform.IsChildOf(player)) break;
            return;
        }
        receiver.TryBossHit(health, attackDamage, transform.position);
    }

    private void FinishAttack(bool crossFade)
    {
        attackStateStarted = false;
        animator.ResetTrigger("Attack");
        if (crossFade) animator.CrossFade("Run", .1f);
        nextAttackTime = Time.time + attackCooldown;
        if (agent && agent.enabled && agent.isOnNavMesh) { agent.isStopped = false; if (player) agent.SetDestination(player.position); }
        phase = BossPhase.Chasing;
    }

    public void OnBossDeath()
    {
        if (phase == BossPhase.Dead) return;

        phase = BossPhase.Dead;

        if (agent != null &&
            agent.enabled &&
            agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }

        animator.SetFloat("Speed", 0f);
        animator.ResetTrigger("Attack");
        animator.SetTrigger("Death");

        Debug.Log("Boss已死亡！");
    }
}