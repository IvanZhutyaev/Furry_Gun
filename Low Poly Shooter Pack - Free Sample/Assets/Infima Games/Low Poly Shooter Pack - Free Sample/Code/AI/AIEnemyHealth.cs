using UnityEngine;
using UnityEngine.Events;

namespace InfimaGames.LowPolyShooterPack.AI
{
    public sealed class AIEnemyHealth : MonoBehaviour
    {
        [Header("Health")]
        [SerializeField] private float healthPoints = 100.0f;
        [SerializeField] private float damageMultiplier = 1.0f;
        [SerializeField] private AIEnemyHealth parentHealth;

        [Header("Death")]
        [SerializeField] private bool disableGameObjectOnDeath = true;

        public UnityEvent onDeath;

        private float lastDamage;
        private int lastDamageFrame = -1;
        private bool dead;

        private void Awake()
        {
            onDeath ??= new UnityEvent();
        }

        public bool IsAlive => !dead && healthPoints > 0.0f;
        public float GetHealthPoints() => healthPoints;

        public void TakeDamage(float amount)
        {
            TakeDamage(amount, Time.frameCount);
        }

        private void TakeDamage(float amount, int damageFrame)
        {
            if (amount <= 0.0f)
                return;

            amount *= damageMultiplier;

            if (lastDamageFrame == damageFrame)
            {
                if (amount <= lastDamage)
                    return;

                TakeDamage(-lastDamage, damageFrame);
            }

            lastDamage = amount;
            lastDamageFrame = damageFrame;

            if (parentHealth != null)
            {
                parentHealth.TakeDamage(amount);
                return;
            }

            if (!IsAlive)
                return;

            healthPoints -= amount;
            if (healthPoints > 0.0f)
                return;

            healthPoints = 0.0f;
            dead = true;
            onDeath?.Invoke();

            if (disableGameObjectOnDeath)
                gameObject.SetActive(false);
        }
    }
}
