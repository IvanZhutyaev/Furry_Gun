using UnityEngine;
using UnityEngine.AI;

public class enemyAI : MonoBehaviour
{
    Transform target;
    NavMeshAgent agent;
    public float LookRadius;
    public Animator anim;

    [Header("Melee / chase")]
    [Tooltip("Добавляется к stoppingDistance агента: пока игрок не дальше этой суммы, враг остаётся в режиме атаки. Снимает дребезг у границы радиуса и обрывы анимации удара.")]
    [SerializeField] private float chaseResumeExtra = 0.85f;

    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth;

    private bool isAttacking;
    /// <summary>Последние значения bool в Animator — не дёргаем каждый кадр без нужды.</summary>
    private bool lastAnimAttack;
    private bool lastAnimRun;
    private bool isDead;

    /// <summary>Один урон за текущее «окно» атаки до сброса (новая атака или AnimEvent).</summary>
    private bool meleeHitConsumed;

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

        if (!agent.isOnNavMesh || target == null || anim == null)
            return;

        float distance = Vector3.Distance(target.position, transform.position);
        float meleeEnter = agent.stoppingDistance;
        float meleeExit = meleeEnter + chaseResumeExtra;

        if (distance <= LookRadius)
        {
            // Гистерезис: войти в атаку у stoppingDistance; выйти в бег только если игрок заметно дальше stoppingDistance.
            bool inMelee = distance <= meleeEnter;
            bool farEnoughToChase = distance > meleeExit;

            if (inMelee || (isAttacking && !farEnoughToChase))
            {
                agent.isStopped = true;
                agent.ResetPath();

                isAttacking = true;
                SetAnimatorCombat(true, false);
            }
            else
            {
                agent.isStopped = false;
                agent.SetDestination(target.position);

                isAttacking = false;
                SetAnimatorCombat(false, true);
            }

            LookTarget();
        }
        else
        {
            // Игрок далеко — стоим на месте
            agent.isStopped = true;
            agent.ResetPath();
            isAttacking = false;
            SetAnimatorCombat(false, false);
        }
    }

    private void SetAnimatorCombat(bool attack, bool run)
    {
        if (attack == lastAnimAttack && run == lastAnimRun)
            return;

        bool wasAttack = lastAnimAttack;
        lastAnimAttack = attack;
        lastAnimRun = run;
        anim.SetBool("isAttack", attack);
        anim.SetBool("isRun", run);

        if (attack && !wasAttack)
            meleeHitConsumed = false;

        if (!attack && wasAttack)
            meleeHitConsumed = false;
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

    /// <summary>
    /// Сбрасывает флаг попадания (Animation Event — начало активных кадров удара), чтобы снова можно было нанести урон 1 раз за этот замах.
    /// </summary>
    public void AnimEvent_MeleeSwingStart()
    {
        meleeHitConsumed = false;
    }

    /// <returns>Можно нанести урон триггером кулака именно сейчас — одна успешная попытка на окно атаки.</returns>
    public bool TryConsumeMeleeHitOnce()
    {
        if (isDead || !isAttacking || meleeHitConsumed)
            return false;

        meleeHitConsumed = true;
        return true;
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