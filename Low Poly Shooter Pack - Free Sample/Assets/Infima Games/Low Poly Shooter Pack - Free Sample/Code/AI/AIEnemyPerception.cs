using UnityEngine;
using System;

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
            Vector3 targetPoint = candidate.position + Vector3.up * 1.2f;
            Vector3 direction = targetPoint - eye;
            distanceToTarget = direction.magnitude;

            if (distanceToTarget > viewDistance)
                return false;

            if (direction.sqrMagnitude < Mathf.Epsilon)
                return true;

            float angle = Vector3.Angle(transform.forward, direction.normalized);
            if (angle > viewAngle * 0.5f)
                return false;

            RaycastHit[] hits = Physics.RaycastAll(eye, direction.normalized, viewDistance, occlusionMask);
            if (hits.Length > 0)
            {
                Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                for (int i = 0; i < hits.Length; i++)
                {
                    Transform hitTransform = hits[i].transform;
                    if (hitTransform == transform || hitTransform.IsChildOf(transform))
                        continue;

                    return hitTransform == candidate || hitTransform.IsChildOf(candidate);
                }
            }

            return true;
        }
    }
}
