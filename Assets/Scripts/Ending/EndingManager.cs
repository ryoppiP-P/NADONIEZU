// EDシーンに置く　UIの表示とフェードイン、タイプライター演出を行う
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EndingManager : MonoBehaviour {
    [Header("Database")]
    public EndingDatabase database;

    [Header("UI")]
    public TextMeshProUGUI titleLabel;
    public TextMeshProUGUI bodyLabel;
    public CanvasGroup fadeGroup;

    [Tooltip("本文が長い場合に自動スクロールさせるScrollRect（任意）")]
    public ScrollRect bodyScrollRect;

    [Tooltip("最終IKDを表示するテキスト（任意）")]
    public TextMeshProUGUI ikdLabel;

    [Header("Settings")]
    public float fadeInDuration = 2f;
    public float typeSpeed = 0.05f;    // タイプライター速度
    public string titleSceneName = "TitleScene"; // 戻り先（任意）

    void Start() {
        // カーソル解放（会話中に固定してた場合の対策）
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        var result = EndingResultHolder.Result;
        var data = database.Find(result);

        if (data == null) {
            Debug.LogError("[Ending] No ending data found!");
            return;
        }

        Debug.Log($"[Ending] Showing: {data.endingTitle}");
        StartCoroutine(PlayEnding(data));
    }

    IEnumerator PlayEnding(EndingData data) {
        // 初期化
        titleLabel.text = "";
        bodyLabel.text = "";
        if (fadeGroup != null) fadeGroup.alpha = 0f;

        // フェードイン
        float t = 0f;
        while (t < fadeInDuration) {
            t += Time.deltaTime;
            if (fadeGroup != null) fadeGroup.alpha = t / fadeInDuration;
            yield return null;
        }
        if (fadeGroup != null) fadeGroup.alpha = 1f;

        // タイトル表示
        titleLabel.text = data.endingTitle;

        if (ikdLabel != null) {
            int finalIKD = IKDManager.Instance != null ? IKDManager.Instance.CurrentIKD : 0;
            ikdLabel.text = $"最終IKD: {finalIKD}";
        }

        yield return new WaitForSeconds(1f);

        // 本文タイプライター（長文は自動で下端へスクロールしながら表示）
        if (bodyScrollRect != null) bodyScrollRect.verticalNormalizedPosition = 1f;
        foreach (char c in data.bodyText) {
            bodyLabel.text += c;

            // ContentSizeFitterの再計算を待ってから最下部へスナップ
            if (bodyScrollRect != null) {
                Canvas.ForceUpdateCanvases();
                bodyScrollRect.verticalNormalizedPosition = 0f;
            }

            yield return new WaitForSeconds(typeSpeed);
        }

        // 完了後、クリックまたはキー入力でタイトルへ
        yield return new WaitForSeconds(1f);
        Debug.Log("[Ending] Press any key to return...");

        while (!Input.anyKeyDown) yield return null;

        if (!string.IsNullOrEmpty(titleSceneName)) {
            FadeManager.FadeOut(titleSceneName, 1.5f);
        }
    }
}
