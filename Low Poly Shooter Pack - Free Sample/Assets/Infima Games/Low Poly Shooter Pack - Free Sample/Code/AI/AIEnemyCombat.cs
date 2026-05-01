using UnityEngine;
using System;

namespace InfimaGames.LowPolyShooterPack.AI
{
    public sealed class AIEnemyCombat : MonoBehaviour
    {
        [Header("Combat")]
        [SerializeField] private Transform firePoint;
        [SerializeField] private float fireRate = 4.0f;
        [SerializeField] private float range = 35.0f;
        [SerializeField] private float damage = 12.0f;
        [SerializeField] private LayerMask hitMask = ~0;

        private float fireCooldown;

        public void SetFirePoint(Transform point)
        {
            firePoint = point;
        }

        public bool CanAttack(Transform target)
        {
            if (target == null)
                return false;

            if (firePoint == null)
                firePoint = transform;

            float distance = Vector3.Distance(transform.position, target.position);
            return distance <= range;
        }

        public bool TryAttack(Transform target)
        {
            if (!CanAttack(target))
                return false;

            fireCooldown -= Time.deltaTime;
            if (fireCooldown > 0.0f)
                return false;

            fireCooldown = Mathf.Max(0.05f, 1.0f / Mathf.Max(0.01f, fireRate));

            if (target.TryGetComponent(out AIPlayerHealth playerHealth))
                playerHealth.ApplyDamage(damage);
            else if (target.GetComponentInParent<AIPlayerHealth>() is { } parentHealth)
                parentHealth.ApplyDamage(damage);

            return true;
        }
    }
}
