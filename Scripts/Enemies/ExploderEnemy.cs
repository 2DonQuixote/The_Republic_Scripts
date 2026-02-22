using UnityEngine;

public class ExploderEnemy : BaseEnemy
{
    [Header("=== 💣 自爆怪专属配置 ===")]
    [Tooltip("自爆的波及半径 (建议比攻击距离 Attack Range 大一些)")]
    public float explosionRadius = 3.5f;

    [Tooltip("自爆造成的巨大伤害")]
    public float explosionDamage = 80f;

    [Tooltip("自爆时的爆炸特效 (可选，拖入你的爆炸预制体)")]
    public GameObject explosionVFX;

    private bool hasExploded = false;

    // ==========================================
    // 🌟 出手逻辑：只负责拉响引信，播放动画
    // ==========================================
    protected override void PerformAttack()
    {
        if (hasExploded || isDead) return;

        // 1. 触发自爆动画！
        if (anim != null) anim.SetTrigger("ZiBao");

        // 2. 物理急刹车，原地准备爆炸
        if (agent != null && agent.isActiveAndEnabled) agent.velocity = Vector3.zero;

        // 3. 锁死方向 (不准再跟着玩家转头了)
        isRotationLocked = true;

        // ⚠️ 取消了代码里的倒计时协程！现在把命交给动画事件！
        Debug.Log("<color=orange>【自爆警告】自爆怪开始蓄力！快跑！</color>");
    }

    // ==========================================
    // 🔥 核心：由自爆动画的关键帧来调用此方法！
    // ==========================================
    public void TriggerExplosion()
    {
        // 🛡️ 死亡拦截：如果它在自爆动画播完之前，就已经被玩家打死了，绝对不准爆！
        if (isDead || hasExploded) return;

        hasExploded = true;

        // 1. 爆炸判定：球形范围 AOE 伤害
        Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius);
        foreach (var hit in hits)
        {
            // 不炸自己，但可以炸玩家和其他普通怪物！
            if (hit.gameObject == this.gameObject) continue;

            IDamageable target = hit.GetComponent<IDamageable>();
            if (target != null)
            {
                target.TakeDamage(explosionDamage);
            }
        }

        // 2. 生成爆炸特效
        if (explosionVFX != null)
        {
            Instantiate(explosionVFX, transform.position + Vector3.up, Quaternion.identity);
        }

        Debug.Log("<color=red>💥 轰！自爆怪已引爆，造成了范围伤害！</color>");

        // 3. 成功自爆：灰飞烟灭，连根拔起（不留尸体）
        Destroy(gameObject);
    }

    // ==========================================
    // 💀 死亡逻辑补充：没来得及爆就被砍死了
    // ==========================================
    public override void TriggerDeath()
    {
        // 调用基类的死亡，这会把 isDead 变成 true，从而彻底卡死上面的 TriggerExplosion
        base.TriggerDeath();

        // EnemyHealth.cs 会接管后续，播放普通的 "Die" 动画，把尸体留在地上
        Debug.Log("<color=grey>自爆怪被击杀，未能成功引爆，留下一具全尸。</color>");
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        Gizmos.color = new Color(1, 0, 0, 0.4f);
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}