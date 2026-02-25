using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class EnemyHealthBar : MonoBehaviour
{
    [Header("UI 组件引用")]
    public GameObject uiCanvas; // 控制显示/隐藏的画布容器
    public Image fillImage;     // 绿色的血条图片

    [Header("✨ 受击反馈配置")]
    public Color flashColor = new Color(1f, 1f, 1f, 1f);
    public float flashDuration = 0.1f;

    private Camera mainCam;
    private float lastHealth = -1f;
    private Color originalColor = Color.clear;
    private Tween colorTween;

    // 🔥 新增：持有发布者的引用
    private EnemyHealth targetEnemy;

    void Awake()
    {
        mainCam = Camera.main;

        // 1. 自动寻找发布者
        // 因为 UI 通常挂在怪物的子物体上，所以用 GetComponentInParent 往上找
        targetEnemy = GetComponentInParent<EnemyHealth>();

        if (targetEnemy == null)
        {
            // 双重保险：万一挂在同一层级
            targetEnemy = GetComponent<EnemyHealth>();
        }

        if (targetEnemy == null)
        {
            Debug.LogError($"UI 错误：{gameObject.name} 找不到它的父亲 EnemyHealth！");
        }

        // 初始化颜色保险逻辑
        if (fillImage != null)
        {
            originalColor = fillImage.color;
            if (originalColor.a == 0 || (originalColor.r == 0 && originalColor.g == 0 && originalColor.b == 0))
            {
                originalColor = Color.red;
                fillImage.color = originalColor;
            }
        }

        // 默认隐藏血条 (满血不显示)
        if (uiCanvas != null) uiCanvas.SetActive(false);
    }

    // 🔥 订阅事件 (当脚本启用时)
    void OnEnable()
    {
        if (targetEnemy != null)
        {
            targetEnemy.OnHealthChanged += UpdateHealth;
        }

        // 颜色保险 (保留你原来的逻辑)
        if (fillImage != null && originalColor != Color.clear)
        {
            fillImage.color = originalColor;
        }
    }

    // 🔥 取消订阅 (当脚本禁用或销毁时)
    void OnDisable()
    {
        if (targetEnemy != null)
        {
            targetEnemy.OnHealthChanged -= UpdateHealth;
        }
    }

    void LateUpdate()
    {
        // 只有显示出来的时候才计算朝向，节省性能
        if (uiCanvas != null && uiCanvas.activeInHierarchy)
        {
            if (mainCam != null)
            {
                transform.rotation = mainCam.transform.rotation;
            }
        }
    }

    // 🔥 事件响应方法：签名必须匹配 Action<float, float>
    public void UpdateHealth(float current, float max)
    {
        if (uiCanvas == null || fillImage == null) return;

        // 1. 更新进度条
        float pct = current / max;
        fillImage.fillAmount = pct;

        // 2. 智能显隐：只有 "受伤" 且 "没死" 才显示血条
        bool shouldShow = current < max && current > 0;
        uiCanvas.SetActive(shouldShow);

        // 3. 受击闪烁逻辑
        // 如果上次血量不是初始值(-1)，且当前血量变少了，说明挨打了
        if (lastHealth != -1f && current < lastHealth)
        {
            PlayDamageEffect();
        }

        lastHealth = current;
    }

    private void PlayDamageEffect()
    {
        if (originalColor == Color.clear && fillImage != null) originalColor = fillImage.color;
        if (colorTween != null && colorTween.IsActive()) colorTween.Kill();

        fillImage.color = originalColor;

        Color safeFlashColor = new Color(flashColor.r, flashColor.g, flashColor.b, 1f);
        colorTween = fillImage.DOColor(safeFlashColor, flashDuration)
            .SetLoops(2, LoopType.Yoyo)
            .SetEase(Ease.OutQuad)
            .OnComplete(() => {
                if (fillImage != null) fillImage.color = originalColor;
            });
    }
}