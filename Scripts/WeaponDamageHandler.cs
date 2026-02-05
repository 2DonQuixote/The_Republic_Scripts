using UnityEngine;
using System.Collections.Generic;

public class WeaponDamageHandler : MonoBehaviour
{
    // 这一刀的具体伤害（由 PlayerController 传过来）
    private float currentDamageAmount;

    // 碰撞体引用
    private Collider damageCollider;

    // 防止一刀挥过去，同一个敌人判定了两次伤害（去重列表）
    private List<GameObject> hitList = new List<GameObject>();

    private void Awake()
    {
        damageCollider = GetComponent<Collider>();
        // 初始状态：关闭碰撞体，或者把 IsTrigger 打开但脚本逻辑不执行
        if (damageCollider != null)
        {
            damageCollider.isTrigger = true;
            damageCollider.enabled = false; // 平时不准伤人
        }
    }

    // 🔥 供 PlayerController 调用：开始造成伤害
    public void EnableDamage(float damage)
    {
        currentDamageAmount = damage;
        hitList.Clear(); // 清空去重列表
        if (damageCollider != null) damageCollider.enabled = true;
    }

    // 🔥 供 PlayerController 调用：结束伤害判定
    public void DisableDamage()
    {
        if (damageCollider != null) damageCollider.enabled = false;
        hitList.Clear();
    }

    private void OnTriggerEnter(Collider other)
    {
        // 1. 如果打到了自己，或者不在伤害判定其间，忽略
        if (other.CompareTag("Player")) return;

        // 2. 去重：如果这一刀已经砍过这个怪了，忽略
        if (hitList.Contains(other.gameObject)) return;

        // 3. 寻找 IDamageable 接口
        IDamageable target = other.GetComponent<IDamageable>();
        if (target != null)
        {
            // 造成实质伤害！
            target.TakeDamage(currentDamageAmount);

            // 记录一下，防止连击判定
            hitList.Add(other.gameObject);

            // 可选：播放个打击音效或特效
            // AudioManager.PlayHitSound();
        }
    }
}