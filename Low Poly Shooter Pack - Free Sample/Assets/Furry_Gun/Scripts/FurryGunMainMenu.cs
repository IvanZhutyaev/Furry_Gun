using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

/// <summary>Собирает главное меню (Furry_Gun, Start, Exit) при загрузке сцены.</summary>
[DisallowMultipleComponent]
public sealed class FurryGunMainMenu : MonoBehaviour
{
    [SerializeField] private string gameSceneName = "1";
    [SerializeField] private Color backgroundColor = new Color(0.11f, 0.09f, 0.13f, 1f);

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
        CreateButton(canvas.transform, "Start", new Vector2(0, 50), OnStartClicked);
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
        text.text = "Furry_Gun";
        text.font = MenuFont;
        text.fontSize = 86;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.fontStyle = FontStyle.Bold;
    }

    private void CreateButton(Transform parent, string label, Vector2 anchoredPos, UnityAction onClick)
    {
        var go = new GameObject(label + "Button");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(300, 70);

        go.AddComponent<CanvasRenderer>();
        var img = go.AddComponent<Image>();
        img.color = new Color(0.22f, 0.42f, 0.32f, 1f);

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        var colors = btn.colors;
        colors.highlightedColor = new Color(0.32f, 0.58f, 0.44f, 1f);
        colors.pressedColor = new Color(0.16f, 0.3f, 0.24f, 1f);
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
        text.fontSize = 32;
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

    private void OnStartClicked()
    {
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
