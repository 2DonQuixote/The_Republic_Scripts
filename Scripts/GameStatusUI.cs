using UnityEngine;
using System.Collections.Generic;

public class GameStatusUI : MonoBehaviour
{
    [Header("配置")]
    public GameObject statusItemPrefab;
    public Transform container; // 挂载 VerticalLayoutGroup 的那个物体

    // 缓存字典：通过 BuffData 快速找到对应的 UI 条目，不用循环找了！
    private Dictionary<BuffData, StatusItemController> activeItems = new Dictionary<BuffData, StatusItemController>();

    private StatusManager targetPlayerStatus;

    private void Start()
    {
        // 1. 寻找玩家身上的 StatusManager
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            targetPlayerStatus = player.GetComponent<StatusManager>();

            if (targetPlayerStatus != null)
            {
                // 2. 订阅所有事件
                targetPlayerStatus.OnBuildupUpdated += HandleBuildup;
                targetPlayerStatus.OnBuffActivated += HandleActive;
                targetPlayerStatus.OnBuffEnded += HandleEnd;
                targetPlayerStatus.OnAllBuffsCleared += HandleClearAll;
            }
        }
        else
        {
            Debug.LogError("GameStatusUI: 找不到 Player！");
        }
    }

    private void OnDestroy()
    {
        // 记得取消订阅，好习惯
        if (targetPlayerStatus != null)
        {
            targetPlayerStatus.OnBuildupUpdated -= HandleBuildup;
            targetPlayerStatus.OnBuffActivated -= HandleActive;
            targetPlayerStatus.OnBuffEnded -= HandleEnd;
            targetPlayerStatus.OnAllBuffsCleared -= HandleClearAll;
        }
    }

    // --- 事件处理逻辑 ---

    // 1. 处理积累值变化
    private void HandleBuildup(BuffData data, float current, float max)
    {
        // 如果归零了，说明没爆出来就衰减完了 -> 移除 UI
        if (current <= 0)
        {
            RemoveItem(data);
            return;
        }

        StatusItemController item = GetOrCreateItem(data);
        // 通知 UI 更新积累条
        item.UpdateBuildup(data.uiMessage, current, max, data.uiColor);
    }

    // 2. 处理 Buff 激活 (转倒计时)
    private void HandleActive(BuffData data)
    {
        StatusItemController item = GetOrCreateItem(data);
        // 通知 UI 切换为倒计时模式
        item.Setup(data.uiMessage, data.duration, data.uiColor);
    }

    // 3. 处理 Buff 结束
    private void HandleEnd(BuffData data)
    {
        RemoveItem(data);
    }

    // 4. 处理全清 (坐火)
    private void HandleClearAll()
    {
        foreach (var kvp in activeItems)
        {
            if (kvp.Value != null) Destroy(kvp.Value.gameObject);
        }
        activeItems.Clear();
    }

    // --- 内部辅助方法 ---

    private StatusItemController GetOrCreateItem(BuffData data)
    {
        // 如果字典里已经有了，直接返回
        if (activeItems.ContainsKey(data))
        {
            if (activeItems[data] != null) return activeItems[data];
            else activeItems.Remove(data); // 如果物体被意外删了，从字典移除
        }

        // 没有则创建
        GameObject newObj = Instantiate(statusItemPrefab, container);
        StatusItemController ctrl = newObj.GetComponent<StatusItemController>();

        // 加入字典
        activeItems.Add(data, ctrl);
        return ctrl;
    }

    private void RemoveItem(BuffData data)
    {
        if (activeItems.ContainsKey(data))
        {
            StatusItemController ctrl = activeItems[data];
            if (ctrl != null)
            {
                // 调用它自己的退场动画，它会在动画结束时 Destroy 自己
                ctrl.RemoveSelf();
            }
            activeItems.Remove(data);
        }
    }
}