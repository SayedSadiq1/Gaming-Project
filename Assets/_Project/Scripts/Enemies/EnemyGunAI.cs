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
    [Tooltip("Drag the EnemyWeaponMount child here. When assigned it overrides the fields below.")]
    public EnemyWeaponMount weapon;

    [Header("Shooting (fallback — used only when no weapon is assigned)")]
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
        agent.updateRotation = false;
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

        UpdateMovingAnimation();
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
    }

    void StopMoving()
    {
        agent.isStopped = true;
    }

    void UpdateMovingAnimation()
    {
        if (anim != null)
            anim.SetBool("IsMoving", agent.velocity.magnitude > 0.1f);
    }

    void FaceTarget()
    {
        Vector3 direction = target.position - transform.position;
        direction.y = 0;

        if (direction == Vector3.zero) return;

        Quaternion lookRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, lookRotation, 360f * Time.deltaTime);
    }

    void Shoot()
    {
        if (Time.time < nextFireTime) return;
        if (agent.velocity.magnitude > 0.5f) return;

        nextFireTime = Time.time + fireRate;

        if (anim != null)
            anim.SetTrigger("Shoot");

        Vector3 targetPoint = target.position + Vector3.up * 1.1f;

        if (weapon != null)
        {
            // Use the mounted weapon — it handles bullet, flash, and sound.
            weapon.Fire(targetPoint);
        }
        else if (firePoint != null && projectilePrefab != null)
        {
            // Fallback: old direct-instantiate method.
            Vector3 dir = (targetPoint - firePoint.position).normalized;
            Instantiate(projectilePrefab, firePoint.position, Quaternion.LookRotation(dir));
        }
    }
}