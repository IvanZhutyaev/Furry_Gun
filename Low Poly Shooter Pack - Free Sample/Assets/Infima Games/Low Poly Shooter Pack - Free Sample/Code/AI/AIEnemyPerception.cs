using UnityEngine;

namespace InfimaGames.LowPolyShooterPack.AI
{
    public sealed class AIEnemyPerception : MonoBehaviour
    {
        [Header("Perception")]
        [SerializeField] private float viewDistance = 30.0f;
        [SerializeField, Range(1.0f, 180.0f)] private float viewAngle = 90.0f;
        [SerializeField] private LayerMask occlusionMask = ~0;
        [SerializeField] private float eyeHeight = 1.6f;

        public bool TryDetectTarget(Transform candidate, out float distanceToTarget)
        {
            distanceToTarget = float.MaxValue;

            if (candidate == null)
                return false;

            Vector3 eye = transform.position + Vector3.up * eyeHeight;
            Vector3 direction = candidate.position - eye;
            distanceToTarget = direction.magnitude;

            if (distanceToTarget > viewDistance)
                return false;

            if (direction.sqrMagnitude < Mathf.Epsilon)
                return true;

            float angle = Vector3.Angle(transform.forward, direction.normalized);
            if (angle > viewAngle * 0.5f)
                return false;

            if (Physics.Raycast(eye, direction.normalized, out RaycastHit hit, viewDistance, occlusionMask))
            {
                return hit.transform == candidate || hit.transform.IsChildOf(candidate);
            }

            return true;
        }
    }
}
