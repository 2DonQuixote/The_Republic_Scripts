public interface IDamageable
{
    // 🔥 把第二个参数加上，让合同与代码保持一致
    void TakeDamage(float amount, bool triggerHitReaction = true);
}