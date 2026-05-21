using UnityEngine;
using UnityEngine.AI;
using Unity.FPS.Game;

public class EnemyGunAI : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("Sight")]
    public Transform eyes;
    public float viewDistance = 20f;
    public float viewAngle = 90f;
    public LayerMask obstacleMask;

    [Header("Movement")]
    public float walkSpeed = 2f;
    public float stoppingDistance = 8f;

    [Header("Shooting")]
    public Transform firePoint;
    public GameObject projectilePrefab;
    public float fireRate = 1f;
    public float damage = 10f;

    private NavMeshAgent agent;
    private Animator anim;
    private float nextFireTime;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponent<Animator>();

        agent.speed = walkSpeed;
        agent.stoppingDistance = stoppingDistance;
    }

    void Update()
    {
        if (target == null) return;

        bool canSeePlayer = CanSeeTarget();

        if (canSeePlayer)
        {
            float distance = Vector3.Distance(transform.position, target.position);

            FaceTarget();

            if (distance > stoppingDistance)
            {
                MoveToPlayer();
            }
            else
            {
                StopMoving();
                Shoot();
            }
        }
        else
        {
            StopMoving();
        }
    }

    bool CanSeeTarget()
    {
        Vector3[] targetPoints =
        {
        target.position + Vector3.up * 1.7f, // head
        target.position + Vector3.up * 1.1f, // chest
        target.position + Vector3.up * 0.5f  // lower body
    };

        foreach (Vector3 point in targetPoints)
        {
            Vector3 directionToTarget = (point - eyes.position).normalized;
            float distanceToTarget = Vector3.Distance(eyes.position, point);

            if (distanceToTarget > viewDistance)
                continue;

            float angle = Vector3.Angle(transform.forward, directionToTarget);

            if (angle > viewAngle / 2f)
                continue;

            if (Physics.Raycast(eyes.position, directionToTarget, out RaycastHit hit, viewDistance))
            {
                if (hit.transform == target || hit.transform.IsChildOf(target))
                {
                    return true;
                }
            }
        }

        return false;
    }

    void MoveToPlayer()
    {
        agent.isStopped = false;
        agent.SetDestination(target.position);

        if (anim != null)
            anim.SetBool("IsMoving", true);
    }

    void StopMoving()
    {
        agent.isStopped = true;

        if (anim != null)
            anim.SetBool("IsMoving", false);
    }

    void FaceTarget()
    {
        Vector3 direction = target.position - transform.position;
        direction.y = 0;

        if (direction == Vector3.zero) return;

        Quaternion lookRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 5f);
    }

    void Shoot()
    {
        if (Time.time < nextFireTime) return;

        nextFireTime = Time.time + fireRate;

        if (anim != null)
        {
            anim.SetTrigger("Shoot");
        }

        Vector3 targetPoint = target.position + Vector3.up * 1.1f;
        Vector3 shootDirection = (targetPoint - firePoint.position).normalized;

        if (projectilePrefab != null)
        {
            Quaternion bulletRotation = Quaternion.LookRotation(shootDirection);
            Instantiate(projectilePrefab, firePoint.position, bulletRotation);
        }

        Debug.Log("Enemy fired projectile");
    }
}