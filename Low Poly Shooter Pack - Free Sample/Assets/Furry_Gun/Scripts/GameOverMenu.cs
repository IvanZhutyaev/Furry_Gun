using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

/// <summary>Экран смерти: You Died!, Try Again (карта 1 заново), Exit.</summary>
[DisallowMultipleComponent]
public sealed class GameOverMenu : MonoBehaviour
{
    [SerializeField] private string gameSceneName = "1";
    [SerializeField] private Color backgroundColor = new Color(0.18f, 0.06f, 0.06f, 1f);

    private void Awake()
    {
        Time.timeScale = 1f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        var cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = backgroundColor;
        }

        EnsureEventSystem();
        BuildUI();
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null)
            return;

        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
        var ui = es.AddComponent<InputSystemUIInputModule>();
        ui.AssignDefaultActions();
#else
        es.AddComponent<StandaloneInputModule>();
#endif
    }

    private void BuildUI()
    {
        var canvasGo = new GameObject("Canvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        var root = canvasGo.GetComponent<RectTransform>();
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;

        CreateTitle(canvas.transform);
        CreateButton(canvas.transform, "Try Again", new Vector2(0, 50), OnTryAgainClicked);
        CreateButton(canvas.transform, "Exit", new Vector2(0, -70), OnExitClicked);
    }

    private void CreateTitle(Transform parent)
    {
        var go = new GameObject("Title");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.68f);
        rt.anchorMax = new Vector2(0.5f, 0.68f);
        rt.sizeDelta = new Vector2(1000, 160);
        go.AddComponent<CanvasRenderer>();
        var text = go.AddComponent<Text>();
        text.text = "You Died!";
        text.font = MenuFont;
        text.fontSize = 86;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(1f, 0.4f, 0.35f, 1f);
        text.fontStyle = FontStyle.Bold;
    }

    private void CreateButton(Transform parent, string label, Vector2 anchoredPos, UnityAction onClick)
    {
        var go = new GameObject(label.Replace(" ", "") + "Button");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(300, 70);

        go.AddComponent<CanvasRenderer>();
        var img = go.AddComponent<Image>();
        img.color = new Color(0.35f, 0.18f, 0.18f, 1f);

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        var colors = btn.colors;
        colors.highlightedColor = new Color(0.5f, 0.28f, 0.25f, 1f);
        colors.pressedColor = new Color(0.25f, 0.12f, 0.12f, 1f);
        btn.colors = colors;
        btn.onClick.AddListener(onClick);

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(go.transform, false);
        var trt = textGo.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;
        textGo.AddComponent<CanvasRenderer>();
        var text = textGo.AddComponent<Text>();
        text.text = label;
        text.font = MenuFont;
        text.fontSize = 30;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
    }

    private static Font MenuFont
    {
        get
        {
            Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (f != null)
                return f;
            f = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (f != null)
                return f;
            return Font.CreateDynamicFontFromOSFont(new[] { "Arial", "Liberation Sans" }, 32);
        }
    }

    private void OnTryAgainClicked()
    {
        Time.timeScale = 1f;
        PlayerProgression.ResetRun();
        EnemyAutoSpawner.PrepareNewRun();
        SceneManager.LoadScene(gameSceneName);
    }

    private void OnExitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.ExitPlaymode();
#else
        Application.Quit();
#endif
    }
}
