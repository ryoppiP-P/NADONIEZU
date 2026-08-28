// 殴った/ぶつけて壊した時などの「手応え」演出（ヒットストップ・カメラシェイク）を一箇所にまとめる。
// PlayerPunchやThrownImpactorなど、複数の発生源から共通で呼べるようにするための静的窓口。
//
// カメラシェイクの実際の位置反映はCameraFollow側のLateUpdateで行う。
// CameraFollowは毎フレームtransform.positionを計算し直して直接上書きするため、
// ここで独自にtransform.localPositionを動かしても次のLateUpdateで即座に消されてしまう。
// そのため「今フレームのシェイクオフセット」を計算して渡すだけにし、
// 実際にcameraPosへ足し込むのはCameraFollow側の責任にする。
using System.Collections;
using UnityEngine;

public class GameFeel : MonoBehaviour {
    public static GameFeel Instance { get; private set; }

    // ヒットストップ
    float hitStopEndTime;      // unscaledTime基準。延長判定に使う
    Coroutine hitStopRoutine;

    // カメラシェイク
    float shakeMagnitude;
    float shakeDuration;       // 減衰カーブ計算用（開始時点の総尺）
    float shakeEndTime;        // unscaledTime基準

    static void EnsureInstance() {
        if (Instance != null) return;
        var go = new GameObject("GameFeel");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<GameFeel>();
    }

    void Awake() {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>一瞬だけ時間を止めて「当たった」手応えを出す。既存の方が長く残っているなら上書きしない。</summary>
    public static void HitStop(float duration, float timeScale) {
        EnsureInstance();
        Instance.DoHitStop(duration, timeScale);
    }

    void DoHitStop(float duration, float timeScale) {
        float endTime = Time.unscaledTime + duration;
        if (hitStopRoutine != null && endTime <= hitStopEndTime) return;

        hitStopEndTime = endTime;
        if (hitStopRoutine != null) StopCoroutine(hitStopRoutine);
        hitStopRoutine = StartCoroutine(HitStopRoutine(timeScale));
    }

    IEnumerator HitStopRoutine(float timeScale) {
        Time.timeScale = timeScale;
        while (Time.unscaledTime < hitStopEndTime) yield return null;

        // ポーズメニューが割り込んでTime.timeScaleを0にしていた場合は、そちらを優先して上書きしない
        if (Time.timeScale == timeScale) Time.timeScale = 1f;
        hitStopRoutine = null;
    }

    /// <summary>カメラを揺らす。既存のシェイクの方が強く/長く残っているなら上書きしない。</summary>
    public static void Shake(float magnitude, float duration) {
        EnsureInstance();
        Instance.DoShake(magnitude, duration);
    }

    void DoShake(float magnitude, float duration) {
        float endTime = Time.unscaledTime + duration;
        if (endTime <= shakeEndTime && magnitude <= shakeMagnitude) return;

        shakeMagnitude = magnitude;
        shakeDuration = duration;
        shakeEndTime = endTime;
    }

    /// <summary>今フレームのカメラシェイクオフセット（ワールド空間の微小値）。CameraFollowが毎LateUpdateで加算する。</summary>
    public static Vector3 GetShakeOffset() {
        return Instance != null ? Instance.ComputeShakeOffset() : Vector3.zero;
    }

    Vector3 ComputeShakeOffset() {
        float remaining = shakeEndTime - Time.unscaledTime;
        if (remaining <= 0f || shakeDuration <= 0f) return Vector3.zero;

        // 二乗で減衰させ、後半で急速に収まる「ビシッ」とした止まり方にする
        float t = Mathf.Clamp01(remaining / shakeDuration);
        float strength = shakeMagnitude * t * t;

        Vector3 offset = Random.insideUnitSphere * strength;
        offset.z = 0f;
        return offset;
    }
}
