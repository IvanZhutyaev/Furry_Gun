using System;
using InfimaGames.LowPolyShooterPack;
using UnityEngine;

/// <summary>
/// Стадии ГГ: каждые 10 убийств врагов — новая стадия (макс. 3). +20 к здоровью и урону за стадию, +скорость бега.
/// </summary>
public static class PlayerProgression
{
    public const int KillsPerStage = 10;
    public const int MaxStage = 3;

    public const float HealthBonusPerStage = 20f;
    public const float RunSpeedBonusPerStage = 2f;

    public static int KillCount { get; private set; }
    public static int CurrentStage { get; private set; } = 1;

    /// <summary>Добавочный урон к базовому <see cref="Projectile.damage"/> (0 / 20 / 40).</summary>
    public static float ProjectileDamageBonus => (CurrentStage - 1) * HealthBonusPerStage;

    public static event Action<int> StageChanged;

    public static void RegisterEnemyKill()
    {
        KillCount++;

        int targetStage = 1 + Mathf.Min(MaxStage - 1, KillCount / KillsPerStage);

        while (CurrentStage < targetStage)
        {
            CurrentStage++;
            ApplyBonusesForStageReached();
            StageChanged?.Invoke(CurrentStage);
            Debug.LogWarning($"[PlayerProgression] Новая стадия {CurrentStage}! Всего убийств: {KillCount}");
        }
    }

    private static void ApplyBonusesForStageReached()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            Debug.LogWarning("[PlayerProgression] Не найден объект с тегом Player — бонусы стадии не применены.");
            return;
        }

        if (player.TryGetComponent(out PlayerHealth hp))
            hp.ApplyMaxHealthIncrease(HealthBonusPerStage);
        else
            Debug.LogWarning("[PlayerProgression] На игроке нет PlayerHealth — бонус здоровья пропущен. Добавь компонент на префаб P_LPSP_FP_CH.");

        if (player.TryGetComponent(out Movement mov))
            mov.ApplyRunSpeedBonus(RunSpeedBonusPerStage);
        else
            Debug.LogWarning("[PlayerProgression] На игроке нет Movement — бонус скорости пропущен.");
    }
}
