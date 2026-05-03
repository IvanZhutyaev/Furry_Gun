using UnityEngine;

/// <summary>
/// Триггер на «кулаке»: один раз за воспроизведение состояния атаки в Animator при условии, что в enemyAI уже включён режим атаки (bool isAttack у аниматора).
/// OnTriggerStay — чтобы засчитать касание, если игрок не выходил из триггера между кадрами.
/// </summary>
[DisallowMultipleComponent]
public class sphereTriggerDamage : MonoBehaviour
{
    [Tooltip("Fallback-интервал урона, если Animator-окно удара не распознано.")]
    [SerializeField]
    private float fallbackHitInterval = 0.8f;

    [SerializeField]
    private float damage = 20f;

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
        // У игрока часто несколько дочерних коллайдеров без тега Player:
        // допускаем попадание, если тег на root ИЛИ найден PlayerHealth в иерархии.
        if (!IsPlayerHit(other, out PlayerHealth hp))
            return;

        if (owner == null)
            owner = GetComponentInParent<enemyAI>();

        if (owner == null)
            return;

        // Animator-first logic with safe fallback (prevents zero-damage when state name/layer mismatch).
        if (!owner.TryConsumeMeleeHit(fallbackHitInterval))
            return;

        hp.TakeDamage(damage);
    }

    /// <summary>Если понадобится отдельно от триггерной геометрии.</summary>
    public void RefreshOwnerCache()
    {
        owner = GetComponentInParent<enemyAI>();
    }

    public void SetDamage(float value)
    {
        damage = Mathf.Max(0f, value);
    }

    private bool IsPlayerHit(Collider other, out PlayerHealth hp)
    {
        hp = null;
        if (other == null)
            return false;

        if (other.TryGetComponent(out hp))
            return true;

        hp = other.GetComponentInParent<PlayerHealth>();
        if (hp == null)
            return false;

        // Оставляем фильтр по тегу для совместимости, но не требуем его на дочернем коллайдере.
        Transform root = other.transform.root;
        return other.CompareTag(playerTag) || (root != null && root.CompareTag(playerTag));
    }
}
