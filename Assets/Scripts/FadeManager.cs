//==============================================================================
//  File   : FadeManager.cs
//  Brief  : フェード管理
// 
//  Author : Ryoto Kikuchi
//  Date   : 2026/6/30
//------------------------------------------------------------------------------
//
//==============================================================================
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

public class FadeManager : MonoBehaviour {
    // インスタンス（外部からは取得のみ、書き換え不可）
    public static FadeManager Instance { get; private set; }

    // 状態（外部からは読み取りのみ）
    public static bool IsFadeIn { get; private set; }
    public static bool IsFadeOut { get; private set; }
    public static bool IsFading => IsFadeIn || IsFadeOut;

    // 内部状態
    private static Canvas fadeCanvas;
    private static Image fadeImage;
    private static float alpha = 0f;
    private static float fadeTime = 0.3f;
    private static string nextSceneName;

    static void Init() {
        GameObject FadeCanvasObject = new GameObject("CanvasFade");
        DontDestroyOnLoad(FadeCanvasObject);

        fadeCanvas = FadeCanvasObject.AddComponent<Canvas>();
        fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        fadeCanvas.sortingOrder = 100;

        Instance = FadeCanvasObject.AddComponent<FadeManager>();

        fadeImage = new GameObject("ImageFade").AddComponent<Image>();
        fadeImage.transform.SetParent(fadeCanvas.transform, false);
        fadeImage.raycastTarget = false;

        var rt = fadeImage.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        fadeImage.color = Color.clear;
        fadeCanvas.enabled = false;
    }

    public static void FadeIn(float duration = 0.3f) {
        if (IsFading) return;
        if (fadeImage == null) Init();

        fadeTime = duration;
        alpha = 1.0f;
        fadeImage.color = Color.black;
        fadeCanvas.enabled = true;
        IsFadeIn = true;
    }

    public static void FadeOut(string sceneName, float duration = 0.3f) {
        if (IsFading) return;
        if (fadeImage == null) Init();

        fadeTime = duration;
        nextSceneName = sceneName;
        alpha = 0.0f;
        fadeImage.color = Color.clear;
        fadeCanvas.enabled = true;
        IsFadeOut = true;
    }

    void Update() {
        if (IsFadeIn) {
            alpha -= Time.unscaledDeltaTime / fadeTime;

            if (alpha <= 0.0f) {
                IsFadeIn = false;
                alpha = 0.0f;
                fadeCanvas.enabled = false;
            }
            fadeImage.color = new Color(0f, 0f, 0f, alpha);
        } else if (IsFadeOut) {
            alpha += Time.unscaledDeltaTime / fadeTime;

            if (alpha >= 1.0f) {
                IsFadeOut = false;
                alpha = 1.0f;
                SceneManager.LoadScene(nextSceneName);
                FadeIn(fadeTime);
            }
            fadeImage.color = new Color(0f, 0f, 0f, alpha);
        }
    }

    // シーン遷移を伴わずに暗転させるだけのメソッド。
    // FadeOut()は必ずSceneManager.LoadSceneを呼ぶため、同一シーン内で一瞬暗転させて
    // 位置をリセットするといった用途には使えないため別途用意する。
    public IEnumerator FadeToBlack(float duration) {
        if (fadeImage == null) Init();
        fadeCanvas.enabled = true;
        float startAlpha = fadeImage.color.a;
        float t = 0f;
        while (t < duration) {
            t += Time.deltaTime;
            Color c = fadeImage.color;
            c.a = Mathf.Lerp(startAlpha, 1f, t / duration);
            fadeImage.color = c;
            yield return null;
        }
        fadeImage.color = new Color(0, 0, 0, 1);
    }

    public IEnumerator FadeFromBlack(float duration) {
        if (fadeImage == null) Init();
        fadeCanvas.enabled = true;
        fadeImage.color = new Color(0, 0, 0, 1);
        float t = 0f;
        while (t < duration) {
            t += Time.deltaTime;
            Color c = fadeImage.color;
            c.a = Mathf.Lerp(1f, 0f, t / duration);
            fadeImage.color = c;
            yield return null;
        }
        fadeImage.color = new Color(0, 0, 0, 0);
    }
}
