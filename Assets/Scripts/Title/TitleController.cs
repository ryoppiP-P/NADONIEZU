// TitleSceneの演出まとめ役。
// ・起動直後、ロゴ→スタートボタン→設定ボタンの順に弾んでポップインさせる
// ・スタートボタンは落ち着いた後もゆっくり呼吸するように拡縮させ、押したくなる導線にする
// ・背景の「物が舞い散っているオフィス」の絵に合わせて、紙くずが画面上からふわふわ降ってくる
//   アンビエント演出を常時流す（BGは静止画のままなので、UI側で動きを足す）
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class TitleController : MonoBehaviour {
    [Header("登場演出の対象")]
    public RectTransform logo;
    public RectTransform gameStartButton;
    public RectTransform settingButton;

    [Header("紙くず演出")]
    [Tooltip("BGとSafeAreaの間などに置く、紙くずの生成先")]
    public RectTransform debrisParent;
    public bool spawnDebris = true;
    public float debrisIntervalMin = 1.2f;
    public float debrisIntervalMax = 2.4f;

    Vector3 logoBaseScale;
    Vector3 gsBaseScale;
    Vector3 stBaseScale;

    void Awake() {
        if (logo != null) logoBaseScale = logo.localScale;
        if (gameStartButton != null) gsBaseScale = gameStartButton.localScale;
        if (settingButton != null) stBaseScale = settingButton.localScale;
    }

    void Start() {
        StartCoroutine(PlayIntro());
        if (spawnDebris && debrisParent != null) StartCoroutine(DebrisLoop());
    }

    IEnumerator PlayIntro() {
        if (logo != null) logo.localScale = Vector3.zero;
        if (gameStartButton != null) gameStartButton.localScale = Vector3.zero;
        if (settingButton != null) settingButton.localScale = Vector3.zero;

        yield return new WaitForSeconds(0.15f);
        yield return PopIn(logo, logoBaseScale, 0.55f);

        yield return new WaitForSeconds(0.08f);
        yield return PopIn(gameStartButton, gsBaseScale, 0.4f);

        yield return new WaitForSeconds(0.06f);
        yield return PopIn(settingButton, stBaseScale, 0.4f);

        // 落ち着いたら、スタートボタンだけゆっくり呼吸させて視線を誘導する
        // （呼吸自体はTitleButtonFeel側が担当。ここでのポップイン中に取り合いにならないよう、終わってから起動する）
        if (gameStartButton != null) {
            var feel = gameStartButton.GetComponent<TitleButtonFeel>();
            if (feel != null) feel.SetBreatheEnabled(true);
        }
    }

    /// <summary>0から目標スケールへ、少しオーバーシュートしてから収まる「ポン」というポップイン。</summary>
    IEnumerator PopIn(RectTransform rt, Vector3 targetScale, float duration) {
        if (rt == null) yield break;

        float t = 0f;
        while (t < duration) {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            rt.localScale = targetScale * BackEaseOut(k);
            yield return null;
        }
        rt.localScale = targetScale;
    }

    /// <summary>定番の「行きすぎてから戻る」イージング。</summary>
    static float BackEaseOut(float k) {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float k1 = k - 1f;
        return 1f + c3 * k1 * k1 * k1 + c1 * k1 * k1;
    }

    IEnumerator DebrisLoop() {
        while (true) {
            SpawnDebris();
            yield return new WaitForSeconds(Random.Range(debrisIntervalMin, debrisIntervalMax));
        }
    }

    void SpawnDebris() {
        var go = new GameObject("Debris", typeof(RectTransform));
        go.transform.SetParent(debrisParent, false);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);

        float w = Random.Range(9f, 20f);
        float h = w * Random.Range(1.2f, 1.6f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(Random.Range(-900f, 900f), Random.Range(10f, 120f));
        rt.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

        var img = go.AddComponent<Image>();
        float shade = Random.Range(0.82f, 1f);
        img.color = new Color(shade, shade, shade * 0.94f, Random.Range(0.55f, 0.85f));
        img.raycastTarget = false;

        go.AddComponent<TitleDebrisPiece>().BeginFall();
    }
}

/// <summary>紙くず1枚。ゆらゆら揺れながら落下し、画面下に近づいたらフェードアウトして消える。</summary>
public class TitleDebrisPiece : MonoBehaviour {
    public void BeginFall() {
        StartCoroutine(Fall());
    }

    IEnumerator Fall() {
        var rt = (RectTransform)transform;
        var img = GetComponent<Image>();

        float fallSpeed = Random.Range(26f, 52f);
        float swayAmp = Random.Range(14f, 42f);
        float swayFreq = Random.Range(0.45f, 1.05f);
        float spin = Random.Range(-45f, 45f);
        float startX = rt.anchoredPosition.x;
        float elapsed = 0f;

        while (rt.anchoredPosition.y > -650f) {
            elapsed += Time.deltaTime;

            Vector2 pos = rt.anchoredPosition;
            pos.y -= fallSpeed * Time.deltaTime;
            pos.x = startX + Mathf.Sin(elapsed * swayFreq * Mathf.PI * 2f) * swayAmp;
            rt.anchoredPosition = pos;
            rt.Rotate(0f, 0f, spin * Time.deltaTime);

            if (pos.y < -480f) {
                var c = img.color;
                c.a = Mathf.MoveTowards(c.a, 0f, Time.deltaTime * 0.6f);
                img.color = c;
                if (c.a <= 0.01f) break;
            }

            yield return null;
        }

        Destroy(gameObject);
    }
}
