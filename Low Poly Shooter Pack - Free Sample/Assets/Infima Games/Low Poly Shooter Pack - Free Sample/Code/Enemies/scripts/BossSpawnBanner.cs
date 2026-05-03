using UnityEngine;

/// <summary>Краткая надпись по центру экрана (например при появлении босса).</summary>
public sealed class BossSpawnBanner : MonoBehaviour
{
    [SerializeField] private string text = "Босс здесь!";
    [SerializeField] private float durationSeconds = 4.5f;
    [SerializeField] private int fontSize = 44;

    private float _endTime;
    private GUIStyle _style;

    /// <summary>Показать баннер на указанное время (игровое время может быть с scale — используем unscaled).</summary>
    public static void Show(string message, float duration = 4.5f, int size = 44)
    {
        var go = new GameObject("BossSpawnBanner");
        var b = go.AddComponent<BossSpawnBanner>();
        b.text = message;
        b.durationSeconds = duration;
        b.fontSize = size;
        b._endTime = Time.unscaledTime + duration;
    }

    private void OnEnable()
    {
        // Если не вызывали Show() (например, висит на объекте в сцене) — старт по полям.
        if (_endTime <= 0f)
            _endTime = Time.unscaledTime + durationSeconds;
    }

    private void OnGUI()
    {
        if (Time.unscaledTime >= _endTime)
        {
            Destroy(gameObject);
            return;
        }

        if (_style == null)
        {
            _style = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _style.normal.textColor = new Color(1f, 0.35f, 0.25f, 1f);
        }

        float w = Mathf.Min(900f, Screen.width - 40f);
        float h = 100f;
        GUI.Label(new Rect((Screen.width - w) * 0.5f, Screen.height * 0.12f, w, h), text, _style);
    }
}
