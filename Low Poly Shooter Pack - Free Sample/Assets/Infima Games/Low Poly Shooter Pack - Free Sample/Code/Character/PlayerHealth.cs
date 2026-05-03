using UnityEngine;

/// <summary>
/// Простое здоровье игрока. Повесь на корень объекта с тегом Player (рядом с Character / коллайдером).
/// </summary>
public sealed class PlayerHealth : MonoBehaviour
{
    [SerializeField] private float maxHealth = 100f;

    [Header("Debug")]
    [SerializeField]
    private bool logHealth = true;

    private float currentHealth;
    private bool gameOverTriggered;

    private void Awake()
    {
        currentHealth = maxHealth;
        Log($"Старт: {FormatHealth()}");
    }

    public float Current => currentHealth;
    public float Max => maxHealth;
    public bool IsDead => currentHealth <= 0f;

    public void TakeDamage(float amount)
    {
        if (amount <= 0f || IsDead)
            return;

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        Log($"Урон {amount:F1} → {FormatHealth()}");

        if (currentHealth <= 0f)
            HandleDepleted();
    }

    private void HandleDepleted()
    {
        if (gameOverTriggered)
            return;

        gameOverTriggered = true;

        Log($"Смерть. {FormatHealth()} — завершение игры");

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        Time.timeScale = 0f;

#if UNITY_EDITOR
        UnityEditor.EditorApplication.ExitPlaymode();
#else
        Application.Quit();
#endif
    }

    /// <summary>Дебаг или UI.</summary>
    public void RestoreFull()
    {
        currentHealth = maxHealth;
        Log($"Восстановление до максимума: {FormatHealth()}");
    }

    /// <summary>Повышение макс. HP и текущего HP на ту же величину (стадии прогрессии).</summary>
    public void ApplyMaxHealthIncrease(float delta)
    {
        if (delta <= 0f)
            return;

        maxHealth += delta;
        currentHealth += delta;
        Log($"Стадия: +{delta:F0} HP → {FormatHealth()}");
    }

    private string FormatHealth()
    {
        float pct = maxHealth > 0f ? 100f * currentHealth / maxHealth : 0f;
        return $"{currentHealth:F1} / {maxHealth:F1} HP ({pct:F0}%)";
    }

    private void Log(string message)
    {
        if (!logHealth)
            return;

        Debug.Log($"[{nameof(PlayerHealth)}] {message}", this);
    }
}
