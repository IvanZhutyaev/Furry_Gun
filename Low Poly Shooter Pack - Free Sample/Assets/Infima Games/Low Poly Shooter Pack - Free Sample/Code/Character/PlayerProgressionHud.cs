using UnityEngine;

/// <summary>Показывает стадию и число убийств в углу экрана (проверка, что прогрессия считается).</summary>
public sealed class PlayerProgressionHud : MonoBehaviour
{
    private GUIStyle _style;

    private void OnGUI()
    {
        if (_style == null)
        {
            _style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
        }

        string line =
            $"Стадия {PlayerProgression.CurrentStage} / {PlayerProgression.MaxStage}   |   Убийств: {PlayerProgression.KillCount}";
        GUI.Label(new Rect(14, 14, 520, 28), line, _style);
    }
}
