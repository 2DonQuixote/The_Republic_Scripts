using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class EnemyHealthBar : MonoBehaviour
{
    [Header("UI 组件引用")]
    public GameObject uiCanvas;
    public Image fillImage;

    [Header("✨ 受击反馈配置")]
    // 💡 这里我们给个默认值，防止 Inspector 里是黑的
    public Color flashColor = new Color(1f, 1f, 1f, 1f); // 纯白
    public float flashDuration = 0.1f;

    private Camera mainCam;
    private float lastHealth = -1f; // 初始标记
    private Color originalColor = Color.clear; // 初始为空，用来判断是否已初始化
    private Tween colorTween;

    void Awake() // 改用 Awake，比 Start 更早运行，防跑飞
    {
        mainCam = Camera.main;

        // 1. 强制抓取原始颜色
        if (fillImage != null)
        {
            originalColor = fillImage.color;
            // 🚑 保险措施：如果抓到的是全透明/黑色，强行设为红色，防止出错
            if (originalColor.a == 0 || (originalColor.r == 0 && originalColor.g == 0 && originalColor.b == 0))
            {
                originalColor = Color.red;
                fillImage.color = originalColor;
                Debug.LogWarning("检测到血条初始颜色异常，已自动修正为红色。");
            }
        }

        // 2. 初始隐藏
        if (uiCanvas != null) uiCanvas.SetActive(false);
    }

    void LateUpdate()
    {
        // 只有显示的时候才计算朝向，省性能
        if (uiCanvas != null && uiCanvas.activeInHierarchy)
        {
            if (mainCam != null)
            {
                transform.LookAt(transform.position + mainCam.transform.forward);
            }
        }
    }

    public void UpdateHealth(float current, float max)
    {
        if (uiCanvas == null || fillImage == null) return;

        // 1. 刷新进度条
        float pct = current / max;
        fillImage.fillAmount = pct;

        bool shouldShow = current < max && current > 0;
        uiCanvas.SetActive(shouldShow);

        // 2. 只有当 lastHealth 已经被初始化过(不是-1)，且血量减少时，才闪烁
        if (lastHealth != -1f && current < lastHealth)
        {
            PlayDamageEffect();
        }

        // 更新记录
        lastHealth = current;
    }

    private void PlayDamageEffect()
    {
        // 🚑 双重保险：如果原始颜色没抓到，现在再抓一次
        if (originalColor == Color.clear && fillImage != null) originalColor = fillImage.color;

        // 杀掉旧动画
        if (colorTween != null && colorTween.IsActive()) colorTween.Kill();

        // 1. 先瞬间变回原始颜色 (比如红色)
        fillImage.color = originalColor;

        // 2. 闪烁动画 (变白 -> 变回原色)
        // 🚑 强制 FlashColor 的 Alpha 为 1，防止变透明
        Color safeFlashColor = new Color(flashColor.r, flashColor.g, flashColor.b, 1f);

        colorTween = fillImage.DOColor(safeFlashColor, flashDuration)
            .SetLoops(2, LoopType.Yoyo)
            .SetEase(Ease.OutQuad)
            .OnComplete(() => {
                // 动画结束，确保最后一定是原始颜色，不是黑色，也不是白色
                fillImage.color = originalColor;
            });
    }

    // 这是一个保险方法：当物体被禁用再启用时，确保颜色是对的
    void OnEnable()
    {
        if (fillImage != null && originalColor != Color.clear)
        {
            fillImage.color = originalColor;
        }
    }
}