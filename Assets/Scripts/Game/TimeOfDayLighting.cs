// GameTimeManagerの経過時間(9:00〜17:00)に応じて、Directional Lightの角度・色・強さを
// 朝→昼→夕方でなめらかに変化させる。新規モデル/エフェクトを足さずに「一日の重み」を出すための演出。
//
// 太陽の向き(Y軸/方位)は既存のシーンライティング・影の落ち方に合わせて調整済みのため触らない。
// 変化させるのは「高さ(X軸=仰角)」「色」「強さ」の3つだけ。
using UnityEngine;

public class TimeOfDayLighting : MonoBehaviour {
    [Header("参照")]
    [Tooltip("未設定ならシーン内のDirectional Lightを自動で探す")]
    public Light sun;

    [Header("太陽の高さ（X軸角度）")]
    [Tooltip("横軸=一日の進み具合(0=開始時刻, 1=終了時刻)、縦軸=X軸の角度(度)")]
    public AnimationCurve elevationCurve = new AnimationCurve(
        new Keyframe(0f, 25f),
        new Keyframe(0.25f, 42f),
        new Keyframe(0.5f, 55f),
        new Keyframe(0.75f, 42f),
        new Keyframe(1f, 22f)
    );

    [Header("太陽の強さ")]
    public AnimationCurve intensityCurve = new AnimationCurve(
        new Keyframe(0f, 1.6f),
        new Keyframe(0.25f, 1.9f),
        new Keyframe(0.5f, 2.2f),
        new Keyframe(0.75f, 1.9f),
        new Keyframe(1f, 1.5f)
    );

    [Header("太陽の色（朝夕は暖色、正午は白に近づく）")]
    public Gradient colorGradient;

    GameTimeManager timeManager;

    void Awake() {
        if (sun == null) {
            var lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            foreach (var l in lights) {
                if (l.type == LightType.Directional) { sun = l; break; }
            }
        }

        // Unityの仕様上、AddComponent直後のGradientフィールドは「null」でも「キー0個」でもなく
        // 白2キーのダミーが自動で入った状態になる。「未設定かどうか」を判定できないため、
        // 既定値は常にコード側で上書きする（朝(暖)→正午(白)→夕方(暖)）。
        colorGradient = new Gradient();
        colorGradient.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color32(255, 225, 190, 255), 0f),
                new GradientColorKey(new Color32(255, 241, 224, 255), 0.25f),
                new GradientColorKey(Color.white, 0.5f),
                new GradientColorKey(new Color32(255, 238, 214, 255), 0.75f),
                new GradientColorKey(new Color32(255, 196, 140, 255), 1f),
            },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
        );
    }

    void Start() {
        timeManager = GameTimeManager.Instance;
        Apply(); // 開始時刻の見た目を即座に反映
    }

    void Update() {
        if (sun == null) return;
        if (timeManager == null) timeManager = GameTimeManager.Instance;
        if (timeManager == null) return;

        Apply();
    }

    void Apply() {
        float progress = GetDayProgress();

        Vector3 e = sun.transform.eulerAngles;
        e.x = elevationCurve.Evaluate(progress);
        sun.transform.eulerAngles = e;

        sun.intensity = intensityCurve.Evaluate(progress);
        sun.color = colorGradient.Evaluate(progress);
    }

    /// <summary>開始時刻を0、終了時刻を1とした一日の進み具合。</summary>
    float GetDayProgress() {
        float elapsed = timeManager.GetCurrentMinutesRaw() - timeManager.startMinutes;
        return Mathf.Clamp01(elapsed / timeManager.limitMinutes);
    }
}
