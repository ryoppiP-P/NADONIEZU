using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasScaler))]
public class ResponsiveCanvasScaler : MonoBehaviour {
    [Tooltip("縦長画面（スマホ縦持ち）想定のMatch値。1=Height基準")]
    [Range(0f, 1f)] public float portraitMatch = 1f;

    [Tooltip("横長スマホ（16:9?19.5:9）想定のMatch値")]
    [Range(0f, 1f)] public float phoneLandscapeMatch = 1f;

    [Tooltip("タブレット（4:3?16:10）想定のMatch値。0.5前後がバランス良い")]
    [Range(0f, 1f)] public float tabletMatch = 0.5f;

    [Tooltip("この比率以下ならタブレット扱い（幅/高さ）")]
    public float tabletAspectThreshold = 1.8f;

    CanvasScaler scaler;
    int lastWidth, lastHeight;

    void Awake() {
        scaler = GetComponent<CanvasScaler>();
        Apply();
    }

    void Update() {
        // 画面サイズが変わったら再適用（回転対応）
        if (Screen.width != lastWidth || Screen.height != lastHeight) {
            Apply();
        }
    }

    void Apply() {
        lastWidth = Screen.width;
        lastHeight = Screen.height;

        float aspect = (float)Screen.width / Screen.height;

        float match;
        if (aspect < 1f) {
            // 縦持ち
            match = portraitMatch;
        } else if (aspect >= tabletAspectThreshold) {
            // 横長スマホ（16:9 = 1.78, 19.5:9 = 2.17）
            match = phoneLandscapeMatch;
        } else {
            // タブレット（4:3 = 1.33, 16:10 = 1.6）
            match = tabletMatch;
        }

        scaler.matchWidthOrHeight = match;
    }
}
