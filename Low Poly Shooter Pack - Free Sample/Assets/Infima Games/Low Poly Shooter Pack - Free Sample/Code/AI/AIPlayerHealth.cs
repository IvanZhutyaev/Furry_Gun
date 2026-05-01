using UnityEngine;
using UnityEngine.Events;

namespace InfimaGames.LowPolyShooterPack.AI
{
    public sealed class AIPlayerHealth : MonoBehaviour
    {
        [SerializeField] private float maxHealth = 100.0f;
        [SerializeField] private bool resetOnDeath = true;
        [SerializeField] private float resetDelay = 1.0f;

        public UnityEvent onDeath;

        private float currentHealth;
        private bool dead;

        public float GetCurrentHealth() => currentHealth;
        public bool IsAlive() => !dead;

        private void Awake()
        {
            currentHealth = maxHealth;
        }

        public void ApplyDamage(float amount)
        {
            if (amount <= 0.0f || dead)
                return;

            currentHealth = Mathf.Max(0.0f, currentHealth - amount);
            if (currentHealth > 0.0f)
                return;

            dead = true;
            onDeath?.Invoke();

            if (resetOnDeath)
                Invoke(nameof(ResetHealth), resetDelay);
        }

        private void ResetHealth()
        {
            currentHealth = maxHealth;
            dead = false;
        }
    }
}
