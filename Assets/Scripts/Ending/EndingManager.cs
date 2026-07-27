// EDシーンに置く　UIの表示とフェードイン、タイプライター演出を行う
using System.Collections;
using TMPro;
using UnityEngine;

public class EndingManager : MonoBehaviour {
    [Header("Database")]
    public EndingDatabase database;

    [Header("UI")]
    public TextMeshProUGUI titleLabel;
    public TextMeshProUGUI bodyLabel;
    public CanvasGroup fadeGroup;

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
        yield return new WaitForSeconds(1f);

        // 本文タイプライター
        foreach (char c in data.bodyText) {
            bodyLabel.text += c;
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
