using UnityEngine;

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

            Vector3 direction = target.position - firePoint.position;
            if (direction.sqrMagnitude < Mathf.Epsilon)
                return true;

            if (direction.magnitude > range)
                return false;

            return Physics.Raycast(firePoint.position, direction.normalized, out RaycastHit hit, range, hitMask) &&
                   (hit.transform == target || hit.transform.IsChildOf(target));
        }

        public bool TryAttack(Transform target)
        {
            if (!CanAttack(target))
                return false;

            fireCooldown -= Time.deltaTime;
            if (fireCooldown > 0.0f)
                return false;

            fireCooldown = Mathf.Max(0.05f, 1.0f / Mathf.Max(0.01f, fireRate));

            Vector3 origin = firePoint != null ? firePoint.position : transform.position;
            Vector3 direction = (target.position - origin).normalized;
            if (Physics.Raycast(origin, direction, out RaycastHit hit, range, hitMask))
            {
                if (hit.transform.TryGetComponent(out AIPlayerHealth playerHealth))
                    playerHealth.ApplyDamage(damage);
                else if (hit.transform.GetComponentInParent<AIPlayerHealth>() is { } parentHealth)
                    parentHealth.ApplyDamage(damage);
            }

            return true;
        }
    }
}
