using UnityEngine;
using UnityEngine.AI;

public class EnemyMovement : MonoBehaviour
{
    public Transform target;

    NavMeshAgent agent;
    Animator anim;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponent<Animator>();
    }

    void Update()
    {
        if (target == null) return;

        agent.SetDestination(target.position);

        bool moving = agent.velocity.magnitude > 0.1f;
        anim.SetBool("IsMoving", moving);
    }
}