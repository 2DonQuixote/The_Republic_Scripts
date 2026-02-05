using UnityEngine;
using UnityEngine.UI;

public class EnemyHealthBar : MonoBehaviour
{
    [Header("UI 组件引用")]
    public GameObject uiCanvas; // 血条的父物体（用来整体隐藏）
    public Image fillImage;     // 真正显示血量的那个绿色/红色条

    private Camera mainCam;

    void Start()
    {
        mainCam = Camera.main;

        // 游戏开始时，如果是满血，直接隐藏
        // 我们假设初始状态是隐藏的，等收到 UpdateHealth 调用再决定是否显示
        if (uiCanvas != null) uiCanvas.SetActive(false);
    }

    void LateUpdate()
    {
        // Billboard 效果：让血条始终正对着摄像机
        // 只有显示的时候才计算，省点性能
        if (uiCanvas != null && uiCanvas.activeInHierarchy)
        {
            // 让 UI 的正面朝向摄像机的方向
            transform.LookAt(transform.position + mainCam.transform.forward);
        }
    }

    // 供 EnemyHealth 调用的核心方法
    public void UpdateHealth(float current, float max)
    {
        if (uiCanvas == null || fillImage == null) return;

        // 1. 计算比例
        float pct = current / max;
        fillImage.fillAmount = pct;

        // 2. 只有“当前血量 < 最大血量”且“没死透(>0)”时才显示
        // 如果你希望死掉时血条也消失，就加 && current > 0
        bool shouldShow = current < max && current > 0;

        uiCanvas.SetActive(shouldShow);
    }
}