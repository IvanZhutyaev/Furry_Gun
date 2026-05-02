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

    [Header("Melee / animator")]
    [Tooltip("Short name состояния с клипом удара на Base Layer (в enemyAnimController — «attack»). Каждый новый заход в это состояние = ещё один возможный хит.")]
    [SerializeField]
    private string attackAnimatorStateShortName = "attack";

    [SerializeField]
    private int attackAnimatorLayer;

    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth;

    [Header("Death")]
    [Tooltip("Через сколько секунд удалять тело с сцены после регдолла.")]
    [SerializeField] private float corpseDestroyDelay = 45f;

    private bool isAttacking;
    /// <summary>Последние значения bool в Animator — не дёргаем каждый кадр без нужды.</summary>
    private bool lastAnimAttack;
    private bool lastAnimRun;
    private bool isDead;

    /// <summary>Один урон за текущее «окно» атаки до сброса (новая атака или AnimEvent).</summary>
    private bool meleeHitConsumed;

    private int attackStateShortNameHash;
    private bool animatorWasInAttackState;
    private int meleeSwingNormalizedCycle = int.MinValue;

    private void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            target = player.transform;

        agent = GetComponent<NavMeshAgent>();
        ConfigurePhysicsForNavMeshCharacter();
        currentHealth = maxHealth;
        attackStateShortNameHash = Animator.StringToHash(attackAnimatorStateShortName);
    }

    /// <summary>
    /// Совмещение NavMeshAgent + неподвижный kinematic RB: без этого тело катится под гравитацией/физикой.
    /// </summary>
    private void ConfigurePhysicsForNavMeshCharacter()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
            return;

        rb.isKinematic = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.None;
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
    }

    private void LateUpdate()
    {
        if (isDead || anim == null)
            return;

        AnimatorStateInfo st = anim.GetCurrentAnimatorStateInfo(attackAnimatorLayer);
        bool inAttackMotion = st.shortNameHash == attackStateShortNameHash;

        if (!inAttackMotion)
        {
            animatorWasInAttackState = false;
            meleeSwingNormalizedCycle = int.MinValue;
            return;
        }

        int cycle = Mathf.FloorToInt(st.normalizedTime);

        // Первый кадр в состоянии attack или новый цикл клипа при loop — можно снова одно попадание.
        if (!animatorWasInAttackState || cycle > meleeSwingNormalizedCycle)
            meleeHitConsumed = false;

        animatorWasInAttackState = true;
        meleeSwingNormalizedCycle = cycle;
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

        lastAnimAttack = attack;
        lastAnimRun = run;
        anim.SetBool("isAttack", attack);
        anim.SetBool("isRun", run);
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

    /// <returns>Урон возможен только в момент проигрывания состояния удара в Animator.</returns>
    public bool TryConsumeMeleeHitOnce()
    {
        if (isDead || !isAttacking || meleeHitConsumed || anim == null)
            return false;

        AnimatorStateInfo st = anim.GetCurrentAnimatorStateInfo(attackAnimatorLayer);
        if (st.shortNameHash != attackStateShortNameHash)
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
        if (isDead)
            return;

        isDead = true;
        enabled = false;

        var rootRb = GetComponent<Rigidbody>();
        var capsule = GetComponent<CapsuleCollider>();

        EnemyDeathRagdoll.Activate(anim, agent, rootRb, capsule);

        Destroy(gameObject, Mathf.Max(1f, corpseDestroyDelay));
    }
}