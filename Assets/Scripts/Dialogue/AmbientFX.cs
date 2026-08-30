// 会話選択肢ボタンの「常時ちょっとしたエフェクト」担当（ホバーしていなくても出る）。
// 実際のParticleSystemは使わず、UI Imageを都度生成してアニメーションさせる自作パーティクル。
// 理由: 会話UIのCanvasはScreen Space - Overlay(常に最前面に描画)のため、
// 通常のワールド空間ParticleSystemを使うとUIの下に隠れてしまう。UI Imageなら確実に最前面に乗る。
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public static class AmbientFX {
    static Sprite heartSprite;
    static Sprite boltSprite;
    static Sprite glowSprite;

    // ハートは手描きの絵文字風画像(Assets/Resources/love.png)を使う。プロシージャル生成だと形がいびつだったため
    static Sprite HeartSprite => heartSprite != null ? heartSprite : (heartSprite = Resources.Load<Sprite>("love"));
    static Sprite BoltSprite => boltSprite != null ? boltSprite : (boltSprite = ProceduralIcons.Bolt(Color.white, 64));
    static Sprite GlowSprite => glowSprite != null ? glowSprite : (glowSprite = ProceduralIcons.SoftAura(Color.white, 128));

    /// <summary>ボタンの外周（4辺のどれか）に沿ったランダムな1点を、少し外側にはみ出させて返す。</summary>
    public static Vector3 RandomPointAroundButton(RectTransform buttonRT, float outset) {
        Vector3[] c = new Vector3[4];
        buttonRT.GetWorldCorners(c); // 0:BL 1:TL 2:TR 3:BR

        Vector3 a, b, outward;
        switch (Random.Range(0, 4)) {
            case 0: a = c[0]; b = c[3]; outward = Vector3.down; break;  // 下辺
            case 1: a = c[1]; b = c[2]; outward = Vector3.up; break;    // 上辺
            case 2: a = c[0]; b = c[1]; outward = Vector3.left; break;  // 左辺
            default: a = c[3]; b = c[2]; outward = Vector3.right; break; // 右辺
        }
        Vector3 p = Vector3.Lerp(a, b, Random.value);
        return p + outward * outset;
    }

    /// <summary>恋愛ルート：ボタンの縁からハートがふわっと浮かぶ</summary>
    public static void SpawnHeart(Transform canvasTransform, RectTransform buttonRT) {
        Vector3 pos = RandomPointAroundButton(buttonRT, Random.Range(6f, 22f));
        var img = CreateParticle(canvasTransform, pos, HeartSprite, Random.Range(20f, 32f));
        var pink = new Color(1f, 0.35f, 0.55f);
        img.color = Color.Lerp(pink, new Color(1f, 0.75f, 0.82f), Random.value);

        img.gameObject.AddComponent<AmbientParticleRunner>()
            .PlayDrift(driftUpBias: 0.8f, distance: Random.Range(28f, 55f), duration: Random.Range(0.9f, 1.3f), spin: Random.Range(-35f, 35f));
    }

    /// <summary>反抗ルート：ボタンの縁で稲妻がバチッと激しく光る（後光つきで大きめ・明るめ）</summary>
    public static void SpawnSpark(Transform canvasTransform, RectTransform buttonRT) {
        Vector3 pos = RandomPointAroundButton(buttonRT, Random.Range(2f, 16f));
        Color sparkColor = Random.value < 0.5f ? new Color(1f, 0.97f, 0.55f) : new Color(0.75f, 0.95f, 1f); // 電光っぽい黄or水色

        // 後光（ボルトの下敷きに大きく広がる光）
        var glow = CreateParticle(canvasTransform, pos, GlowSprite, Random.Range(80f, 130f));
        glow.color = sparkColor;
        glow.gameObject.AddComponent<AmbientParticleRunner>().PlayFlash(Random.Range(0.18f, 0.3f));

        // 本体の稲妻
        var img = CreateParticle(canvasTransform, pos, BoltSprite, Random.Range(48f, 80f));
        img.color = Color.Lerp(sparkColor, Color.white, 0.55f);
        img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-25f, 25f));

        img.gameObject.AddComponent<AmbientParticleRunner>()
            .PlayFlash(Random.Range(0.14f, 0.24f));
    }

    /// <summary>狂気ルート：オーラの中を漂う光る残り火</summary>
    public static void SpawnEmber(Transform canvasTransform, RectTransform buttonRT) {
        Vector3 pos = RandomPointAroundButton(buttonRT, Random.Range(-10f, 30f));
        var img = CreateParticle(canvasTransform, pos, GlowSprite, Random.Range(24f, 44f));
        img.color = Color.Lerp(new Color(0.85f, 0.35f, 1f), new Color(0.55f, 0.15f, 0.75f), Random.value);

        img.gameObject.AddComponent<AmbientParticleRunner>()
            .PlayDrift(driftUpBias: 0.4f, distance: Random.Range(20f, 45f), duration: Random.Range(1.1f, 1.6f), spin: Random.Range(-20f, 20f));
    }

    static Image CreateParticle(Transform parent, Vector3 worldPos, Sprite sprite, float size) {
        var go = new GameObject("FX", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.transform.SetAsLastSibling();

        var rt = (RectTransform)go.transform;
        rt.position = worldPos;
        rt.sizeDelta = new Vector2(size, size);
        rt.localScale = Vector3.zero;

        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        return img;
    }
}

/// <summary>1個のFX用UI Imageの寿命とアニメーションを管理し、終わったら自分で消える。</summary>
class AmbientParticleRunner : MonoBehaviour {
    public void PlayDrift(float driftUpBias, float distance, float duration, float spin) {
        StartCoroutine(Drift(driftUpBias, distance, duration, spin));
    }

    IEnumerator Drift(float driftUpBias, float distance, float duration, float spin) {
        var rt = (RectTransform)transform;
        var img = GetComponent<Image>();

        // CreateParticle()でrt.position(ワールド座標)を使って生成位置を決めているため、
        // その時点のanchoredPositionには既に正しい値が入っている。ここをVector2.zero起点にすると
        // 生成位置を無視してCanvas中心へワープしてしまうので、必ず現在値を始点にする。
        Vector2 startPos = rt.anchoredPosition;

        Vector2 dir = Random.insideUnitCircle;
        dir.y = Mathf.Abs(dir.y) * driftUpBias + (1f - driftUpBias) * 0.2f;
        dir.Normalize();
        Vector2 endPos = startPos + dir * distance;

        float t = 0f;
        while (t < duration) {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            float rise = 1f - Mathf.Pow(1f - k, 2f);

            rt.anchoredPosition = Vector2.Lerp(startPos, endPos, rise) + Vector2.down * (k * k * 18f);
            rt.localRotation = Quaternion.Euler(0f, 0f, spin * k);

            float popIn = Mathf.Clamp01(k / 0.2f);
            float shrink = 1f - Mathf.Pow(k, 3f) * 0.25f;
            rt.localScale = Vector3.one * (popIn * shrink);

            var c = img.color; c.a = 1f - Mathf.Pow(k, 2f); img.color = c;
            yield return null;
        }
        Destroy(gameObject);
    }

    public void PlayFlash(float duration) {
        StartCoroutine(Flash(duration));
    }

    IEnumerator Flash(float duration) {
        var rt = (RectTransform)transform;
        var img = GetComponent<Image>();

        float t = 0f;
        while (t < duration) {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            // 一瞬でパッと膨らみ、すぐ萎んで消える「バチッ」とした光り方
            float scale = k < 0.3f ? Mathf.Lerp(0.2f, 1.15f, k / 0.3f) : Mathf.Lerp(1.15f, 0.7f, (k - 0.3f) / 0.7f);
            rt.localScale = Vector3.one * scale;

            var c = img.color; c.a = 1f - k; img.color = c;
            yield return null;
        }
        Destroy(gameObject);
    }
}
