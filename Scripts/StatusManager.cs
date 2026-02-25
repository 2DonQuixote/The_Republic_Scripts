using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

public class StatusManager : MonoBehaviour
{
    // 🔥 定义事件：逻辑层只发出信号，不关心谁在听
    public event Action<BuffData, float, float> OnBuildupUpdated; // (数据, 当前值, 阈值)
    public event Action<BuffData> OnBuffActivated;                // (数据) -> 转为倒计时
    public event Action<BuffData> OnBuffEnded;                    // (数据) -> 移除图标
    public event Action OnAllBuffsCleared;                        // -> 坐火时清空所有

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
            this.tickTimer = 0f;
        }
    }

    public class BuildupTracker
    {
        public float currentValue;
        public float decayPauseTimer;
    }

    private Dictionary<StatusType, float> resistanceBonuses = new Dictionary<StatusType, float>();
    private Dictionary<BuffData, BuildupTracker> buildupTrackers = new Dictionary<BuffData, BuildupTracker>();
    public List<ActiveBuff> currentBuffs = new List<ActiveBuff>();

    private IDamageable targetHealth;

    private void Awake() { targetHealth = GetComponent<IDamageable>(); }

    private void Update()
    {
        HandleActiveBuffs();
        HandleBuildupDecay();
    }

    // ==========================================
    // 增加积累值
    // ==========================================
    public void AddStatusBuildup(BuffData data, float amount)
    {
        if (data == null) return;

        // 1. 如果 Buff 已经激活 (Active)
        var activeBuff = currentBuffs.Find(x => x.data == data);
        if (activeBuff != null)
        {
            if (data.refreshTimeOnHit)
            {
                activeBuff.timer = data.duration;
                // 🔥 广播：Buff 刷新了 (UI 应该重置倒计时)
                OnBuffActivated?.Invoke(data);
            }
            return;
        }

        // 2. 如果还没激活，处理积累条 (Buildup)
        if (!buildupTrackers.ContainsKey(data)) buildupTrackers[data] = new BuildupTracker();
        BuildupTracker tracker = buildupTrackers[data];

        float maxThreshold = GetRealThreshold(data);

        tracker.currentValue += amount;
        tracker.decayPauseTimer = 2.0f;

        // 🔥 广播：积累值变了 (UI 去更新进度条)
        OnBuildupUpdated?.Invoke(data, tracker.currentValue, maxThreshold);

        // 3. 判定爆发
        if (tracker.currentValue >= maxThreshold)
        {
            ActivateBuff(data);
            tracker.currentValue = 0f;
            // 爆发时不需要专门发 Buildup=0 的通知，
            // 因为 ActivateBuff 会紧接着发出 Activated 通知，UI 会自动切换模式
        }
    }

    private void ActivateBuff(BuffData newData)
    {
        currentBuffs.Add(new ActiveBuff(newData));

        // 🔥 广播：状态激活！(UI 应该切换为倒计时模式)
        OnBuffActivated?.Invoke(newData);
    }

    private void HandleActiveBuffs()
    {
        for (int i = currentBuffs.Count - 1; i >= 0; i--)
        {
            ActiveBuff buff = currentBuffs[i];

            // 🛡️ 保险：防止空数据报错
            if (buff == null || buff.data == null)
            {
                currentBuffs.RemoveAt(i);
                continue;
            }

            buff.timer -= Time.deltaTime;

            if (buff.data.damagePerTick > 0)
            {
                buff.tickTimer += Time.deltaTime;
                if (buff.tickTimer >= buff.data.tickInterval)
                {
                    buff.tickTimer = 0f;
                    if (targetHealth != null) targetHealth.TakeDamage(buff.data.damagePerTick, false);
                }
            }

            // 时间到，移除状态
            if (buff.timer <= 0)
            {
                // 🔥 广播：Buff 结束
                OnBuffEnded?.Invoke(buff.data);
                currentBuffs.RemoveAt(i);
            }
        }
    }

    private void HandleBuildupDecay()
    {
        List<BuffData> keys = new List<BuffData>(buildupTrackers.Keys);
        foreach (var key in keys)
        {
            // 如果已经激活了，就不处理积累衰减
            if (currentBuffs.Exists(x => x.data == key)) continue;

            BuildupTracker tracker = buildupTrackers[key];
            float maxThreshold = GetRealThreshold(key);
            bool hasChanged = false;

            if (tracker.decayPauseTimer > 0)
            {
                tracker.decayPauseTimer -= Time.deltaTime;
            }
            else if (tracker.currentValue > 0)
            {
                tracker.currentValue -= key.decayRate * Time.deltaTime;
                if (tracker.currentValue < 0) tracker.currentValue = 0;
                hasChanged = true;
            }

            // 只有数值变化了才广播，节省性能
            if (hasChanged)
            {
                // 🔥 广播：积累值衰减
                OnBuildupUpdated?.Invoke(key, tracker.currentValue, maxThreshold);
            }
        }
    }

    public float GetRealThreshold(BuffData data)
    {
        float bonus = 0f;
        if (resistanceBonuses.ContainsKey(data.type)) bonus = resistanceBonuses[data.type];
        return data.baseThreshold + bonus;
    }

    public void AddResistance(StatusType type, float amount)
    {
        if (!resistanceBonuses.ContainsKey(type)) resistanceBonuses[type] = 0;
        resistanceBonuses[type] += amount;
    }

    public void ClearDebuffsOnRest()
    {
        for (int i = currentBuffs.Count - 1; i >= 0; i--)
        {
            if (currentBuffs[i] != null && currentBuffs[i].data != null && currentBuffs[i].data.clearOnRest)
            {
                currentBuffs.RemoveAt(i);
            }
        }
        buildupTrackers.Clear();

        // 🔥 广播：清空所有 (UI 应该删除所有图标)
        OnAllBuffsCleared?.Invoke();
    }
}