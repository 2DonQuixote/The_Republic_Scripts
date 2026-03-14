using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 【行为芯片】巡逻组件：仅在未发现玩家 (Idle状态) 时运作，支持随机散步和固定路线。
/// 🔥 纯动画驱动版 (Root Motion)：移除了代码限速，怪物走多快完全取决于散步动画的步伐！
/// </summary>
[RequireComponent(typeof(EnemyBrain))]
public class PatrolBehavior : MonoBehaviour
{
    public enum PatrolMode { RandomWander, Waypoints }

    [Header("=== 🚶 巡逻模式 ===")]
    public PatrolMode patrolMode = PatrolMode.RandomWander;
    // ❌ 已经彻底移除 patrolSpeed！一切交由动画的 Root Motion 接管！

    [Header("=== 🎲 随机巡逻设置 ===")]
    public float wanderRadius = 10f;
    public float minWaitTime = 2.0f;
    public float maxWaitTime = 5.0f;

    [Header("=== 📍 固定路径设置 (空物体路标) ===")]
    [Tooltip("把场景里的空物体拖进这个列表里")]
    public List<Transform> waypoints = new List<Transform>();
    public float waypointWaitTime = 2.0f;
    [Tooltip("勾选：A->B->C->B->A (往返) \n不勾选：A->B->C->A (绕圈)")]
    public bool pingPong = false;

    private EnemyBrain brain;
    private Vector3 startingPosition;
    private int currentWaypointIndex = 0;
    private bool isWaiting = false;
    private bool goingForward = true;

    private void Awake()
    {
        brain = GetComponent<EnemyBrain>();
        startingPosition = transform.position; // 记录出生点
    }

    private void Start()
    {
        if (brain.currentState == EnemyBrain.BrainState.Idle)
        {
            SetNextDestination();
        }
    }

    private void Update()
    {
        // 核心拦截：只要进入了战斗，巡逻芯片立刻彻底休眠！
        if (brain.currentState != EnemyBrain.BrainState.Idle)
        {
            if (isWaiting)
            {
                StopAllCoroutines();
                isWaiting = false;
            }
            return;
        }

        // --- 非战斗 (Peace) 状态下的行为 ---
        // 注意：这里什么都不用设置！只要有了寻路目标，大脑会自动获取方向传给混合树，动画接管移动。

        if (!isWaiting && !brain.Agent.pathPending && brain.Agent.remainingDistance <= brain.Agent.stoppingDistance + 0.1f)
        {
            StartCoroutine(WaitAndSetNextDestination());
        }

        // 平滑转头看向路径的下一个拐点
        if (brain.Agent.hasPath)
        {
            brain.FaceTarget(brain.Agent.steeringTarget);
        }
    }

    private IEnumerator WaitAndSetNextDestination()
    {
        isWaiting = true;

        // 🔥 动画驱动优化：不要用 velocity = Vector3.zero 强行清空物理速度！
        // 而是直接清除寻路路径。大脑感知到没路了，会自动通过平滑过渡让动画切回发呆！
        brain.Agent.ResetPath();

        // 站在原地东张西望等几秒
        float waitT = patrolMode == PatrolMode.RandomWander ? Random.Range(minWaitTime, maxWaitTime) : waypointWaitTime;
        yield return new WaitForSeconds(waitT);

        // 休息够了，继续出发
        SetNextDestination();

        isWaiting = false;
    }

    private void SetNextDestination()
    {
        if (patrolMode == PatrolMode.RandomWander)
        {
            // 在出生点附近随机找个点
            Vector3 randomDir = Random.insideUnitSphere * wanderRadius;
            randomDir += startingPosition;
            NavMeshHit hit;
            if (NavMesh.SamplePosition(randomDir, out hit, wanderRadius, 1))
            {
                brain.Agent.SetDestination(hit.position);
            }
        }
        else if (patrolMode == PatrolMode.Waypoints && waypoints.Count > 0)
        {
            Transform targetPoint = waypoints[currentWaypointIndex];
            if (targetPoint != null)
            {
                brain.Agent.SetDestination(targetPoint.position);
            }

            if (pingPong)
            {
                if (goingForward)
                {
                    currentWaypointIndex++;
                    if (currentWaypointIndex >= waypoints.Count)
                    {
                        currentWaypointIndex = waypoints.Count - 2;
                        goingForward = false;
                        if (currentWaypointIndex < 0) currentWaypointIndex = 0;
                    }
                }
                else
                {
                    currentWaypointIndex--;
                    if (currentWaypointIndex < 0)
                    {
                        currentWaypointIndex = 1;
                        goingForward = true;
                        if (currentWaypointIndex >= waypoints.Count) currentWaypointIndex = 0;
                    }
                }
            }
            else
            {
                // 绕圈
                currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Count;
            }
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (patrolMode == PatrolMode.RandomWander)
        {
            Gizmos.color = new Color(0, 1, 0, 0.3f);
            Gizmos.DrawWireSphere(Application.isPlaying ? startingPosition : transform.position, wanderRadius);
        }
        else if (patrolMode == PatrolMode.Waypoints && waypoints != null)
        {
            Gizmos.color = Color.green;
            for (int i = 0; i < waypoints.Count; i++)
            {
                if (waypoints[i] != null)
                {
                    Gizmos.DrawSphere(waypoints[i].position, 0.3f);
                    if (i > 0 && waypoints[i - 1] != null)
                    {
                        Gizmos.DrawLine(waypoints[i - 1].position, waypoints[i].position);
                    }
                }
            }
            if (!pingPong && waypoints.Count > 1 && waypoints[0] != null && waypoints[waypoints.Count - 1] != null)
            {
                Gizmos.DrawLine(waypoints[waypoints.Count - 1].position, waypoints[0].position);
            }
        }
    }
#endif
}