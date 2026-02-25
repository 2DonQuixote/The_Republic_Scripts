using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

// 统一命名：PlayerHealth (逻辑) <-> PlayerHealthBar (UI)
public class PlayerHealthBar : MonoBehaviour
{
    [Header("=== UI 组件引用 ===")]
    public Image frontFillImage; // 红条
    public Image backGhostImage; // 黄条

    [Header("=== ✨ 震动设置 ===")]
    public RectTransform containerRect; // 整个血条父物体

    [Header("=== 🎨 动画参数 ===")]
    public float frontDuration = 0.5f;
    public float ghostDelay = 0.3f;
    public float ghostDuration = 1.0f;

    [Header("=== 🥊 受击反馈 (Juice) ===")]
    public float punchScale = 0.1f;
    public float punchDuration = 0.12f;
    public int punchVibrato = 8;
    [Range(0, 1)] public float punchElasticity = 0.6f;

    // --- 内部变量 ---
    private PlayerHealth targetPlayer; // 持有逻辑层的引用
    private Tween _frontTween;
    private Tween _ghostTween;
    private Tween _punchTween;
    private float _currentFillAmount = 1f;

    private void Awake()
    {
        if (containerRect == null) containerRect = GetComponent<RectTransform>();
    }

    private void Start()
    {
        // 1. 自动寻找玩家逻辑脚本
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            targetPlayer = playerObj.GetComponent<PlayerHealth>();

            if (targetPlayer != null)
            {
                // 2. 订阅事件 (核心解耦)
                targetPlayer.OnHealthChanged += UpdateUI;

                // 3. 手动初始化一次显示
                // (虽然 PlayerHealth Start 也会广播，但为了防止执行顺序问题，这里可以重置一下 UI)
                frontFillImage.fillAmount = 1f;
                backGhostImage.fillAmount = 1f;
            }
        }
    }

    private void OnDestroy()
    {
        // 4. 销毁时取消订阅，防止内存泄漏
        if (targetPlayer != null)
        {
            targetPlayer.OnHealthChanged -= UpdateUI;
        }

        _frontTween?.Kill();
        _ghostTween?.Kill();
        _punchTween?.Kill();
    }

    // 事件响应方法
    private void UpdateUI(float current, float max)
    {
        float targetFill = (max <= 0) ? 0 : Mathf.Clamp01(current / max);
        bool isDamage = targetFill < (_currentFillAmount - 0.001f);

        // 红条
        _frontTween?.Kill();
        if (frontFillImage != null)
            _frontTween = frontFillImage.DOFillAmount(targetFill, frontDuration).SetEase(Ease.OutCubic);

        // 幽灵条
        _ghostTween?.Kill();
        if (backGhostImage != null)
            _ghostTween = backGhostImage.DOFillAmount(targetFill, ghostDuration).SetDelay(ghostDelay).SetEase(Ease.OutCubic);

        // 震动反馈
        if (isDamage && containerRect != null)
        {
            _punchTween?.Kill();
            containerRect.localScale = Vector3.one;
            _punchTween = containerRect.DOPunchScale(Vector3.one * punchScale, punchDuration, punchVibrato, punchElasticity);
        }

        _currentFillAmount = targetFill;
    }
}