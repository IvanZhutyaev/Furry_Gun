using UnityEngine;
using UnityEngine.AI;

public class enemyAI : MonoBehaviour
{
    private enum DeathStyle
    {
        FlatFall = 0,
        Ragdoll = 1,
        PartialRagdoll = 2
    }
    Transform target;
    NavMeshAgent agent;
    public float LookRadius;
    public Animator anim;

    [Header("Melee / chase")]
    [Tooltip("Добавляется к stoppingDistance агента: пока игрок не дальше этой суммы, враг остаётся в режиме атаки. Снимает дребезг у границы радиуса и обрывы анимации удара.")]
    [SerializeField] private float chaseResumeExtra = 0.25f;
    [Tooltip("Короткая задержка перед стартом движения, чтобы анимация бега успела начаться.")]
    [SerializeField] private float runAnimationLeadTime = 0.08f;

    [Header("Melee / animator")]
    [Tooltip("Short name состояния с клипом удара на Base Layer (в enemyAnimController — «attack»). Каждый новый заход в это состояние = ещё один возможный хит.")]
    [SerializeField]
    private string attackAnimatorStateShortName = "attack";

    [SerializeField]
    private int attackAnimatorLayer;
    [Tooltip("Минимальный интервал между любыми успешными мили-попаданиями этого врага.")]
    [SerializeField] private float meleeGlobalHitCooldown = 0.35f;

    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth;

    [Header("Death")]
    [SerializeField] private DeathStyle deathStyle = DeathStyle.FlatFall;
    [Tooltip("Через сколько секунд удалять тело с сцены после регдолла.")]
    [SerializeField] private float corpseDestroyDelay = 4f;
    private const float CorpseDestroyDelayHard = 4f;

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
    private float meleeFallbackNextHitTime;
    private float meleeGlobalNextHitTime;
    private bool wasChasingLastFrame;
    private float runMovementUnlockTime;

    /// <summary>Игрок нанёс урон с любой дистанции — преследуем как при входе в LookRadius.</summary>
    private bool aggroFromDamage;

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

    private void EnsurePlayerTarget()
    {
        if (target != null)
            return;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            target = player.transform;
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

        if (distance <= LookRadius || aggroFromDamage)
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
                wasChasingLastFrame = false;
            }
            else
            {
                isAttacking = false;
                SetAnimatorCombat(false, true);

                // При переходе в бег даём Animator один короткий шаг, чтобы ноги не "залипали" на месте.
                if (!wasChasingLastFrame)
                    runMovementUnlockTime = Time.unscaledTime + Mathf.Max(0f, runAnimationLeadTime);

                bool canMoveNow = Time.unscaledTime >= runMovementUnlockTime;
                if (canMoveNow)
                {
                    agent.isStopped = false;
                    agent.SetDestination(target.position);
                }
                else
                {
                    agent.isStopped = true;
                    agent.ResetPath();
                }

                wasChasingLastFrame = true;
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
            wasChasingLastFrame = false;
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
        // Disabled for strict one-hit-per-attack-state logic.
        // Reset happens only on animator state enter/new cycle in LateUpdate.
    }

    /// <returns>Урон возможен только в момент проигрывания состояния удара в Animator.</returns>
    public bool TryConsumeMeleeHitOnce()
    {
        if (isDead || !isAttacking || meleeHitConsumed || anim == null || Time.unscaledTime < meleeGlobalNextHitTime)
            return false;

        AnimatorStateInfo st = anim.GetCurrentAnimatorStateInfo(attackAnimatorLayer);
        if (st.shortNameHash != attackStateShortNameHash)
            return false;

        meleeHitConsumed = true;
        meleeGlobalNextHitTime = Time.unscaledTime + Mathf.Max(0.05f, meleeGlobalHitCooldown);
        return true;
    }

    /// <summary>
    /// Общая проверка нанесения урона: сначала штатный animator-hit, затем fallback с общим кулдауном на врага.
    /// </summary>
    public bool TryConsumeMeleeHit(float fallbackInterval)
    {
        if (isDead || !isAttacking)
            return false;

        // Если атака корректно распознана в Animator, урон только через "один раз за swing".
        // Это убирает второй хит на обратном движении руки.
        if (anim != null)
        {
            AnimatorStateInfo st = anim.GetCurrentAnimatorStateInfo(attackAnimatorLayer);
            bool inAttackMotion = st.shortNameHash == attackStateShortNameHash;
            if (inAttackMotion)
                return TryConsumeMeleeHitOnce();
        }

        // Fallback используется только когда attack-state не распознан (сбитый layer/name/контроллер).
        float nextAllowedTime = Mathf.Max(meleeFallbackNextHitTime, meleeGlobalNextHitTime);
        if (Time.unscaledTime < nextAllowedTime)
            return false;

        meleeFallbackNextHitTime = Time.unscaledTime + Mathf.Max(0.05f, fallbackInterval);
        meleeGlobalNextHitTime = Time.unscaledTime + Mathf.Max(0.05f, meleeGlobalHitCooldown);
        return true;
    }

    public void TakeDamage(float damage)
    {
        if (isDead)
            return;

        if (damage > 0f)
        {
            EnsurePlayerTarget();
            aggroFromDamage = true;
        }

        currentHealth = Mathf.Max(0f, currentHealth - damage);

        if (currentHealth <= 0f)
            Die();
    }

    public void ConfigureBossStats(float health, float lookRadius, float meleeDamage, float moveSpeed, float stoppingDistance)
    {
        maxHealth = Mathf.Max(1f, health);
        currentHealth = maxHealth;
        LookRadius = Mathf.Max(1f, lookRadius);

        NavMeshAgent nav = GetComponent<NavMeshAgent>();
        if (nav != null)
        {
            nav.speed = Mathf.Max(0.1f, moveSpeed);
            nav.stoppingDistance = Mathf.Max(0f, stoppingDistance);
        }

        sphereTriggerDamage[] triggers = GetComponentsInChildren<sphereTriggerDamage>(true);
        for (int i = 0; i < triggers.Length; i++)
        {
            if (triggers[i] == null)
                continue;
            triggers[i].SetDamage(meleeDamage);
            triggers[i].RefreshOwnerCache();
        }
    }

    private void Die()
    {
        if (isDead)
            return;

        isDead = true;
        PlayerProgression.RegisterEnemyKill();
        enabled = false;
        aggroFromDamage = false;

        var rootRb = GetComponent<Rigidbody>();
        var capsule = GetComponent<CapsuleCollider>();

        if (deathStyle == DeathStyle.FlatFall)
            EnemyDeathRagdoll.ActivateFlatFall(anim, agent, rootRb, capsule);
        else if (deathStyle == DeathStyle.PartialRagdoll)
            EnemyDeathRagdoll.ActivatePartial(anim, agent, rootRb, capsule);
        else
            EnemyDeathRagdoll.Activate(anim, agent, rootRb, capsule);

        // Unity сериализует старые значения в сценах/префабах, поэтому фиксируем 4 секунды жёстко.
        Destroy(gameObject, CorpseDestroyDelayHard);
    }
}