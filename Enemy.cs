using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Enemy : MonoBehaviour
{
    private CharacterController enemyController;
    private Vector3 spawnLocation;

    [Header("追逐/攻击玩家检测")]//*******平台抽成太贵，加我v : yiyiyi__ee
    [SerializeField] private Transform targetObject;
    [SerializeField] private float chaseStartDistance;
    [SerializeField] private float chaseStopDistance;
    [SerializeField] private bool isNeverChase;
    [SerializeField] private float chaseTime;
    private bool chasingPlayer;
    [SerializeField] private float attackStartDistance;
    [SerializeField] private float attackStopDistance;
    private bool tryAttackPlayer;
    private bool canAttackPlayer;
    private bool attackingPlayer;
    [SerializeField] private float facingPlayerAngleThreshold;
    private bool facingPlayer;

    [Header("攻击")]
    [SerializeField] private Transform attackSpawnPoint;

    [SerializeField] private float attackTime;
    private float attackTimeDelta;

    [Header("漫游运动控制")]
    [SerializeField] private float roamingRange;
    [SerializeField] private bool isClosePlayer;//Ture围绕玩家漫游，Flase四处漫游
    [SerializeField] private float roamingUpdateMinTime;
    [SerializeField] private float roamingUpdateMaxTime;
    private float destinationUpdateTime;
    private float destinationUpdateTimeLeft;
    [SerializeField] private float roamingMinSpeed;
    [SerializeField] private float roamingMaxSpeed;
    private float velocityUpdateTime;
    private float velocityUpdateTimeLeft;

    [Header("追逐运动控制")]
    [SerializeField] private float chasingMaxSpeed;
    [SerializeField] private float chasingCompleteDistance;
    private float currentMaxSpeed;
    private float currentTargetSpeed;
    private float currentDistanceToTarget;

    [Header("整体运动控制")]
    [SerializeField] private float slowdownStartDistance;
    [SerializeField] private float speedChangeRate;
    [SerializeField] private float rotateSpeed;
    [SerializeField] private float facingTargetSpeed;
    private float currentSpeed;
    private Vector3 currentTarget;
    private Vector3 currentTargetDirection;
    private Vector3 currentDirection;


    [Header("障碍检测探针")]
    [SerializeField] private Transform obstacleDetectionRef;
    [SerializeField] private Transform startPoint;
    [SerializeField] private Transform forward;
    [SerializeField] private Transform left;
    [SerializeField] private Transform right;
    [SerializeField] private Transform up;
    [SerializeField] private Transform down;
    [SerializeField] private LayerMask obstacleLayerMask;
    [SerializeField] private float rayStartDistance;
    [SerializeField] private float detectionRayMinLength;
    [SerializeField] private float sideAvoidCoefficient;
    [SerializeField] private float rotateAvoidCoefficient;
    private float detectionRayLength;
    private Ray forwardRay;
    private RaycastHit forwardRayHit;
    private Ray leftRay;
    private RaycastHit leftRayHit;
    private Ray rightRay;
    private RaycastHit rightRayHit;
    private Ray upRay;
    private RaycastHit upRayHit;
    private Ray downRay;
    private RaycastHit downRayHit;
    private bool forwardObstacle;
    private bool leftObstacle;
    private bool rightObstacle;
    private bool upObstacle;
    private bool downObstacle;

    private Vector3 targetTempDestination;
    private Vector3 sideAvoidTempDestination;
    private Vector3 rotateAvoidTempDestination;
    private Vector3 currentTargetDestination;

    [Header("冲刺运动控制")]
    [SerializeField] public bool isSprint;// 是否冲刺 
    [SerializeField] public float sprintSpeed;// 冲刺速度
    [SerializeField] public float sprintDistance;// 冲刺距离

    private void Awake()
    {
        enemyController = GetComponent<CharacterController>();
        enemyController.radius = rayStartDistance;
        startPoint.localPosition = new Vector3(0, 0, rayStartDistance);

        spawnLocation = transform.position;

        //共三种模式选择：冲刺后跟随、冲刺、跟随、漫游
        if(isNeverChase && isSprint)
        {
            int v = Random.Range(0, 2);
            if (v == 0)
                targetObject = GameObject.FindWithTag("PlayerUpLeft").transform;
            else if(v == 1)
                targetObject = GameObject.FindWithTag("PlayerUpRight").transform;
            else
                targetObject = GameObject.FindWithTag("PlayerForward").transform;
        }
        else if (!isNeverChase && isSprint)
        {
            int v = Random.Range(0, 1);
            if (v == 0)
                targetObject = GameObject.FindWithTag("PlayerDown").transform;
            else
                targetObject = GameObject.FindWithTag("PlayerUp").transform;
        }
        else if (isNeverChase)
        {
            targetObject = GameObject.FindWithTag("PlayerForward").transform;
        }
        else
        {
            targetObject = GameObject.FindWithTag("Player").transform;
        }

    }
    void Update()
    {
        PlayerCheck();
        if (isNeverChase && isSprint)
        {
            ChasingAndSprint(); Debug.Log("1");
        }
        else if(isSprint && !isNeverChase)
        {
            Sprint(); Debug.Log("2");
        }
        else if (chasingPlayer && !isNeverChase && chaseTime>0)
        {
            Chasing();
            chaseTime -= Time.deltaTime; Debug.Log("3");
        }
        else
        {
            isNeverChase = true;
            Roaming(); Debug.Log("4");
        }

        CalculateCurrentSpeed();

        CastObstacleDetectionRay();
        GetTargetTempDestination();
        GetSideAvoidTempDestination();
        GetRotateAvoidTempDestination();

        GetCurrentDirection();

        enemyController.Move(currentDirection * currentSpeed * Time.deltaTime);


        if (tryAttackPlayer)
        {
            transform.forward = new Vector3(Mathf.Lerp(transform.forward.x, (targetObject.position - transform.position).normalized.x, Time.deltaTime * facingTargetSpeed), Mathf.Lerp(transform.forward.y, (targetObject.position - transform.position).normalized.y, Time.deltaTime * facingTargetSpeed), Mathf.Lerp(transform.forward.z, (targetObject.position - transform.position).normalized.z, Time.deltaTime * facingTargetSpeed));
        }
        else
        {
            transform.forward = currentDirection;
        }

        OutPlayerVision();
    }
    private void PlayerCheck()
    {
        //计算怪物与玩家的距离
        float playerDistance = (transform.position - targetObject.position).magnitude;
        //计算是否追逐玩家
        if (playerDistance < chaseStartDistance && !chasingPlayer)
        {
            chasingPlayer = true;
        }
        if (playerDistance > chaseStopDistance && chasingPlayer)
        {
            chasingPlayer = false;
        }
        //计算是否攻击玩家
        if (playerDistance < attackStartDistance && !tryAttackPlayer)
        {
            tryAttackPlayer = true;
        }
        if (playerDistance > attackStopDistance && tryAttackPlayer)
        {
            tryAttackPlayer = false;
        }
        if (Vector3.Angle((targetObject.position - transform.position), transform.forward) < facingPlayerAngleThreshold)
        {
            facingPlayer = true;
        }
        else
        {
            facingPlayer = false;
        }
        if (facingPlayer)
        {
            canAttackPlayer = true;
        }
        else
        {
            canAttackPlayer = false;
        }
        if (canAttackPlayer && tryAttackPlayer)
        {
            attackingPlayer = true;
        }
        else
        {
            attackingPlayer = false;
        }
        if (attackingPlayer)
        {
            if (attackTimeDelta > 0)
            {
                attackTimeDelta -= Time.deltaTime;
            }
            else
            {
                attackTimeDelta = attackTime;
                SpawnAttackProjectile();
            }
        }
        else
        {
            attackTimeDelta = 0;
        }
    }
    private void SpawnAttackProjectile()
    {
        Vector3 attackDirection = (targetObject.position - attackSpawnPoint.position).normalized;
        
    }

    /// <summary>
    /// 漫游
    /// </summary>
    private void Roaming()
    {
        if (destinationUpdateTimeLeft > 0)
        {
            destinationUpdateTimeLeft -= Time.deltaTime;
        }
        else
        {
            destinationUpdateTime = Random.Range(roamingUpdateMinTime, roamingUpdateMaxTime);
            destinationUpdateTimeLeft = destinationUpdateTime;

            Vector3 randomDirection;
            do
            {
                randomDirection = transform.TransformDirection(Random.onUnitSphere);
            } while (randomDirection.z < 0f); // 只接受z坐标大于等于0的方向向量

            if (isClosePlayer)
            {
                currentTarget = spawnLocation + ((randomDirection + (targetObject.position - spawnLocation).normalized+ (targetObject.position - spawnLocation).normalized)) * roamingRange;
            }
            else
            {
                currentTarget = spawnLocation + randomDirection * roamingRange;
            }
        }
        // Vector3 currentTargetDirection = currentTargetDestination - transform.position;
        // if ( Vector3.Angle(currentDirection, currentTargetDirection) < 1)
        // {
        //     currentTarget = spawnLocation + Random.insideUnitSphere * roamingRange;
        // }

        if (velocityUpdateTimeLeft > 0)
        {
            velocityUpdateTimeLeft -= Time.deltaTime;
        }
        else
        {
            velocityUpdateTime = Random.Range(roamingUpdateMinTime, roamingUpdateMaxTime);
            velocityUpdateTimeLeft = velocityUpdateTime;
            currentMaxSpeed = Random.Range(roamingMinSpeed, roamingMaxSpeed);
        }
    }
    /// <summary>
    /// 追踪
    /// </summary>
    private void Chasing()
    {
        currentTarget = (transform.position - targetObject.position).normalized * chasingCompleteDistance + targetObject.position;
        currentMaxSpeed = chasingMaxSpeed;
    }

    /// <summary>
    /// 冲刺
    /// </summary>
    private void Sprint()
    {
        currentTarget = (targetObject.position - transform.position).normalized * sprintDistance + targetObject.position;
        currentMaxSpeed = sprintSpeed;
        if(IsOrientationSimilar(transform,targetObject.transform,60f))
        {
            MidniteOilSoftware.ObjectPoolManager.DespawnGameObject(gameObject);
        }
    }

    /// <summary>
    /// 冲刺后追踪
    /// </summary>
    private void ChasingAndSprint()
    {
        if (Vector3.Distance(targetObject.position,transform.position)<80f)
        {
            targetObject = GameObject.FindWithTag("Player").transform;
            isSprint = false;
            isNeverChase = true;
        }
        else
        {
            currentTarget = (targetObject.position - transform.position).normalized * sprintDistance + targetObject.position;
            currentMaxSpeed = sprintSpeed;
        }
        
    }

    /// <summary>
    /// 计算当前速度
    /// </summary>
    private void CalculateCurrentSpeed()
    {
        currentDistanceToTarget = (currentTarget - transform.position).magnitude;
        if (currentDistanceToTarget > slowdownStartDistance)
        {
            currentTargetSpeed = currentMaxSpeed;
        }
        else
        {
            currentTargetSpeed = currentMaxSpeed * currentDistanceToTarget / slowdownStartDistance;
        }
        currentSpeed = Mathf.Lerp(currentSpeed, currentTargetSpeed, Time.deltaTime * speedChangeRate);
    }
    /// <summary>
    /// 投射检测障碍物的射线
    /// </summary>
    private void CastObstacleDetectionRay()
    {
        obstacleDetectionRef.transform.forward = currentDirection;

        forwardRay = new Ray(startPoint.position, forward.position - startPoint.position);
        leftRay = new Ray(startPoint.position, left.position - startPoint.position);
        rightRay = new Ray(startPoint.position, right.position - startPoint.position);
        upRay = new Ray(startPoint.position, up.position - startPoint.position);
        downRay = new Ray(startPoint.position, down.position - startPoint.position);

        if (enemyController.velocity.magnitude > detectionRayMinLength)
        {
            detectionRayLength = enemyController.velocity.magnitude;
        }
        else
        {
            detectionRayLength = detectionRayMinLength;
        }

        if (Physics.Raycast(forwardRay, out forwardRayHit, detectionRayLength, obstacleLayerMask))
        {
            forwardObstacle = true;
        }
        else
        {
            forwardObstacle = false;
        }
        if (Physics.Raycast(leftRay, out leftRayHit, detectionRayLength, obstacleLayerMask))
        {
            leftObstacle = true;
        }
        else
        {
            leftObstacle = false;
        }
        if (Physics.Raycast(rightRay, out rightRayHit, detectionRayLength, obstacleLayerMask))
        {
            rightObstacle = true;
        }
        else
        {
            rightObstacle = false;
        }
        if (Physics.Raycast(upRay, out upRayHit, detectionRayLength, obstacleLayerMask))
        {
            upObstacle = true;
        }
        else
        {
            upObstacle = false;
        }
        if (Physics.Raycast(downRay, out downRayHit, detectionRayLength, obstacleLayerMask))
        {
            downObstacle = true;
        }
        else
        {
            downObstacle = false;
        }
    }
    /// <summary>
    /// 获取到达终点的临时目标
    /// </summary>
    private void GetTargetTempDestination()
    {
        targetTempDestination = (currentTarget - transform.position).normalized * detectionRayLength + transform.position;
    }
    /// <summary>
    /// 获取避开临时目的地的向量
    /// </summary>
    private void GetSideAvoidTempDestination()
    {
        sideAvoidTempDestination = transform.position;
        if (leftObstacle)
        {
            sideAvoidTempDestination -= (detectionRayLength - leftRayHit.distance) * leftRay.direction * sideAvoidCoefficient;
        }
        if (rightObstacle)
        {
            sideAvoidTempDestination -= (detectionRayLength - rightRayHit.distance) * rightRay.direction * sideAvoidCoefficient;
        }
        if (downObstacle)
        {
            sideAvoidTempDestination -= (detectionRayLength - downRayHit.distance) * downRay.direction * sideAvoidCoefficient;
        }
        if (upObstacle)
        {
            sideAvoidTempDestination -= (detectionRayLength - upRayHit.distance) * upRay.direction * sideAvoidCoefficient;
        }
    }
    /// <summary>
    /// 获取避开障碍到达临时目的地所产生的旋转值
    /// </summary>
    private void GetRotateAvoidTempDestination()
    {
        rotateAvoidTempDestination = transform.position;
        if (forwardObstacle)
        {
            if (!rightObstacle)
            {
                rotateAvoidTempDestination -= leftRay.direction * (detectionRayLength - forwardRayHit.distance) * rotateAvoidCoefficient;
            }
            else if (!leftObstacle)
            {
                rotateAvoidTempDestination -= rightRay.direction * (detectionRayLength - forwardRayHit.distance) * rotateAvoidCoefficient;
            }
            else if (!upObstacle)
            {
                rotateAvoidTempDestination -= downRay.direction * (detectionRayLength - forwardRayHit.distance) * rotateAvoidCoefficient;
            }
            else if (!downObstacle)
            {
                rotateAvoidTempDestination -= upRay.direction * (detectionRayLength - forwardRayHit.distance) * rotateAvoidCoefficient;
            }
        }
    }
    /// <summary>
    /// 获取目前的方向
    /// </summary>
    private void GetCurrentDirection()
    {
        if (forwardObstacle)
        {
            currentTargetDestination = transform.position + (sideAvoidTempDestination - transform.position) + (rotateAvoidTempDestination - transform.position);
        }
        else
        {
            currentTargetDestination = transform.position + (targetTempDestination - transform.position) + (sideAvoidTempDestination - transform.position);
        }
        currentTargetDirection = (currentTargetDestination - transform.position).normalized;
        currentDirection = new Vector3(Mathf.Lerp(currentDirection.x, currentTargetDirection.x,Time.deltaTime * rotateSpeed),Mathf.Lerp(currentDirection.y, currentTargetDirection.y,Time.deltaTime * rotateSpeed),Mathf.Lerp(currentDirection.z, currentTargetDirection.z,Time.deltaTime * rotateSpeed));
    }

    /// <summary>
    /// 玩家失去视野后应该消失,离得太远也要消失
    /// </summary>
    private void OutPlayerVision()
    {
        if (/*(!isNeverChase && IsOrientationSimilar(transform, targetObject.transform, 40f) && Vector3.Distance(transform.position, targetObject.transform.position) < 60f)*/
              Vector3.Distance(transform.position, targetObject.transform.position) > 800f
            || Vector3.Distance(transform.position, targetObject.transform.position) < 20f)
        {
            MidniteOilSoftware.ObjectPoolManager.DespawnGameObject(gameObject);
        }

    }

    /// <summary>
    /// 判断两个物体的朝向是否一致
    /// </summary>
    /// <param name="transform1">一个物体</param>
    /// <param name="transform2">另一个物体</param>
    /// <param name="angleTreshold">设置一个阈值，用于判断朝向的相似性</param>
    /// <returns></returns>
    private bool IsOrientationSimilar(Transform transform1, Transform transform2, float angleTreshold)
    {
        // 计算两个向量之间的夹角
        float angle = Vector3.Angle(transform1.forward, transform2.forward);
        
        return angle <= angleTreshold;
    }

    private void OnDrawGizmos()
    {
        //显示实时目的点
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, currentTarget);

        //显示生成原点
        Gizmos.DrawSphere(spawnLocation, 1);

        //显示实时方向向量
        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position, transform.position + currentDirection * 100);


        //显示侦测障碍射线
        if (forwardObstacle)
        {
            Gizmos.color = Color.red;
        }
        else
        {
            Gizmos.color = Color.black;
        }
        Gizmos.DrawLine(forwardRay.origin, forwardRay.GetPoint(detectionRayLength));
        if (leftObstacle)
        {
            Gizmos.color = Color.red;
        }
        else
        {
            Gizmos.color = Color.black;
        }
        Gizmos.DrawLine(leftRay.origin, leftRay.GetPoint(detectionRayLength));
        if (rightObstacle)
        {
            Gizmos.color = Color.red;
        }
        else
        {
            Gizmos.color = Color.black;
        }
        Gizmos.DrawLine(rightRay.origin, rightRay.GetPoint(detectionRayLength));
        if (upObstacle)
        {
            Gizmos.color = Color.red;
        }
        else
        {
            Gizmos.color = Color.black;
        }
        Gizmos.DrawLine(upRay.origin, upRay.GetPoint(detectionRayLength));
        if (downObstacle)
        {
            Gizmos.color = Color.red;
        }
        else
        {
            Gizmos.color = Color.black;
        }
        Gizmos.DrawLine(downRay.origin, downRay.GetPoint(detectionRayLength));

        Gizmos.color = Color.red;
        Gizmos.DrawSphere(targetTempDestination, 2);
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(sideAvoidTempDestination, 2);
        Gizmos.color = Color.blue;
        Gizmos.DrawSphere(rotateAvoidTempDestination, 2);
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(currentTargetDestination, 1);
        Gizmos.DrawSphere(currentTarget, 2);

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(startPoint.position, forward.position);
        Gizmos.color = Color.green;
        Gizmos.DrawLine(startPoint.position, left.position);
        Gizmos.DrawLine(startPoint.position, right.position);
        Gizmos.DrawLine(startPoint.position, up.position);
        Gizmos.DrawLine(startPoint.position, down.position);


    }
}
