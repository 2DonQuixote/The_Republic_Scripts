using UnityEngine;
using System.Collections;

public class RangedEnemy : BaseEnemy
{
    [Header("=== 🏹 远程攻击配置 ===")]
    public GameObject projectilePrefab; // 拖入毒球预制体
    public Transform throwPoint;        // 毒球发射点（一般在手掌心）
    public float rangedAttackRange = 12f; // 只要在这个范围内，就会开始想攻击
    public float throwInterval = 3.0f;    // 丢毒球的冷却时间

    [Header("=== ⚔️ 近战自卫配置 ===")]
    public float meleeTriggerDistance = 3.0f; // 小于这个距离，强行切换为重击
    public float heavySmashDamage = 30f;
    public float heavyLungeDistance = 2.0f;
    public float heavyLungeDuration = 0.3f;
    public float heavyEndDelay = 1.5f;

    // 内部状态
    private bool isMeleeMode = false;

    protected override void Start()
    {
        base.Start();
        // 覆盖基类的攻击距离，让它在很远的地方就停下开始射击
        // 注意：BaseEnemy 里我们是用 attackRange 来判断是否进入 Attack 状态的
        // 所以我们把 attackRange 设为远程距离
        attackRange = rangedAttackRange;
    }

    // ==========================================
    // 决策中心：决定是丢毒还是砸人
    // ==========================================
    protected override void PerformAttack()
    {
        float distance = Vector3.Distance(transform.position, player.position);

        if (distance <= meleeTriggerDistance)
        {
            // 距离太近，触发重击自卫！
            isMeleeMode = true;
            ExecuteHeavySmash();
        }
        else
        {
            // 距离安全，丢毒！
            isMeleeMode = false;
            ExecuteRangedThrow();
        }
    }

    // --- 技能 1: 远程丢毒 ---
    private void ExecuteRangedThrow()
    {
        // 播放投掷动画 (需要在 Animator 里加 "Throw" Trigger)
        if (anim != null) anim.SetTrigger("Throw");

        // 远程攻击可以允许转身自瞄（不用锁死方向，除非你想增加难度）
        isRotationLocked = false;

        // 注意：具体的发射逻辑 SpawnProjectile() 应该由动画事件触发！
        // 如果不想配动画事件，可以用下面的协程代替（不推荐，建议用事件）
        // StartCoroutine(FallbackThrowRoutine()); 

        Debug.Log("<color=cyan>远程怪：吃我一记毒球！</color>");
    }

    // --- 技能 2: 近战重击 ---
    private void ExecuteHeavySmash()
    {
        // 播放重击动画
        if (anim != null) anim.SetTrigger("Attack2");

        // 启动我们熟悉的突进协程
        StartCoroutine(LungeForwardCoroutine(heavyLungeDistance, 0.5f, heavyLungeDuration, heavyEndDelay, false));

        Debug.Log("<color=red>远程怪：别靠这么近！重击！</color>");
    }

    // ==========================================
    // 🛠️ 动画事件接收器
    // ==========================================

    // 1. 远程攻击事件：在挥手的那一帧调用
    public void SpawnProjectile()
    {
        if (projectilePrefab != null && throwPoint != null && player != null)
        {
            GameObject ball = Instantiate(projectilePrefab, throwPoint.position, Quaternion.identity);
            PoisonProjectile script = ball.GetComponent<PoisonProjectile>();

            // 简单的预判：朝玩家胸口丢
            Vector3 aimDir = (player.position + Vector3.up * 1.0f - throwPoint.position).normalized;
            script.Launch(aimDir);
        }
    }

    // 2. 近战攻击事件：在砸地的那一帧调用
    public void DoMeleeDamage()
    {
        // 只有近战模式才判定这个
        if (!isMeleeMode) return;

        if (player != null)
        {
            float dist = Vector3.Distance(transform.position, player.position);
            // 判定范围稍微大一点
            if (dist <= meleeTriggerDistance + 1.0f)
            {
                IDamageable target = player.GetComponent<IDamageable>();
                if (target != null) target.TakeDamage(heavySmashDamage);
            }
        }
    }

    // 复用之前的突进协程逻辑
    IEnumerator LungeForwardCoroutine(float distance, float delay, float moveDuration, float endDelay, bool isGrabType)
    {
        if (agent != null) agent.velocity = Vector3.zero;
        if (delay > 0) yield return new WaitForSeconds(delay);
        if (isDead) yield break;

        isRotationLocked = true; // 近战重击必须锁方向！

        float speed = distance / moveDuration;
        float timer = 0f;
        while (timer < moveDuration)
        {
            if (isDead || agent == null) break;
            agent.Move(transform.forward * speed * Time.deltaTime);
            timer += Time.deltaTime;
            yield return null;
        }

        if (endDelay > 0) yield return new WaitForSeconds(endDelay);
        OnAttackAnimEnd();
    }
}