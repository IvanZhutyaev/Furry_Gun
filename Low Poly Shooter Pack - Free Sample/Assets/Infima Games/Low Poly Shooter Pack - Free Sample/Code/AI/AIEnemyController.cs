using UnityEngine;

namespace InfimaGames.LowPolyShooterPack.AI
{
    public sealed class AIEnemyController : MonoBehaviour
    {
        private enum EnemyState
        {
            Patrol,
            Search,
            Chase,
            Attack,
            Dead
        }

        [Header("References")]
        [SerializeField] private AIEnemyPerception perception;
        [SerializeField] private AIEnemyCombat combat;
        [SerializeField] private AIEnemyHealth health;
        [SerializeField] private AIEnemyPatrolPoint[] patrolPoints;

        [Header("Movement")]
        [SerializeField] private float patrolSpeed = 2.0f;
        [SerializeField] private float chaseSpeed = 4.0f;
        [SerializeField] private float rotationSpeed = 7.0f;
        [SerializeField] private float stoppingDistance = 1.5f;
        [SerializeField] private float attackDistance = 20.0f;
        [SerializeField] private float searchDuration = 3.0f;

        private EnemyState state = EnemyState.Patrol;
        private Transform target;
        private int patrolIndex;
        private float searchTimer;

        private void Awake()
        {
            if (perception == null)
                perception = GetComponent<AIEnemyPerception>();

            if (combat == null)
                combat = GetComponent<AIEnemyCombat>();

            if (health == null)
                health = GetComponent<AIEnemyHealth>();

            if (health != null)
                health.onDeath.AddListener(OnDeath);
        }

        private void Start()
        {
            target = ServiceLocator.Current.Get<IGameModeService>().GetPlayerCharacter().transform;
        }

        private void Update()
        {
            if (state == EnemyState.Dead || health == null || !health.IsAlive)
                return;

            bool seesTarget = perception != null && perception.TryDetectTarget(target, out float distanceToTarget);
            if (seesTarget)
            {
                state = distanceToTarget <= attackDistance ? EnemyState.Attack : EnemyState.Chase;
                searchTimer = searchDuration;
            }
            else if (state == EnemyState.Attack || state == EnemyState.Chase)
            {
                state = EnemyState.Search;
            }

            switch (state)
            {
                case EnemyState.Patrol:
                    TickPatrol();
                    break;
                case EnemyState.Search:
                    TickSearch();
                    break;
                case EnemyState.Chase:
                    TickChase();
                    break;
                case EnemyState.Attack:
                    TickAttack();
                    break;
            }
        }

        private void TickPatrol()
        {
            if (patrolPoints == null || patrolPoints.Length == 0)
                return;

            Transform patrolTarget = patrolPoints[patrolIndex].transform;
            MoveTowards(patrolTarget.position, patrolSpeed);

            if (Vector3.Distance(transform.position, patrolTarget.position) <= stoppingDistance)
                patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
        }

        private void TickSearch()
        {
            searchTimer -= Time.deltaTime;
            if (searchTimer <= 0.0f)
                state = EnemyState.Patrol;
        }

        private void TickChase()
        {
            if (target == null)
                return;

            MoveTowards(target.position, chaseSpeed);
        }

        private void TickAttack()
        {
            if (target == null)
                return;

            Vector3 look = target.position - transform.position;
            look.y = 0.0f;
            if (look.sqrMagnitude > Mathf.Epsilon)
            {
                Quaternion desired = Quaternion.LookRotation(look.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, desired, Time.deltaTime * rotationSpeed);
            }

            if (Vector3.Distance(transform.position, target.position) > attackDistance)
            {
                state = EnemyState.Chase;
                return;
            }

            if (combat != null)
                combat.TryAttack(target);
        }

        private void MoveTowards(Vector3 destination, float speed)
        {
            Vector3 toDestination = destination - transform.position;
            toDestination.y = 0.0f;

            if (toDestination.sqrMagnitude <= stoppingDistance * stoppingDistance)
                return;

            Vector3 direction = toDestination.normalized;
            transform.position += direction * (speed * Time.deltaTime);

            if (direction.sqrMagnitude > Mathf.Epsilon)
            {
                Quaternion desired = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, desired, Time.deltaTime * rotationSpeed);
            }
        }

        public void SetPatrolPoints(AIEnemyPatrolPoint[] points)
        {
            patrolPoints = points;
            patrolIndex = 0;
        }

        private void OnDeath()
        {
            state = EnemyState.Dead;
        }
    }
}
