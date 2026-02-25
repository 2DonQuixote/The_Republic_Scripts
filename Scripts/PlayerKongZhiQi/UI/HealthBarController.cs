using UnityEngine;
using UnityEngine.UI;
using DG.Tweening; // 🔥 必须引入 DOTween

public class HealthBarController : MonoBehaviour
{
    [Header("=== UI 组件引用 ===")]
    public Image frontFillImage; // 前置红条
    public Image backGhostImage; // 后置黄条 (幽灵条)

    [Header("=== ✨ 震动设置 ===")]
    [Tooltip("把整个血条的父物体拖进去 (比如 HealthBarPanel)，受伤时它会震动")]
    public RectTransform containerRect;

    [Header("=== 🎨 动画参数 ===")]
    public float frontDuration = 0.5f; // 红条缩减时间
    public float ghostDelay = 0.3f;    // 黄条延迟多久开始缩
    public float ghostDuration = 1.0f; // 黄条缩减时间

    [Header("=== 🥊 受击反馈 (Juice) ===")]
    public float punchScale = 0.1f;    // 震动幅度 (0.1 = 放大10%)
    public float punchDuration = 0.12f;// 震动时长
    public int punchVibrato = 8;       // 震动频率
    [Range(0, 1)] public float punchElasticity = 0.6f; // 弹性

    // --- 内部变量 ---
    private PlayerHealth targetPlayer;
    private Tween _frontTween;
    private Tween _ghostTween;
    private Tween _punchTween;

    // 记录上一次的血量比例，用于判断是“受伤”还是“回血”
    private float _currentFillAmount = 1f;

    private void Awake()
    {
        // 自动容错：如果你忘记拖 containerRect，我就震动我自己
        if (containerRect == null) containerRect = GetComponent<RectTransform>();
    }

    private void Start()
    {
        // 1. 寻找玩家
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            targetPlayer = playerObj.GetComponent<PlayerHealth>();
            if (targetPlayer != null)
            {
                // 2. 订阅事件：只要血量变了，就通知我
                targetPlayer.OnHealthChanged += UpdateHealthUI;

                // 3. 初始化状态：假设刚开始是满血
                InitializeUI();
            }
        }
    }

    private void OnDestroy()
    {
        // 1. 取消订阅 (防止报错)
        if (targetPlayer != null)
        {
            targetPlayer.OnHealthChanged -= UpdateHealthUI;
        }

        // 2. 🔥 必做：UI销毁时，立刻杀掉所有动画
        // 否则切换场景时，动画还在后台跑，会报 "MissingReferenceException"
        _frontTween?.Kill();
        _ghostTween?.Kill();
        _punchTween?.Kill();
    }

    private void InitializeUI()
    {
        if (frontFillImage != null) frontFillImage.fillAmount = 1f;
        if (backGhostImage != null) backGhostImage.fillAmount = 1f;
        _currentFillAmount = 1f;
    }

    // 核心逻辑：这个方法会在 PlayerHealth 广播时被自动调用
    private void UpdateHealthUI(float current, float max)
    {
        // 计算目标百分比 (0 ~ 1)
        float targetFill = (max <= 0) ? 0 : Mathf.Clamp01(current / max);

        // 判断是否受伤 (新血量 < 旧血量)
        // 减去 0.001f 是为了处理浮点数误差，防止数值没变也触发震动
        bool isDamage = targetFill < (_currentFillAmount - 0.001f);

        // --- 1. 红条动画 (快速缩减) ---
        _frontTween?.Kill(); // 打断上一次动画
        if (frontFillImage != null)
        {
            _frontTween = frontFillImage.DOFillAmount(targetFill, frontDuration)
                .SetEase(Ease.OutCubic);
        }

        // --- 2. 幽灵条动画 (延迟缩减) ---
        _ghostTween?.Kill();
        if (backGhostImage != null)
        {
            _ghostTween = backGhostImage.DOFillAmount(targetFill, ghostDuration)
                .SetDelay(ghostDelay) // 关键：延迟一会儿再动
                .SetEase(Ease.OutCubic);
        }

        // --- 3. 受击震动 ---
        if (isDamage && containerRect != null)
        {
            _punchTween?.Kill();

            // 关键：震动前先把大小重置为 1
            // 否则如果连续快速挨打，物体可能会越震越大回不去了
            containerRect.localScale = Vector3.one;

            _punchTween = containerRect.DOPunchScale(Vector3.one * punchScale, punchDuration, punchVibrato, punchElasticity);
        }

        // 更新记录，为下一次判断做准备
        _currentFillAmount = targetFill;
    }
}