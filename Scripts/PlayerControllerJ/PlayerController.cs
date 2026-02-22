using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections; // 引用协程需要的命名空间

[RequireComponent(typeof(Rigidbody), typeof(Animator))]
public class PlayerController : MonoBehaviour
{
    [Header("=== 核心配置 ===")]
    // 🔥 这里拖入你的 WeaponItem (例如 Sword_Beginner)
    public WeaponItem currentWeapon;

    // 🔥 右手骨骼挂载点 (记得在Inspector里把 Hand_R 拖进去！)
    [Header("=== 模型挂载点 ===")]
    public Transform rightHandTransform;

    [Header("=== 移动参数 ===")]
    public float walkSpeed = 5f;
    public float runSpeed = 8f;
    public float crouchSpeed = 2.5f;
    public float rotateSpeed = 15f;
    public bool useRootMotion = true;

    [Header("=== 通用手感配置 ===")]
    [Tooltip("输入缓存时间(秒): 防止按键太快吞指令")]
    public float inputBufferTime = 0.8f;

    [Header("=== 翻滚配置 ===")]
    public float rollForce = 15f;
    public float rollDuration = 0.6f;
    public float rollDrag = 3f;
    public float rollCooldown = 1.0f;
    [Tooltip("翻滚转攻击允许的时间点 (0.7 = 翻滚动作播放70%后可出刀)")]
    [Range(0f, 1f)] public float rollAttackWindow = 0.7f;

    // --- 内部状态 ---
    private GameControls controls;
    private Vector2 moveInput;
    private Rigidbody rb;
    private Animator animator;

    // 武器模型引用
    private GameObject currentWeaponModel;

    // [已删除] private WeaponDamageHandler currentWeaponHandler; <-- 删掉了这个

    // 状态标记
    private bool isCrouching;
    private bool isRunning;
    private bool isRolling;
    private bool isAttacking;
    private bool canMove = true;

    // 战斗状态
    private int comboCount = 0; // 当前连击段数
    private bool currentAttackIsHeavy = false;

    // 计时器
    private float lastInputTime = -100f;
    private float lastAttackStartTime;
    private float rollStartTime;
    private float lastRollTime = -100f;
    private float defaultDrag;
    private bool bufferedInputIsHeavy = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
        defaultDrag = rb.drag;

        // 初始化 Input System
        controls = new GameControls();
        SetupInput();
    }

    private void Start()
    {
        // 游戏开始自动装备武器
        EquipWeapon(currentWeapon);
    }

    // ================= 核心：武器装备逻辑 =================

    public void EquipWeapon(WeaponItem newWeapon)
    {
        currentWeapon = newWeapon;

        // 1. 切换动画控制器
        if (currentWeapon != null && currentWeapon.weaponAnimator != null)
        {
            animator.runtimeAnimatorController = currentWeapon.weaponAnimator;
        }

        // 2. 生成武器模型
        if (currentWeaponModel != null)
        {
            Destroy(currentWeaponModel);
        }

        if (currentWeapon != null && currentWeapon.modelPrefab != null)
        {
            if (rightHandTransform != null)
            {
                // 生成新模型
                currentWeaponModel = Instantiate(currentWeapon.modelPrefab, rightHandTransform);
                currentWeaponModel.transform.localPosition = Vector3.zero;
                currentWeaponModel.transform.localRotation = Quaternion.identity;
                currentWeaponModel.transform.localScale = Vector3.one;

                // [已删除] 获取 Handler 的逻辑删掉了，因为现在不需要给模型挂脚本了
            }
            else
            {
                Debug.LogError("请在 PlayerController 组件里把 'Right Hand Transform' 拖进去！");
            }
        }

        // 切换武器时重置状态
        ResetCombo();
        CancelInvoke(nameof(ResetCombo));
    }

    // ================= 核心：输入与更新 =================

    private void SetupInput()
    {
        controls.Gameplay.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        controls.Gameplay.Move.canceled += ctx => moveInput = Vector2.zero;

        controls.Gameplay.Run.performed += ctx => isRunning = true;
        controls.Gameplay.Run.canceled += ctx => isRunning = false;

        controls.Gameplay.Crouch.performed += ctx => ToggleCrouch(!isCrouching);

        controls.Gameplay.Roll.performed += ctx => HandleRollInput();

        // 攻击输入缓存
        controls.Gameplay.Attack.performed += ctx => BufferAttackInput(false);
        controls.Gameplay.HeavyAttack.performed += ctx => BufferAttackInput(true);
    }

    private void OnEnable() => controls.Gameplay.Enable();
    private void OnDisable() => controls.Gameplay.Disable();

    private void Update()
    {
        if (!canMove) return;
        // 只有这里每帧检查输入缓存，决定是否出招
        CheckBufferedInput();
    }

    private void FixedUpdate()
    {
        if (!canMove) return;
        // 攻击或翻滚时锁定移动（除非使用了 RootMotion）
        if (isRolling || isAttacking) return;

        HandleMovement();
        HandleRotation();
    }

    // ================= 核心：战斗逻辑 =================

    private void BufferAttackInput(bool isHeavy)
    {
        if (!canMove) return;

        // 如果正在翻滚，要看是否到了可取消的窗口期
        if (!isRolling || (Time.time - rollStartTime >= rollDuration * rollAttackWindow))
        {
            lastInputTime = Time.time;
            bufferedInputIsHeavy = isHeavy;
        }
    }

    private void CheckBufferedInput()
    {
        // 检查输入是否过期
        bool hasBufferedInput = (Time.time - lastInputTime) < inputBufferTime;
        if (!hasBufferedInput) return;

        if (isRolling)
        {
            // 翻滚攻击
            if ((Time.time - rollStartTime) >= rollDuration * rollAttackWindow)
            {
                ExecuteAttack(bufferedInputIsHeavy);
                lastInputTime = -100f; // 消耗掉输入
            }
        }
        else if (CanAttackNow())
        {
            // 正常连招
            ExecuteAttack(bufferedInputIsHeavy);
            lastInputTime = -100f; // 消耗掉输入
        }
    }

    private bool CanAttackNow()
    {
        if (isRolling) return false;
        if (!isAttacking) return true; // 没攻击当然可以攻击

        if (currentWeapon == null) return false;

        // 如果正在攻击，检查是否到了连招窗口期 (Combo Window)
        AttackAction currentAction = GetCurrentActionData();
        if (currentAction == null) return true;

        float timePlayed = Time.time - lastAttackStartTime;
        // 比如动作全长 1秒，WindowStart=0.6，那么 0.6秒后才能出下一刀
        return timePlayed >= (currentAction.totalDuration * currentAction.comboWindowStart);
    }

    private void ExecuteAttack(bool isHeavy)
    {
        if (currentWeapon == null) return;

        // 1. 状态准备：只要出刀，就取消“连招重置倒计时”
        CancelInvoke(nameof(ResetCombo));
        PrepareAttackState();
        currentAttackIsHeavy = isHeavy;

        AttackAction action = null;

        // 2. 获取动作数据
        if (isHeavy)
        {
            comboCount = 0; // 重击通常是单独动作
            action = currentWeapon.GetHeavyAttack(0);
        }
        else
        {
            // 轻击：防止数组越界
            if (comboCount >= currentWeapon.lightAttacks.Count) comboCount = 0;
            action = currentWeapon.GetLightAttack(comboCount);
        }

        if (action == null)
        {
            OnAttackEnd();
            return;
        }

        // 3. 播放动画
        animator.CrossFade(action.animName, action.transitionDuration);

        // 4. 🔥 [伤害判定] 启动协程
        // 传入整个 action 数据，方便协程里读取 delay(前摇) 和 radius/angle(扇形数据)
        StartCoroutine(EnableDamageWithDelay(action));

        // 5. 设置硬直结束时间
        Invoke(nameof(OnAttackEnd), action.totalDuration);

        // 6. 连招计数加一 (为下一刀做准备)
        if (!isHeavy) comboCount++;
    }

    // 🔥🔥🔥【重构版】基于数学计算的扇形判定协程 🔥🔥🔥
    IEnumerator EnableDamageWithDelay(AttackAction action)
    {
        // 1. 【前摇阶段】等待前摇时间
        if (action.damageDelay > 0)
        {
            yield return new WaitForSeconds(action.damageDelay);
        }

        // 2. 检查状态：如果被打断（isAttacking变成false），就不判定伤害了
        if (!isAttacking)
        {
            yield break;
        }

        // 3. 🔥【执行判定】直接调用扇形检测 (一击判定)
        PerformSectorAttack(action);

        // 以前的 DisableDamage 之类的全都不需要了
    }

    // 🔥🔥🔥【新增】扇形检测核心逻辑 🔥🔥🔥
    private void PerformSectorAttack(AttackAction action)
    {
        // 1. 以玩家为中心，画一个球，把所有碰到的敌人找出来
        // 这里的 LayerMask 默认检测所有层，你可以改成 LayerMask.GetMask("Enemy") 来优化性能
        Collider[] hits = Physics.OverlapSphere(transform.position, action.attackRadius);

        foreach (var hit in hits)
        {
            // 排除自己
            if (hit.gameObject == gameObject) continue;

            // 排除非 IDamageable 物体
            IDamageable target = hit.GetComponent<IDamageable>();
            if (target == null) continue;

            // 2. 算角度：敌人在我前方多少度？
            Vector3 dirToTarget = (hit.transform.position - transform.position).normalized;

            // 既然是扇形，我们只关心水平面上的角度，忽略高度差
            dirToTarget.y = 0;
            Vector3 myForward = transform.forward;
            myForward.y = 0;

            // 计算夹角 (0到180度)
            float angle = Vector3.Angle(myForward, dirToTarget);

            // 3. 判定：如果在扇形角度内的一半 (因为 Angle 算的是中线往两边的偏角)
            // 比如扇形是90度，那么只要偏角小于45度就算在里面
            if (angle <= action.attackAngle * 0.5f)
            {
                // 命中！造成伤害
                target.TakeDamage(action.damageMultiplier);

                // 生成打击特效
                if (action.hitVFX != null)
                {
                    // 在敌人位置稍微高一点的地方生成
                    Instantiate(action.hitVFX, hit.transform.position + Vector3.up, Quaternion.identity);
                }
            }
        }
    }

    private AttackAction GetCurrentActionData()
    {
        if (currentWeapon == null) return null;
        if (currentAttackIsHeavy) return currentWeapon.GetHeavyAttack(0);

        // 此时 comboCount 已经加过1了，所以要查当前动作得减1
        int index = Mathf.Clamp(comboCount - 1, 0, currentWeapon.lightAttacks.Count - 1);
        if (comboCount == 0) index = 0; // 还没打第一下

        return currentWeapon.GetLightAttack(index);
    }

    private void PrepareAttackState()
    {
        // 如果是从翻滚打断过来的
        if (isRolling)
        {
            isRolling = false;
            rb.drag = defaultDrag;
            CancelInvoke(nameof(OnRollEnd));
            // [已删除] DisableDamage 调用
        }

        FaceMouseInstant(); // 攻击瞬间朝向鼠标

        isAttacking = true;
        rb.velocity = Vector3.zero; // 攻击时停止滑步
        lastAttackStartTime = Time.time;

        CancelInvoke(nameof(OnAttackEnd)); // 取消之前的结束回调
    }

    // ================= 状态回调 =================

    // 动作做完了 (或者 Invoke 时间到了)
    public void OnAttackEnd()
    {
        isAttacking = false;

        // [已删除] DisableDamage 调用，现在协程跑完自动就结束了，没有状态残留

        // 开启“连招中断倒计时”，比如2秒内不打下一刀，连招归零
        Invoke(nameof(ResetCombo), currentWeapon.comboResetTime);
    }

    private void ResetCombo()
    {
        comboCount = 0;
    }

    // ================= 翻滚逻辑 =================

    private void HandleRollInput()
    {
        if (!canMove) return;
        if (Time.time < lastRollTime + rollCooldown) return;
        if (isRolling) return;

        // 攻击中也可以翻滚（取消后摇）
        if (isAttacking)
        {
            AttackAction currentAction = GetCurrentActionData();
            if (currentAction != null)
            {
                float timePlayed = Time.time - lastAttackStartTime;
                // 如果还没到“允许翻滚点”，则不能滚 (比如刚抬手不能滚)
                if (timePlayed < currentAction.totalDuration * currentAction.rollCancelStartTime)
                {
                    return;
                }
            }
        }

        PerformRoll();
    }

    private void PerformRoll()
    {
        isAttacking = false;

        // [已删除] DisableDamage 调用
        // 翻滚打断攻击时，因为协程里有 `if(!isAttacking) yield break;`
        // 所以正在等待的伤害判定也会自动取消，非常安全。

        ResetCombo();
        CancelInvoke(nameof(OnAttackEnd));

        isRolling = true;
        lastRollTime = Time.time;
        rollStartTime = Time.time;

        animator.CrossFade("Roll", 0.1f);
        rb.drag = rollDrag; // 增加阻力，让翻滚停得更自然

        // 确定翻滚方向
        Vector3 rollDir = transform.forward;
        if (moveInput.magnitude > 0.1f)
            rollDir = new Vector3(moveInput.x, 0, moveInput.y).normalized;

        transform.rotation = Quaternion.LookRotation(rollDir);
        rb.AddForce(rollDir * rollForce, ForceMode.Impulse);

        Invoke(nameof(OnRollEnd), rollDuration);
    }

    public void OnRollEnd()
    {
        isRolling = false;
        rb.drag = defaultDrag;
    }

    // ================= 基础移动 =================

    private void HandleMovement()
    {
        if (moveInput.magnitude > 0.1f)
        {
            float currentSpeed = isCrouching ? crouchSpeed : (isRunning ? runSpeed : walkSpeed);
            Vector3 targetDir = new Vector3(moveInput.x, 0, moveInput.y).normalized;
            rb.MovePosition(rb.position + targetDir * currentSpeed * Time.fixedDeltaTime);

            float animSpeed = isRunning ? 1f : 0.5f;
            if (isCrouching) animSpeed = 0.5f;
            animator.SetFloat("Speed", moveInput.magnitude * animSpeed, 0.1f, Time.fixedDeltaTime);
        }
        else
        {
            animator.SetFloat("Speed", 0, 0.1f, Time.fixedDeltaTime);
        }
    }

    private void HandleRotation()
    {
        if (moveInput.magnitude > 0.1f)
        {
            Vector3 targetDir = new Vector3(moveInput.x, 0, moveInput.y).normalized;
            rb.rotation = Quaternion.Slerp(rb.rotation, Quaternion.LookRotation(targetDir), rotateSpeed * Time.fixedDeltaTime);
        }
    }

    private void FaceMouseInstant()
    {
        if (Camera.main == null) return;
        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (new Plane(Vector3.up, new Vector3(0, transform.position.y, 0)).Raycast(ray, out float enter))
        {
            Vector3 lookDir = (ray.GetPoint(enter) - transform.position).normalized;
            lookDir.y = 0;
            if (lookDir != Vector3.zero) rb.rotation = Quaternion.LookRotation(lookDir);
        }
    }

    private void ToggleCrouch(bool state)
    {
        isCrouching = state;
        animator.SetBool("IsCrouch", isCrouching);
    }

    // 启用 RootMotion 时处理位移
    private void OnAnimatorMove()
    {
        if ((isAttacking || isRolling) && useRootMotion && canMove)
        {
            Vector3 newPos = rb.position + animator.deltaPosition;
            newPos.y = rb.position.y;
            rb.MovePosition(newPos);
        }
    }

    // ... (上面的代码保持不变)

    // 🔥🔥🔥【新增】可视化调试辅助线 🔥🔥🔥
    private void OnDrawGizmosSelected()
    {
        // 如果没有武器，就不画了
        if (currentWeapon == null) return;

        // 为了方便调试，默认画出“轻攻击第一下”的范围
        // 如果你正在攻击，就画出“当前动作”的范围
        AttackAction displayAction = null;

        if (isAttacking)
        {
            displayAction = GetCurrentActionData();
        }
        else
        {
            // 没攻击时，默认显示轻攻击第一下，方便你在编辑器里调参数
            displayAction = currentWeapon.GetLightAttack(0);
        }

        if (displayAction == null) return;

        // 1. 设置颜色 (半透明红色)
        Gizmos.color = new Color(1, 0, 0, 0.3f);

        // 2. 画出攻击半径 (圆球)
        Gizmos.DrawWireSphere(transform.position, displayAction.attackRadius);

        // 3. 画出扇形的两条边
        Vector3 forward = transform.forward;
        Quaternion leftRayRotation = Quaternion.AngleAxis(-displayAction.attackAngle * 0.5f, Vector3.up);
        Quaternion rightRayRotation = Quaternion.AngleAxis(displayAction.attackAngle * 0.5f, Vector3.up);

        Vector3 leftRay = leftRayRotation * forward;
        Vector3 rightRay = rightRayRotation * forward;

        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position, leftRay * displayAction.attackRadius);
        Gizmos.DrawRay(transform.position, rightRay * displayAction.attackRadius);
    }


    // ==========================================
    // 🔥 外部硬控接口：被投机抓取
    // ==========================================
    public void ApplyGrab(float duration)
    {
        // 1. 锁死玩家输入
        canMove = false;

        // 2. 强行打断当前正在进行的攻击或翻滚
        isAttacking = false;
        isRolling = false;
        ResetCombo();
        CancelInvoke(); // 取消所有等待中的后摇回调

        // 3. 物理急刹车：防止玩家带着惯性滑走
        if (rb != null) rb.velocity = Vector3.zero;

        // 4. 播放被咬动画 (开启硬控开关)
        if (animator != null) animator.SetBool("IsGrabbed", true);

        // 5. 开启解绑倒计时
        StartCoroutine(GrabRecoverCoroutine(duration));

        Debug.Log($"<color=red>玩家被投技命中！失去控制 {duration} 秒！</color>");
    }

    private IEnumerator GrabRecoverCoroutine(float duration)
    {
        // 乖乖等怪物咬完
        yield return new WaitForSeconds(duration);

        // 恢复自由
        canMove = true;
        if (animator != null) animator.SetBool("IsGrabbed", false);

        Debug.Log("<color=green>玩家挣脱投技，恢复控制！</color>");
    }

} 