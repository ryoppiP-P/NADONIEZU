// タイトル表示前に挟む、黒幕フェードの導入シーケンス。
// 流れ: 黒→フェードインで開発元ロゴ(白背景つき)が見える→静止→黒へフェードアウト（暗転）
//      →フェードインでタイトル背景が見える→（ここでTitleController側の登場演出を起動）
// 黒幕(CanvasGroup)はあくまで場面転換の「暗転」専用。開発元ロゴは黒地に埋もれないよう
// 専用の白背景カード(splashCard)ごとON/OFFし、フェード表現は常に黒幕側だけが担当する。
// TitleController側はOnFinishedを待ってから登場演出を始める（このコンポーネント自体は
// TitleControllerの存在を知らなくてよい設計にして、依存を一方向にしている）。
using System;
using System.Collections;
using UnityEngine;

public class SplashScreen : MonoBehaviour {
    [Header("参照")]
    [Tooltip("画面全体を覆う黒幕（フェード専用）")]
    public CanvasGroup curtain;

    [Tooltip("白背景+開発元ロゴのカード。フェードはさせず、黒幕の裏でON/OFFするだけ")]
    public GameObject splashCard;

    [Header("タイミング")]
    public float fadeDuration = 0.5f;
    public float holdDuration = 1.1f;

    /// <summary>タイトル背景が見える所まで進んだタイミングで呼ばれる</summary>
    public event Action OnFinished;

    void Start() {
        StartCoroutine(Run());
    }

    IEnumerator Run() {
        if (curtain != null) curtain.alpha = 1f;
        if (splashCard != null) splashCard.SetActive(true);

        yield return Fade(1f, 0f); // 黒からフェードイン → 白背景+開発元ロゴが見える
        yield return new WaitForSeconds(holdDuration);
        yield return Fade(0f, 1f); // 黒へフェードアウト（暗転）

        if (splashCard != null) splashCard.SetActive(false); // 暗転中に白カードを消す

        yield return Fade(1f, 0f); // フェードイン → タイトル背景が見える

        OnFinished?.Invoke();

        gameObject.SetActive(false);
    }

    IEnumerator Fade(float from, float to) {
        if (curtain == null) yield break;
        float t = 0f;
        while (t < fadeDuration) {
            t += Time.deltaTime;
            curtain.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / fadeDuration));
            yield return null;
        }
        curtain.alpha = to;
    }
}
