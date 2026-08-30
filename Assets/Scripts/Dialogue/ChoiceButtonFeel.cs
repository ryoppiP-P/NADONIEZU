// 選択肢ボタンに付ける「バカゲー的な手触り」担当。
// ・ホバーでちょっと弾む/傾くウォブル
// ・ホバーしていなくても常に出続けるルート別アンビエントエフェクト
//     恋愛=ハートがふわふわ、狂気=不穏なオーラがじわじわ明滅、反抗=稲妻がバチバチ
// ・選ばれた瞬間に、その仕上げとして一発大きめの演出
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ChoiceButtonFeel : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler {
    public RouteType route;

    RectTransform rt;
    Transform canvasTransform;
    Vector3 baseScale = Vector3.one;
    Coroutine wobbleRoutine;
    Coroutine ambientRoutine;
    Coroutine emberRoutine;

    Image aura; // 狂気ルート専用。他ルートでは非表示のまま使わない

    void Awake() {
        rt = (RectTransform)transform;
        baseScale = transform.localScale;
        canvasTransform = GetComponentInParent<Canvas>()?.transform;
        EnsureAura();
    }

    // ボタンより一回り大きい、じわっと明滅する紫の後光。狂気ルートの時だけ動かす
    void EnsureAura() {
        var existing = transform.Find("Aura");
        if (existing != null) { aura = existing.GetComponent<Image>(); return; }

        var go = new GameObject("Aura", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        go.transform.SetAsFirstSibling(); // ボタン本体より後ろ(下)に敷く

        var auraRT = (RectTransform)go.transform;
        auraRT.anchorMin = new Vector2(0.5f, 0.5f);
        auraRT.anchorMax = new Vector2(0.5f, 0.5f);
        auraRT.pivot = new Vector2(0.5f, 0.5f);
        auraRT.anchoredPosition = Vector2.zero;
        auraRT.sizeDelta = rt != null ? rt.sizeDelta + new Vector2(160f, 160f) : new Vector2(780f, 320f);

        aura = go.AddComponent<Image>();
        aura.sprite = ProceduralIcons.SoftAura(Color.white, 128);
        aura.color = new Color(0.78f, 0.15f, 0.95f, 0f); // 派手めのホットマゼンタ寄り紫
        aura.raycastTarget = false;
        go.SetActive(false);
    }

    void OnEnable() {
        Refresh();
    }

    /// <summary>ホバー/演出状態をリセットし、現在のrouteでアンビエントループを起動し直す。
    /// choicesPanel側のSetActive(true)がボタン個々のSetActive(true)より先に走り、
    /// そちらでOnEnable()が（routeがまだ確定していないタイミングで）呼ばれてしまうことがあるため、
    /// DialogueUI.ShowChoices()側でrouteを確定させた直後に明示的にも呼んでもらう。</summary>
    public void Refresh() {
        transform.localScale = baseScale;
        transform.localRotation = Quaternion.identity;

        if (ambientRoutine != null) StopCoroutine(ambientRoutine);
        if (emberRoutine != null) { StopCoroutine(emberRoutine); emberRoutine = null; }
        ambientRoutine = StartCoroutine(AmbientLoop());
    }

    void OnDisable() {
        if (ambientRoutine != null) { StopCoroutine(ambientRoutine); ambientRoutine = null; }
        if (emberRoutine != null) { StopCoroutine(emberRoutine); emberRoutine = null; }
        if (aura != null) aura.gameObject.SetActive(false);
    }

    IEnumerator AmbientLoop() {
        if (canvasTransform == null) canvasTransform = GetComponentInParent<Canvas>()?.transform;

        if (route == RouteType.Madness) {
            aura.gameObject.SetActive(true);
            if (canvasTransform != null) emberRoutine = StartCoroutine(EmberLoop());
            yield return PulseAuraForever();
            yield break;
        }

        if (aura != null) aura.gameObject.SetActive(false);
        if (canvasTransform == null) yield break;

        while (true) {
            if (route == RouteType.Romance) {
                int burst = Random.Range(1, 3);
                for (int i = 0; i < burst; i++) AmbientFX.SpawnHeart(canvasTransform, rt);
                yield return new WaitForSecondsRealtime(Random.Range(0.12f, 0.28f));
            } else if (route == RouteType.Rebel) {
                int burst = Random.Range(1, 3);
                for (int i = 0; i < burst; i++) AmbientFX.SpawnSpark(canvasTransform, rt);
                yield return new WaitForSecondsRealtime(Random.Range(0.1f, 0.3f));
            } else {
                yield return null; // Normalルートは何もしない（平常運転が売り）
            }
        }
    }

    IEnumerator PulseAuraForever() {
        while (true) {
            float t = 0f;
            float duration = Random.Range(0.6f, 1.0f); // 速く・大きく脈打たせる
            while (t < duration) {
                t += Time.unscaledDeltaTime;
                float k = t / duration;
                // sin波でドクドクと脈打つ不穏な呼吸
                float wave = (Mathf.Sin(k * Mathf.PI * 2f - Mathf.PI * 0.5f) + 1f) * 0.5f;
                float scale = Mathf.Lerp(0.85f, 1.55f, wave);
                var c = aura.color;
                c.a = Mathf.Lerp(0.28f, 0.85f, wave);
                aura.color = c;
                aura.rectTransform.localScale = Vector3.one * scale;
                yield return null;
            }
        }
    }

    IEnumerator EmberLoop() {
        while (true) {
            AmbientFX.SpawnEmber(canvasTransform, rt);
            yield return new WaitForSecondsRealtime(Random.Range(0.12f, 0.26f));
        }
    }

    public void OnPointerEnter(PointerEventData eventData) {
        Restart(Wobble(1.1f, 5f));
    }

    public void OnPointerExit(PointerEventData eventData) {
        Restart(Wobble(1f, 0f));
    }

    void Restart(IEnumerator routine) {
        if (wobbleRoutine != null) StopCoroutine(wobbleRoutine);
        wobbleRoutine = StartCoroutine(routine);
    }

    IEnumerator Wobble(float targetScale, float tiltDegrees) {
        float duration = 0.16f;
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
    }

    /// <summary>この選択肢が実際にクリックされた瞬間に呼ぶ。ルートに応じて一発大きめの演出を出す。</summary>
    public void PlaySelectBurst() {
        if (canvasTransform == null) canvasTransform = GetComponentInParent<Canvas>()?.transform;
        if (canvasTransform == null) return;

        switch (route) {
            case RouteType.Romance:
                for (int i = 0; i < 10; i++) AmbientFX.SpawnHeart(canvasTransform, rt);
                break;
            case RouteType.Rebel:
                for (int i = 0; i < 6; i++) AmbientFX.SpawnSpark(canvasTransform, rt);
                break;
            case RouteType.Madness:
                if (aura != null) StartCoroutine(AuraSpike());
                for (int i = 0; i < 8; i++) AmbientFX.SpawnEmber(canvasTransform, rt);
                break;
        }
    }

    IEnumerator AuraSpike() {
        float t = 0f;
        float duration = 0.4f;
        while (t < duration) {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            float burst = 1f - Mathf.Pow(1f - k, 2f);
            var c = aura.color;
            c.a = Mathf.Lerp(0.95f, 0.2f, burst);
            aura.color = c;
            aura.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.8f, 1f, burst);
            yield return null;
        }
    }
}
