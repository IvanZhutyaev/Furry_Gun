using UnityEngine;

/// <summary>
/// Триггер на «кулаке»: один раз за воспроизведение состояния атаки в Animator при условии, что в enemyAI уже включён режим атаки (bool isAttack у аниматора).
/// OnTriggerStay — чтобы засчитать касание, если игрок не выходил из триггера между кадрами.
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
