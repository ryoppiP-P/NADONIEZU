// UIをバカゲー調に見せる汎用の小道具。付けたUI要素に次をまとめて足す（全部Inspectorでオン/オフ可）。
//  ・静的な傾き(tilt)        … ちょっと斜めに置いて「貼り紙/ステッカー」感を出す
//  ・登場ポップ(popOnEnable) … 有効化された瞬間に小さく回りながらドンと弾んで収まる（timeScale=0でも動く）
//  ・常時ゆらゆら(wobble)    … 角度が小さく揺れ続ける（吹き出し/ステッカーの呼吸）
//  ・文字の太い縁取り        … TextMeshProの文字に黒フチを付けて漫画の効果音っぽくする
// 縁取りはTMPのマテリアル設定なのでシーンに保存されない。そのため実行時(Awake)に適用する。
// 注意: 自分でlocalScale/localRotationを動かすスクリプト(TitleButtonFeel等)を持つ要素には
//       pop/tilt/wobbleを付けないこと（取り合いになる）。文字の縁取りだけなら安全。
using System.Collections;
using TMPro;
using UnityEngine;

public class BakaUiFx : MonoBehaviour {
    [Header("静的な傾き(度)")]
    public float tilt = 0f;

    [Header("登場ポップ")]
    public bool popOnEnable = false;
    public float popDuration = 0.4f;
    [Tooltip("登場開始時の大きさ(1=原寸)")]
    public float popFromScale = 0.55f;
    [Tooltip("登場開始時に余分に傾いている角度。0へ回って収まる")]
    public float popSpin = 7f;

    [Header("常時ゆらゆら")]
    [Tooltip("傾きの振れ幅(度)。0で無効")]
    public float wobbleAngle = 0f;
    public float wobbleSpeed = 1.6f;
    [Tooltip("大きさの呼吸の振れ幅(0.03=3%)。0で無効")]
    public float breatheScale = 0f;

    [Header("文字の縁取り(TextMeshProがあれば)")]
    public bool outlineText = false;
    public Color outlineColor = Color.black;
    [Range(0f, 1f)] public float outlineWidth = 0.22f;

    Vector3 baseScale = Vector3.one;
    float phase;
    bool popping;
    Coroutine popRoutine;

    void Awake() {
        baseScale = transform.localScale;
        phase = Random.value * 6.28f; // 複数並べてもゆらぎが揃わないように
        ApplyTextOutline();
        transform.localRotation = Quaternion.Euler(0f, 0f, tilt);
    }

    void OnEnable() {
        if (popOnEnable) {
            if (popRoutine != null) StopCoroutine(popRoutine);
            popRoutine = StartCoroutine(Pop());
        }
    }

    void OnDisable() {
        popRoutine = null;
        popping = false;
        transform.localScale = baseScale;
        transform.localRotation = Quaternion.Euler(0f, 0f, tilt);
    }

    void Update() {
        if (popping) return;
        if (wobbleAngle <= 0f && breatheScale <= 0f) return;

        float t = Time.unscaledTime * wobbleSpeed + phase;
        if (wobbleAngle > 0f)
            transform.localRotation = Quaternion.Euler(0f, 0f, tilt + Mathf.Sin(t) * wobbleAngle);
        if (breatheScale > 0f)
            transform.localScale = baseScale * (1f + Mathf.Sin(t * 0.8f) * breatheScale);
    }

    void ApplyTextOutline() {
        if (!outlineText) return;
        foreach (var tmp in GetComponentsInChildren<TMP_Text>(true)) {
            tmp.outlineColor = outlineColor;
            tmp.outlineWidth = outlineWidth;
        }
    }

    IEnumerator Pop() {
        popping = true;
        float t = 0f;
        while (t < popDuration) {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / popDuration);
            float e = BackEaseOut(k);
            transform.localScale = baseScale * Mathf.LerpUnclamped(popFromScale, 1f, e);
            transform.localRotation = Quaternion.Euler(0f, 0f, tilt + popSpin * (1f - e));
            yield return null;
        }
        transform.localScale = baseScale;
        transform.localRotation = Quaternion.Euler(0f, 0f, tilt);
        popping = false;
        popRoutine = null;
    }

    static float BackEaseOut(float k) {
        const float c1 = 2.2f; // タイトルのポップより少し強めに行きすぎて戻る
        const float c3 = c1 + 1f;
        float k1 = k - 1f;
        return 1f + c3 * k1 * k1 * k1 + c1 * k1 * k1;
    }
}
