using UnityEngine;
using UnityEngine.AI;
using System.Collections;

// 🔥 纯正的外挂配件：只继承 MonoBehaviour
[RequireComponent(typeof(BaseEnemy))]
public class RangedAddon : MonoBehaviour
{
    [Header("=== 🏹 远程外挂配置 ===")]
    public GameObject projectilePrefab;     // 毒球预制体
    public Transform throwPoint;            // 发射点 (手部骨骼)

    [Tooltip("最小射程：玩家距离小于此值时，外挂休眠，把控制权还给近战老父亲")]
    public float minRange = 3.0f;
    [Tooltip("最大射程：在这个距离内才扔毒球")]
    public float maxRange = 10.0f;

    public float cooldown = 4.0f;
    public float throwEndDelay = 1.0f;      // 扔完发呆的硬直，防平移

    private float _timer = 0f;
    private bool _isThrowing = false;       // 🔥 新增：标记是否正在执行投掷动作

    // 依赖的身体组件
    private BaseEnemy _baseAI;
    private NavMeshAgent _agent;
    private Animator _anim;
    private Transform _player;

    private void Start()
    {
        _baseAI = GetComponent<BaseEnemy>();
        _agent = GetComponent<NavMeshAgent>();
        _anim = GetComponentInChildren<Animator>();

        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) _player = p.transform;
    }

    private void Update()
    {
        if (_baseAI == null || _player == null) return;

        // 如果怪物死了，直接罢工
        if (_baseAI.currentState == BaseEnemy.AIState.Dead) return;

        // 冷却计时
        if (_timer > 0) _timer -= Time.deltaTime;

        // 🔥 核心冲突避免：
        // 如果老父亲正在施展近战动作 (Attack 状态)，或者我们自己正在播投掷动画，绝不插手！
        if ((_baseAI.currentState == BaseEnemy.AIState.Attack && !_baseAI.isAIHijacked) || _isThrowing) return;

        // 测距
        float dist = Vector3.Distance(transform.position, _player.position);

        // 🔥 判断是否在“远程甜点距离”内
        if (dist >= minRange && dist <= maxRange)
        {
            // 1. 🔒 持续劫持大脑！只要在这个圈里，老父亲就别想接管身体
            _baseAI.isAIHijacked = true;

            // 2. 踩死物理刹车（原地站桩）
            if (_agent != null && _agent.isActiveAndEnabled)
            {
                _agent.velocity = Vector3.zero;
                _agent.isStopped = true;
            }

            // 3. 转身一直盯着玩家看
            FaceTarget(_player.position);

            // 4. 如果冷却完毕，丢毒球！
            if (_timer <= 0)
            {
                ExecuteThrow();
            }
        }
        else
        {
            // 🔥 如果玩家跑出了远程范围（太近了，或者逃得太远了）
            // 如果此时大脑还在被我们劫持，赶紧把控制权还给老父亲！
            if (_baseAI.isAIHijacked)
            {
                _baseAI.isAIHijacked = false; // 🔓 解除劫持！
                if (_agent != null && _agent.isActiveAndEnabled) _agent.isStopped = false;
            }
        }
    }

    private void ExecuteThrow()
    {
        _isThrowing = true; // 标记开始投掷
        _timer = cooldown;

        // 播放丢毒动画
        if (_anim != null) _anim.SetTrigger("Throw");

        // 开启硬直等待协程
        StartCoroutine(ThrowWaitCoroutine());
    }

    private IEnumerator ThrowWaitCoroutine()
    {
        // 等待动画后摇结束
        yield return new WaitForSeconds(throwEndDelay);

        // 投掷动作彻底结束
        _isThrowing = false;

        // 💡 注意：这里不再像以前那样无脑解除劫持了！
        // 而是交给 Update 去判断玩家是否还在远程圈子里。
    }

    // ==========================================
    // 🎯 供 Animation Event 调用的生成方法
    // ==========================================
    public void SpawnProjectile()
    {
        if (projectilePrefab == null || throwPoint == null || _player == null) return;

        GameObject ball = Instantiate(projectilePrefab, throwPoint.position, throwPoint.rotation);
        var script = ball.GetComponent<PoisonProjectile>();

        if (script != null)
        {
            // 🔥 直接把玩家当前踩着的地面坐标，作为落点传给毒球
            script.LaunchToPoint(_player.position);
        }
    }

    private void FaceTarget(Vector3 targetPos)
    {
        Vector3 dir = (targetPos - transform.position).normalized;
        dir.y = 0;
        if (dir != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(dir);
        }
    }
}