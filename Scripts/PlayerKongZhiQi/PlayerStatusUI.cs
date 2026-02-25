using UnityEngine;
using System.Collections.Generic;

// 改名：PlayerStatusUI，对应 StatusManager
public class PlayerStatusUI : MonoBehaviour
{
    // 移除单例，不再需要 static Instance

    [Header("配置")]
    public GameObject statusItemPrefab;
    public Transform container;

    private Dictionary<BuffData, StatusItemController> activeItems = new Dictionary<BuffData, StatusItemController>();
    private StatusManager targetStatusManager; // 逻辑层引用

    private void Start()
    {
        // 1. 自动寻找玩家的状态逻辑
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            targetStatusManager = player.GetComponent<StatusManager>();

            if (targetStatusManager != null)
            {
                // 2. 像 PlayerHealthBar 那样订阅事件
                targetStatusManager.OnBuildupUpdated += HandleBuildup;
                targetStatusManager.OnBuffActivated += HandleActive;
                targetStatusManager.OnBuffEnded += HandleEnd;
                targetStatusManager.OnAllBuffsCleared += HandleClearAll;
            }
        }
    }

    private void OnDestroy()
    {
        if (targetStatusManager != null)
        {
            targetStatusManager.OnBuildupUpdated -= HandleBuildup;
            targetStatusManager.OnBuffActivated -= HandleActive;
            targetStatusManager.OnBuffEnded -= HandleEnd;
            targetStatusManager.OnAllBuffsCleared -= HandleClearAll;
        }
    }

    // ... 下面的 HandleBuildup, HandleActive 等逻辑代码完全保持不变 ...
    // (直接复制你原来 GameStatusUI 里的逻辑方法即可)

    private void HandleBuildup(BuffData data, float current, float max)
    {
        if (current <= 0) { RemoveItem(data); return; }
        StatusItemController item = GetOrCreateItem(data);
        item.UpdateBuildup(data.uiMessage, current, max, data.uiColor);
    }

    private void HandleActive(BuffData data)
    {
        StatusItemController item = GetOrCreateItem(data);
        item.Setup(data.uiMessage, data.duration, data.uiColor);
    }

    private void HandleEnd(BuffData data) => RemoveItem(data);

    private void HandleClearAll()
    {
        foreach (var kvp in activeItems) if (kvp.Value != null) Destroy(kvp.Value.gameObject);
        activeItems.Clear();
    }

    private StatusItemController GetOrCreateItem(BuffData data)
    {
        if (activeItems.ContainsKey(data))
        {
            if (activeItems[data] != null) return activeItems[data];
            else activeItems.Remove(data);
        }
        GameObject newObj = Instantiate(statusItemPrefab, container);
        StatusItemController ctrl = newObj.GetComponent<StatusItemController>();
        activeItems.Add(data, ctrl);
        return ctrl;
    }

    private void RemoveItem(BuffData data)
    {
        if (activeItems.ContainsKey(data))
        {
            if (activeItems[data] != null) activeItems[data].RemoveSelf();
            activeItems.Remove(data);
        }
    }
}