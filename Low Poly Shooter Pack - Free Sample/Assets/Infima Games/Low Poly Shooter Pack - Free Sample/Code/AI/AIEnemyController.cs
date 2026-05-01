using UnityEngine;

namespace InfimaGames.LowPolyShooterPack.AI
{
    public sealed class AIEnemyController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private AIEnemyPerception perception;
        [SerializeField] private AIEnemyCombat combat;
        [SerializeField] private AIEnemyHealth health;

        [Header("Movement")]
        [SerializeField] private float chaseSpeed = 4.0f;
        [SerializeField] private float rotationSpeed = 7.0f;
        [SerializeField] private float stoppingDistance = 1.1f;
        [SerializeField] private float attackDistance = 30.0f;
        [SerializeField] private float forceAggroDistance = 60.0f;

        private Transform target;
        private Rigidbody cachedRigidbody;

        private void Awake()
        {
            if (perception == null)
                perception = GetComponent<AIEnemyPerception>();

            if (combat == null)
                combat = GetComponent<AIEnemyCombat>();

            if (health == null)
                health = GetComponent<AIEnemyHealth>();

            cachedRigidbody = GetComponent<Rigidbody>();
            if (cachedRigidbody == null)
                cachedRigidbody = gameObject.AddComponent<Rigidbody>();
            cachedRigidbody.useGravity = false;
            cachedRigidbody.isKinematic = true;
            cachedRigidbody.constraints = RigidbodyConstraints.FreezeRotation;

            if (health != null)
            {
                health.onDeath ??= new UnityEngine.Events.UnityEvent();
                health.onDeath.AddListener(OnDeath);
            }
        }

        private void Start()
        {
            if (target == null)
                ReacquireTarget();
        }

        private void Update()
        {
            if (health == null || !health.IsAlive)
                return;

            if (target == null)
                ReacquireTarget();

            if (target == null || combat == null)
                return;

            Vector3 look = target.position - transform.position;
            look.y = 0.0f;
            if (look.sqrMagnitude > Mathf.Epsilon)
            {
                Quaternion desired = Quaternion.LookRotation(look.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, desired, Time.deltaTime * rotationSpeed);
            }

            float distance = Vector3.Distance(transform.position, target.position);
            bool seesTarget = perception == null || perception.TryDetectTarget(target, out _);
            bool aggressive = seesTarget || distance <= forceAggroDistance;
            if (!aggressive)
                return;

            if (distance > attackDistance)
            {
                MoveTowards(target.position, chaseSpeed);
                return;
            }

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

        public void SetTarget(Transform value)
        {
            target = value;
        }

        private void ReacquireTarget()
        {
            CharacterBehaviour playerCharacter = null;
            if (ServiceLocator.Current != null)
                playerCharacter = ServiceLocator.Current.Get<IGameModeService>()?.GetPlayerCharacter();

            if (playerCharacter != null)
            {
                target = playerCharacter.transform;
                return;
            }

            // Last-resort fallback for scene setups where services are not available yet.
            GameObject taggedPlayer = GameObject.FindWithTag("Player");
            if (taggedPlayer != null)
                target = taggedPlayer.transform;
        }

        private void OnDeath()
        {
            enabled = false;
        }
    }
}
