using UnityEngine;
using UnityEngine.AI;

public class enemyAI : MonoBehaviour
{
    Transform target;
    NavMeshAgent agent;
    public float LookRadius;
    public Animator anim;
    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth;

    private bool isAttacking = false;
    private bool isDead = false;

    private void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            target = player.transform;

        agent = GetComponent<NavMeshAgent>();
        currentHealth = maxHealth;
    }

    void Update()
    {
        if (isDead)
            return;

        if (!agent.isOnNavMesh || target == null) return;

        float distance = Vector3.Distance(target.position, transform.position);

        if (distance <= LookRadius)
        {
            if (distance <= agent.stoppingDistance)
            {
                // Останавливаем агента
                agent.isStopped = true;
                agent.ResetPath();

                // Атакуем (только один раз включаем анимацию)
                if (!isAttacking)
                {
                    isAttacking = true;
                    anim.SetBool("isAttack", true);
                    anim.SetBool("isRun", false);
                }
            }
            else
            {
                // Возобновляем движение
                agent.isStopped = false;
                agent.SetDestination(target.position);

                // Бежим
                isAttacking = false;
                anim.SetBool("isAttack", false);
                anim.SetBool("isRun", true);
            }

            LookTarget();
        }
        else
        {
            // Игрок далеко — стоим на месте
            agent.isStopped = true;
            agent.ResetPath();
            isAttacking = false;
            anim.SetBool("isAttack", false);
            anim.SetBool("isRun", false);
        }
    }

    private void LookTarget()
    {
        if (target == null) return;

        Vector3 direction = target.position - transform.position;
        direction.y = 0;

        if (direction.magnitude < 0.1f) return;

        Quaternion lookRotation = Quaternion.LookRotation(direction);
        float rotationSpeed = 5f;

        transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * rotationSpeed);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, LookRadius);
    }

    public void TakeDamage(float damage)
    {
        if (isDead)
            return;

        currentHealth = Mathf.Max(0f, currentHealth - damage);

        if (currentHealth <= 0f)
            Die();
    }

    private void Die()
    {
        isDead = true;

        if (agent != null)
        {
            agent.isStopped = true;
            if (agent.isOnNavMesh)
                agent.ResetPath();
        }

        if (anim != null)
        {
            anim.SetBool("isAttack", false);
            anim.SetBool("isRun", false);
        }

        Destroy(gameObject);
    }
}