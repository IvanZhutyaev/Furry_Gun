using UnityEngine;

/// <summary>
/// Триггер на «кулаке»: один раз списывает удар через enemyAI.TryConsumeMeleeHit за окно атаки.
/// Если игрок уже стоит внутри триггера в момент начала замаха — нужен OnTriggerStay; Enter не всегда сработает повторно.
/// </summary>
[DisallowMultipleComponent]
public class sphereTriggerDamage : MonoBehaviour
{
    [SerializeField]
    private float damage = 15f;

    [SerializeField]
    private string playerTag = "Player";

    [Tooltip("Если пусто, ищется enemyAI выше по иерархии.")]
    [SerializeField]
    private enemyAI owner;

#if UNITY_EDITOR
    private void Reset()
    {
        var col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;
    }
#endif

    private void Awake()
    {
        if (owner == null)
            owner = GetComponentInParent<enemyAI>();
    }

    private void OnTriggerStay(Collider other)
    {
        if (!other.CompareTag(playerTag))
            return;

        if (owner == null)
            owner = GetComponentInParent<enemyAI>();

        if (owner == null || !owner.TryConsumeMeleeHitOnce())
            return;

        ApplyDamageToPlayer(other.gameObject);
    }

    /// <summary>Если понадобится отдельно от триггерной геометрии.</summary>
    public void RefreshOwnerCache()
    {
        owner = GetComponentInParent<enemyAI>();
    }

    private void ApplyDamageToPlayer(GameObject hitObject)
    {
        if (hitObject.TryGetComponent<PlayerHealth>(out var hp))
        {
            hp.TakeDamage(damage);
            return;
        }

        hp = hitObject.GetComponentInParent<PlayerHealth>();
        if (hp != null)
            hp.TakeDamage(damage);
    }
}
