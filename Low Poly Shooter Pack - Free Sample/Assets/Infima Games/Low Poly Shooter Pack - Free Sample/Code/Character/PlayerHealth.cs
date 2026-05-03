using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Простое здоровье игрока. Повесь на корень объекта с тегом Player (рядом с Character / коллайдером).
/// </summary>
public sealed class PlayerHealth : MonoBehaviour
{
    [SerializeField] private float maxHealth = 100f;

    [Tooltip("Сцена с экраном смерти (имя файла сцены, напр. YouDied).")]
    [SerializeField] private string gameOverSceneName = "YouDied";

    [Header("Debug")]
    [SerializeField]
    private bool logHealth = true;

    private float currentHealth;
    private bool gameOverTriggered;
    private bool healthInitialized;

    private void Awake()
    {
        EnsureHealthInitialized();
        Log($"Старт: {FormatHealth()}");
    }

    public float Current => currentHealth;
    public float Max => maxHealth;
    public bool IsDead => healthInitialized && currentHealth <= 0f;

    private void EnsureHealthInitialized()
    {
        if (healthInitialized)
            return;
        currentHealth = maxHealth;
        healthInitialized = true;
    }

    public void TakeDamage(float amount)
    {
        EnsureHealthInitialized();
        if (amount <= 0f || currentHealth <= 0f)
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

        Log($"Смерть. {FormatHealth()} — экран game over");

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        Time.timeScale = 1f;

        if (!string.IsNullOrEmpty(gameOverSceneName))
            SceneManager.LoadScene(gameOverSceneName);
    }

    /// <summary>Дебаг или UI.</summary>
    public void RestoreFull()
    {
        EnsureHealthInitialized();
        currentHealth = maxHealth;
        Log($"Восстановление до максимума: {FormatHealth()}");
    }

    /// <summary>Повышение макс. HP и текущего HP на ту же величину (стадии прогрессии).</summary>
    public void ApplyMaxHealthIncrease(float delta)
    {
        EnsureHealthInitialized();
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
