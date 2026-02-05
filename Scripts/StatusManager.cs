using UnityEngine;
using System.Collections.Generic;

public class StatusManager : MonoBehaviour
{
    [System.Serializable]
    public class ActiveBuff
    {
        public BuffData data;
        public float timer;
        public float tickTimer;

        public ActiveBuff(BuffData data)
        {
            this.data = data;
            this.timer = data.duration;
            this.tickTimer = data.triggerImmediately ? data.tickInterval : 0f;
        }
    }

    public List<ActiveBuff> currentBuffs = new List<ActiveBuff>();

    // 🔥 修改点 1: 不再只认 PlayerHealth，而是认接口
    private IDamageable targetHealth;

    private void Awake()
    {
        // 🔥 修改点 2: 获取接口，这样挂在玩家身上能用，挂在怪物身上也能用
        targetHealth = GetComponent<IDamageable>();
    }

    private void Update()
    {
        for (int i = currentBuffs.Count - 1; i >= 0; i--)
        {
            ActiveBuff buff = currentBuffs[i];
            buff.timer -= Time.deltaTime;
            if (buff.timer <= 0) { currentBuffs.RemoveAt(i); continue; }

            if (buff.data.damagePerTick > 0)
            {
                buff.tickTimer += Time.deltaTime;
                if (buff.tickTimer >= buff.data.tickInterval)
                {
                    buff.tickTimer = 0f;
                    // 🔥 修改点 3: 调用接口扣血
                    if (targetHealth != null) targetHealth.TakeDamage(buff.data.damagePerTick);
                }
            }
        }
    }

    public void ApplyBuff(BuffData newData)
    {
        if (!newData.isStackable)
        {
            var existingBuff = currentBuffs.Find(x => x.data == newData);
            if (existingBuff != null)
            {
                existingBuff.timer = newData.duration;
                if (newData.triggerImmediately) existingBuff.tickTimer = newData.tickInterval;
            }
            else
            {
                currentBuffs.Add(new ActiveBuff(newData));
            }
        }
        else
        {
            currentBuffs.Add(new ActiveBuff(newData));
        }

        // 注意：如果你不想在怪物头顶显示 UI，这里可以加个判断
        // 目前这样写，怪物中 Buff 也会试图调用 UI，可能看起来有点怪，但功能是好的
        if (GameStatusUI.Instance != null && gameObject.CompareTag("Player"))
        {
            GameStatusUI.Instance.ShowStatus(newData.uiMessage, newData.duration, newData.uiColor, newData.isStackable);
        }
    }

    // 之前给你加的“清除负面状态”功能 (保留在这里)
    public void ClearDebuffsOnRest()
    {
        for (int i = currentBuffs.Count - 1; i >= 0; i--)
        {
            if (currentBuffs[i].data.clearOnRest)
            {
                currentBuffs.RemoveAt(i);
            }
        }
        // UI 刷新部分省略，因为怪物通常不需要刷新 UI
    }
}