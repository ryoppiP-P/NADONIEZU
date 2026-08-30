// EDシーンに置く　UIの表示とフェードイン、タイプライター演出を行う
// 流れ: タイトル → 本文（ゆっくりタイプ／クリックで一気に表示）→ 最終IKDをドンと表示 → ランク呼称をポップイン → 入力でタイトルへ
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

    [Tooltip("最終IKDの数字（本文を読み終えてから大きく表示する）")]
    public TextMeshProUGUI ikdLabel;

    [Tooltip("数字の上に出す「最終IKD」の見出し（任意。未設定なら数字側に見出しを含めて表示する）")]
    public TextMeshProUGUI ikdCaption;

    [Tooltip("IKDに応じたランク呼称（任意。IKDManager.GetRankLabel()の結果を表示する）")]
    public TextMeshProUGUI rankLabel;

    [Tooltip("最後に出す「クリックでタイトルへ」の表示（任意）")]
    public GameObject continuePrompt;

    [Header("Settings")]
    public float fadeInDuration = 2f;

    [Tooltip("1文字あたりの表示間隔。大きいほどゆっくり")]
    public float typeSpeed = 0.085f;

    [Tooltip("句読点（。、！？）で追加で置く間")]
    public float punctuationPause = 0.25f;

    [Tooltip("改行で追加で置く間。空行はさらに倍の間が入る")]
    public float linePause = 0.35f;

    [Header("Timing")]
    public float titleDelay = 0.6f;        // 表示開始までの溜め
    public float titleFadeDuration = 0.8f; // タイトルのフェードイン
    public float afterTitleWait = 1.2f;    // タイトル後、本文が始まるまで
    public float beforeIkdWait = 1.4f;     // 本文が終わってからIKDが出るまで
    public float ikdPunchDuration = 0.7f;  // IKDが決まるまでの演出時間
    public float scrollSmooth = 6f;        // 本文の自動スクロールの滑らかさ

    public string titleSceneName = "TitleScene"; // 戻り先（任意）

    bool skipRequested = false;

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
        // 初期化：IKDと続行案内は本文を読み終えるまで隠しておく
        titleLabel.text = "";
        bodyLabel.text = "";
        SetAlpha(titleLabel, 0f);
        if (ikdLabel != null) ikdLabel.gameObject.SetActive(false);
        if (ikdCaption != null) ikdCaption.gameObject.SetActive(false);
        if (rankLabel != null) rankLabel.gameObject.SetActive(false);
        if (continuePrompt != null) continuePrompt.SetActive(false);

        if (fadeGroup != null) {
            fadeGroup.alpha = 0f;
            float t = 0f;
            while (t < fadeInDuration) {
                t += Time.deltaTime;
                fadeGroup.alpha = t / fadeInDuration;
                yield return null;
            }
            fadeGroup.alpha = 1f;
        }

        yield return new WaitForSeconds(titleDelay);

        // タイトルをふわっと出す
        titleLabel.text = data.endingTitle;
        float ft = 0f;
        while (ft < titleFadeDuration) {
            ft += Time.deltaTime;
            SetAlpha(titleLabel, Mathf.Clamp01(ft / titleFadeDuration));
            yield return null;
        }
        SetAlpha(titleLabel, 1f);

        yield return new WaitForSeconds(afterTitleWait);

        // 本文
        yield return TypeBody(data.bodyText);

        yield return new WaitForSeconds(beforeIkdWait);

        // 最終IKDをドンと出す
        yield return RevealIKD();

        // 続行案内。ここで押しっぱなしの入力を拾わないよう1フレーム空ける
        if (continuePrompt != null) continuePrompt.SetActive(true);
        yield return null;

        while (!AnyPressThisFrame()) yield return null;

        if (!string.IsNullOrEmpty(titleSceneName)) {
            FadeManager.FadeOut(titleSceneName, 1.5f);
        }
    }

    /// <summary>本文を1文字ずつ表示する。句読点と改行で間を置き、クリックで全文表示にスキップできる。</summary>
    IEnumerator TypeBody(string text) {
        if (string.IsNullOrEmpty(text)) yield break;

        skipRequested = false;
        if (bodyScrollRect != null) bodyScrollRect.verticalNormalizedPosition = 1f;

        int shown = 0;
        float wait = 0f;

        while (shown < text.Length) {
            if (!skipRequested && AnyPressThisFrame()) skipRequested = true;

            if (skipRequested) {
                bodyLabel.text = text;
                shown = text.Length;
            } else {
                wait -= Time.deltaTime;
                // 1フレームで複数文字進むこともあるのでwhileで消化する
                while (wait <= 0f && shown < text.Length) {
                    char c = text[shown];
                    bodyLabel.text += c;
                    shown++;
                    wait += DelayFor(c, text, shown);
                }
            }

            ScrollTowardBottom();
            yield return null;
        }

        // 末尾までスクロールし切るのを待つ（読み終わりで文字が隠れたままにならないように）
        float guard = 0f;
        while (bodyScrollRect != null && bodyScrollRect.verticalNormalizedPosition > 0.002f && guard < 2f) {
            guard += Time.deltaTime;
            ScrollTowardBottom();
            yield return null;
        }
    }

    /// <summary>文字ごとの待ち時間。読点で軽く、句点で長めに、改行でさらに息を置く。</summary>
    float DelayFor(char c, string text, int nextIndex) {
        float d = typeSpeed;
        if (c == '\n') {
            // 空行（改行が連続する箇所）は段落の切れ目なので長めに取る
            bool blankLine = nextIndex < text.Length && text[nextIndex] == '\n';
            d += blankLine ? linePause * 2f : linePause;
        } else if (c == '。' || c == '！' || c == '？') {
            d += punctuationPause;
        } else if (c == '、') {
            d += punctuationPause * 0.5f;
        }
        return d;
    }

    void ScrollTowardBottom() {
        if (bodyScrollRect == null) return;
        Canvas.ForceUpdateCanvases();
        // 1文字ごとに最下部へ瞬間移動させると文字が跳ねて読みにくいので、滑らかに追従させる
        bodyScrollRect.verticalNormalizedPosition = Mathf.Lerp(
            bodyScrollRect.verticalNormalizedPosition, 0f, Time.deltaTime * scrollSmooth);
    }

    /// <summary>最終IKDを、数字を駆け上がらせながら大きめから原寸へ詰めて表示する。</summary>
    IEnumerator RevealIKD() {
        if (ikdLabel == null) yield break;

        int finalIKD = IKDManager.Instance != null ? IKDManager.Instance.CurrentIKD : 0;

        if (ikdCaption != null) ikdCaption.gameObject.SetActive(true);
        ikdLabel.gameObject.SetActive(true);

        var rt = ikdLabel.rectTransform;
        float t = 0f;
        while (t < ikdPunchDuration) {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / ikdPunchDuration);
            float ease = 1f - Mathf.Pow(1f - k, 3f);

            ikdLabel.text = FormatIKD(Mathf.RoundToInt(Mathf.Lerp(0f, finalIKD, ease)));
            float s = Mathf.Lerp(1.45f, 1f, 1f - Mathf.Pow(1f - k, 4f));
            rt.localScale = new Vector3(s, s, 1f);
            yield return null;
        }

        ikdLabel.text = FormatIKD(finalIKD);
        rt.localScale = Vector3.one;

        // 数字が着地した少し後に、ランク呼称を追いかけてポンと出す
        if (rankLabel != null && IKDManager.Instance != null) {
            yield return new WaitForSeconds(0.15f);
            rankLabel.text = IKDManager.Instance.GetRankLabel(finalIKD);
            rankLabel.gameObject.SetActive(true);
            yield return PunchIn(rankLabel.rectTransform, 0.35f);
        }
    }

    /// <summary>少し大きめから原寸へ収まる、軽いポップイン演出。</summary>
    IEnumerator PunchIn(RectTransform rt, float duration) {
        float t = 0f;
        while (t < duration) {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            float s = Mathf.Lerp(1.3f, 1f, 1f - Mathf.Pow(1f - k, 4f));
            rt.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        rt.localScale = Vector3.one;
    }

    // 見出しを別ラベルに分けている場合は数字だけ、無い場合は従来通り見出し込みで出す
    string FormatIKD(int value) => ikdCaption != null ? value.ToString() : $"最終IKD: {value}";

    void SetAlpha(TextMeshProUGUI label, float a) {
        if (label == null) return;
        var c = label.color;
        c.a = a;
        label.color = c;
    }

    bool AnyPressThisFrame() {
        if (Input.anyKeyDown) return true;
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began) return true;
        return false;
    }
}
