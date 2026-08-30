// タイトル画面のボタンに付ける、ホバーでちょっと弾む/傾くだけの軽いバカゲー的手触り。
// 会話選択肢のChoiceButtonFeelと同じ考え方だが、ルート別演出は不要なのでホバー分だけの簡易版。
// 加えて、任意でホバーしていない間ゆっくり呼吸するように拡縮できる（クリック誘導用）。
// TitleControllerの登場ポップインと同じRectTransform.localScaleを取り合わないよう、
// 呼吸は明示的にSetBreatheEnabled(true)されるまで一切動かない。
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

public class TitleButtonFeel : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler {
    public float breathePeriod = 1.7f;
    public float breatheMaxScale = 1.045f;

    Vector3 baseScale = Vector3.one;
    bool hovering = false;
    bool breatheEnabled = false;
    Coroutine wobbleRoutine;

    void Awake() {
        baseScale = transform.localScale;
    }

    void Update() {
        if (hovering || wobbleRoutine != null) return;
        if (!breatheEnabled) return; // 登場演出などが終わるまでは何もしない（他スクリプトの制御に任せる）

        float wave = (Mathf.Sin(Time.time * Mathf.PI * 2f / breathePeriod) + 1f) * 0.5f;
        transform.localScale = baseScale * Mathf.Lerp(1f, breatheMaxScale, wave);
    }

    /// <summary>登場ポップインが終わってから呼ぶ。以降、非ホバー時にゆっくり呼吸させる。</summary>
    public void SetBreatheEnabled(bool enabled) { breatheEnabled = enabled; }

    public void OnPointerEnter(PointerEventData eventData) {
        hovering = true;
        Restart(Wobble(1.08f, 4f));
    }

    public void OnPointerExit(PointerEventData eventData) {
        hovering = false;
        Restart(Wobble(1f, 0f));
    }

    void Restart(IEnumerator routine) {
        if (wobbleRoutine != null) StopCoroutine(wobbleRoutine);
        wobbleRoutine = StartCoroutine(routine);
    }

    IEnumerator Wobble(float targetScale, float tiltDegrees) {
        float duration = 0.15f;
        Vector3 fromScale = transform.localScale;
        Quaternion fromRot = transform.localRotation;
        Vector3 toScaleVec = baseScale * targetScale;
        Quaternion toRot = Quaternion.Euler(0f, 0f, tiltDegrees);

        float t = 0f;
        while (t < duration) {
            t += Time.unscaledDeltaTime;
            float k = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / duration), 3f);
            transform.localScale = Vector3.Lerp(fromScale, toScaleVec, k);
            transform.localRotation = Quaternion.Lerp(fromRot, toRot, k);
            yield return null;
        }
        transform.localScale = toScaleVec;
        transform.localRotation = toRot;
        wobbleRoutine = null; // 呼吸(Update)に制御を返す
    }
}
