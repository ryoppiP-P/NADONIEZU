// 投げたオブジェクトに一時的にアタッチするコンポーネント。
// OpenFractureのFractureクラスはパッケージ側にあり編集できないため、外部から補う。
//
// PlayerPunch.BreakFractureObject() と同じ手順を踏む：
//   1. Fracture側のRigidbodyのisKinematicを解除する
//   2. ComputeFracture() を自前で呼ぶ（衝突力のしきい値に依存しない）
//   3. 生成された破片を吹き飛ばす
//
// ポイント：OpenFractureは破片生成時に元オブジェクトの速度を継承させる
// （Fracture.CreateFragmentTemplate内 fragmentRigidBody.velocity = thisRigidBody.velocity）。
// 元がKinematic（velocity=0）のままだと破片は必ず静止状態で生まれるため、
// 破壊前に速度を流し込んでおくことで「ぶつかった勢いのまま破片が飛ぶ」状態を作る。
//
// 破片へ加える力は ForceMode.VelocityChange を使う。破片ごとに質量がバラバラ
// （0.4〜0.8kg程度）なので、Impulseだと軽い破片だけ極端に吹き飛んで見栄えが安定しないため。
// VelocityChangeなら指定値がそのまま「散る速度(m/s)」になり調整しやすい。
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class ThrownImpactor : MonoBehaviour {
    [Header("散乱の強さ")]
    [Tooltip("ぶつかった速度のうち、破片が散る速度に変換される割合")]
    [Range(0f, 5f)] public float scatterSpeedRatio = 1.2f;

    [Tooltip("破片が散る速度の上限(m/s)。暴走防止")]
    public float maxScatterSpeed = 22f;

    [Tooltip("破片が散る速度の下限(m/s)。軽く当てても最低限は散る")]
    public float minScatterSpeed = 8f;

    [Tooltip("破片ごとの速度のばらつき。1に近いほど速い破片と遅い破片の差が開いて散らばって見える")]
    [Range(0f, 1f)] public float speedVariation = 0.45f;

    [Tooltip("破片が飛ぶ向きのランダム性。0だと全部同じ方向に飛んで『かたまり』に見える")]
    [Range(0f, 2f)] public float directionRandomness = 0.9f;

    [Tooltip("破片の回転の激しさ(rad/s)")]
    public float spinSpeed = 20f;

    [Tooltip("この範囲内の破片だけに力を加える")]
    public float impactRadius = 3.5f;

    [Tooltip("破壊前にFracture本体へ流し込む速度の割合。全破片に同じ速度が乗るので、上げすぎると『かたまりのまま飛ぶ』ようになる")]
    [Range(0f, 2f)] public float momentumTransferRatio = 0.15f;

    [Header("発動条件")]
    [Tooltip("これ未満の速度では衝撃を伝播しない")]
    public float minSpeedToTransfer = 1.5f;

    [Tooltip("生成直後、破片同士の衝突を無視する時間(秒)。0にすると破片が互いに押し合って一瞬で止まる")]
    public float fragmentNoCollideTime = 0.4f;

    [Header("貫通")]
    [Tooltip("破壊した際、投げたオブジェクトが保持する速度の割合。1で減速なし、0で従来通り止まる")]
    [Range(0f, 1f)] public float pierceSpeedRetention = 0.65f;

    [Tooltip("貫通後、さらに別のFractureオブジェクトも壊せるようにする")]
    public bool allowMultiplePierce = true;

    [Tooltip("この時間が経過したら自動でコンポーネントを削除する（何にも当たらなかった場合の保険）")]
    public float autoRemoveTime = 10f;

    [Tooltip("破片生成完了コールバックが来ない場合に備えたフォールバック待機時間(秒)")]
    public float fallbackWaitTime = 0.5f;

    [Tooltip("動作確認用のログを出す。調整が終わったらfalseにしてよい")]
    public bool debugLog = true;

    [Header("IKD")]
    [Tooltip("投げつけてFractureオブジェクトを破壊したときのIKD加算量")]
    public int ikdGainOnBreak = 10;

    [Header("演出（Fractureを破壊した瞬間）")]
    [Tooltip("破壊時のヒットストップ時間(秒)")]
    public float breakHitStopDuration = 0.16f;

    [Tooltip("破壊時のヒットストップ中のタイムスケール")]
    [Range(0f, 1f)] public float breakHitStopTimeScale = 0.02f;

    [Tooltip("破壊時のカメラシェイクの強さ")]
    public float breakCameraShakeMagnitude = 0.4f;

    [Tooltip("破壊時のカメラシェイクの時間")]
    public float breakCameraShakeDuration = 0.28f;

    [Header("演出（Deformableにめり込んだだけ＝壊れない時）")]
    [Tooltip("めり込みヒットストップ時間(秒)")]
    public float dentHitStopDuration = 0.05f;

    [Tooltip("めり込みヒットストップ中のタイムスケール")]
    [Range(0f, 1f)] public float dentHitStopTimeScale = 0.15f;

    [Tooltip("めり込みカメラシェイクの強さ")]
    public float dentCameraShakeMagnitude = 0.08f;

    [Tooltip("めり込みカメラシェイクの時間")]
    public float dentCameraShakeDuration = 0.1f;

    Rigidbody rb;
    Vector3 lastVelocity;
    float removeAt;
    bool hasImpacted;
    bool impactApplied;

    // 衝突時点で確保しておく値（Fracture本体は直後に非アクティブ化されるため値で持つ）
    string pendingFragmentRootName;
    Vector3 pendingContactPoint;
    Vector3 pendingForceDir;
    float pendingScatterSpeed;
    Fracture pendingFracture;

    // 貫通用：衝突で速度が殺された次のフレームに復元するための保持値
    Vector3 pierceVelocity;
    bool restorePierceVelocity;

    void Awake() {
        rb = GetComponent<Rigidbody>();
    }

    /// <summary>投げた瞬間に呼ぶ。タイマーと記録速度をリセットする。</summary>
    public void BeginThrow() {
        hasImpacted = false;
        impactApplied = false;
        removeAt = Time.time + autoRemoveTime;
        lastVelocity = rb != null ? rb.linearVelocity : Vector3.zero;
    }

    void FixedUpdate() {
        if (rb == null) { Destroy(this); return; }

        // 貫通：衝突解決で殺された速度を、物理ステップ後にあらためて復元する
        // （OnCollisionEnter内で入れても、その後の解決で潰されることがあるため）
        if (restorePierceVelocity) {
            restorePierceVelocity = false;
            rb.linearVelocity = pierceVelocity;
        }

        // 衝突でRigidbodyの速度が変化する直前の値をここで保持しておく
        // （OnCollisionEnterの時点ではrb.linearVelocityが既に変化済みのことがあるため）
        lastVelocity = rb.linearVelocity;

        if (hasImpacted) return; // 衝撃処理中/処理済みはタイムアウト判定しない

        if (Time.time >= removeAt || rb.linearVelocity.magnitude < minSpeedToTransfer) {
            Destroy(this);
        }
    }

    void OnCollisionEnter(Collision collision) {
        if (hasImpacted) return;

        // === Deformable：Fractureじゃなくてもへこませる ===
        // Fractureと同一オブジェクトに両方付いていることは想定しない前提。
        // 先に処理しておくことで、Fractureで早期returnした場合でもへこみが漏れないようにする。
        var deformable = collision.collider.GetComponentInParent<Deformable>();
        if (deformable != null) {
            ContactPoint dcp = collision.GetContact(0);
            // lastVelocityベースの方が安定（col.impulseは0になることがある）
            float dentForce = lastVelocity.magnitude * (rb != null ? rb.mass : 1f);
            deformable.ApplyDent(dcp.point, -dcp.normal, dentForce);
            GameFeel.HitStop(dentHitStopDuration, dentHitStopTimeScale);
            GameFeel.Shake(dentCameraShakeMagnitude, dentCameraShakeDuration);
            // returnはしない：Fractureも同時に判定する
        }

        var fracture = collision.collider.GetComponentInParent<Fracture>();
        if (fracture == null) return;

        float impactSpeed = lastVelocity.magnitude;
        if (impactSpeed < minSpeedToTransfer) return;

        hasImpacted = true;

        GameFeel.HitStop(breakHitStopDuration, breakHitStopTimeScale);
        GameFeel.Shake(breakCameraShakeMagnitude, breakCameraShakeDuration);

        pendingScatterSpeed = Mathf.Clamp(impactSpeed * scatterSpeedRatio, minScatterSpeed, maxScatterSpeed);
        pendingContactPoint = collision.GetContact(0).point;
        pendingForceDir = lastVelocity.normalized;
        pendingFragmentRootName = fracture.gameObject.name + "Fragments";
        pendingFracture = fracture;

        // === Punchと同じ手順で自前で破壊する ===
        var frb = fracture.GetComponent<Rigidbody>();
        if (frb != null) {
            if (frb.isKinematic) frb.isKinematic = false;
            frb.linearVelocity = lastVelocity * momentumTransferRatio;
        }

        // === 貫通：衝突で殺される速度を保持し、勢いを残したまま進み続ける ===
        if (rb != null && pierceSpeedRetention > 0f) {
            pierceVelocity = pendingForceDir * (impactSpeed * pierceSpeedRetention);
            rb.linearVelocity = pierceVelocity;
            restorePierceVelocity = true;
        }

        fracture.callbackOptions.onCompleted.AddListener(OnFractureCompleted);

        bool alreadyFracturing = GameObject.Find(pendingFragmentRootName) != null;
        if (!alreadyFracturing && fracture.gameObject.activeSelf) {
            fracture.ComputeFracture();

            // 貫通で同じ破片へ複数回積算されないよう、実際に破壊を起こした一撃だけ加算する
            if (IKDManager.Instance != null)
                IKDManager.Instance.Add(ikdGainOnBreak);
            RouteTracker.Instance?.RegisterDestruction();
        }

        StartCoroutine(FallbackTimeout());
    }


    void OnFractureCompleted() {
        ApplyImpactNow();
    }

    IEnumerator FallbackTimeout() {
        yield return new WaitForSeconds(fallbackWaitTime);
        ApplyImpactNow();
    }

    void ApplyImpactNow() {
        if (impactApplied) return;
        impactApplied = true;

        if (pendingFracture != null) {
            pendingFracture.callbackOptions.onCompleted.RemoveListener(OnFractureCompleted);
        }

        // 破片ルートの子を直接辿る（生成直後はOverlapSphereだと物理シーン未同期で拾えないことがある）
        var root = GameObject.Find(pendingFragmentRootName);
        int affected = 0;

        // 力を加えた破片のコライダーを集めておく（後で相互衝突を一時的に切るため）
        // 自分自身のコライダーも含めることで、貫通中に自分が作った破片に阻まれないようにする
        var fragmentColliders = new System.Collections.Generic.List<Collider>();
        var selfCol = GetComponent<Collider>();
        if (selfCol != null) fragmentColliders.Add(selfCol);

        if (root != null) {
            foreach (Transform child in root.transform) {
                var childRb = child.GetComponent<Rigidbody>();
                if (childRb == null || childRb.isKinematic) continue; // Kinematicな破片は除外
                if (childRb == rb) continue;                          // 自分自身は除外
                if (Vector3.Distance(child.position, pendingContactPoint) > impactRadius) continue;

                var childCol = child.GetComponent<Collider>();
                if (childCol != null) fragmentColliders.Add(childCol);

                // 爆心からの放射方向を主軸に、ランダム方向・進行方向・上向きを混ぜる。
                // ランダム成分が無いと全破片がほぼ同じ向きに飛び、「散乱」ではなく
                // 「かたまりごと吹っ飛んだ」ように見えてしまう。
                Vector3 explosionDir = (child.position - pendingContactPoint).normalized;
                if (explosionDir.sqrMagnitude < 0.001f) explosionDir = Random.onUnitSphere;

                Vector3 dir = (explosionDir
                             + Random.onUnitSphere * directionRandomness
                             + pendingForceDir * 0.3f
                             + Vector3.up * 0.35f).normalized;

                // 速度にも個体差をつける。全部同じ速さだと互いに離れていかないため。
                float speed = pendingScatterSpeed * Random.Range(1f - speedVariation, 1f + speedVariation);

                // VelocityChange = 質量を無視して直接その速度を与える（破片ごとの質量差で暴れないように）
                childRb.AddForce(dir * speed, ForceMode.VelocityChange);
                childRb.AddTorque(Random.insideUnitSphere * spinSpeed, ForceMode.VelocityChange);
                affected++;
            }
        }

        // OpenFractureは破片にconvexなMeshColliderを付けるため、パズル状に切られた破片同士の
        // 凸包が大きく重なり合う。そのままだと生成直後に激しい押し戻しが起きて速度が相殺され、
        // せっかく与えた散乱速度が一瞬で消える。飛び出す間だけ相互衝突を無効化する。
        SetFragmentsIgnoreEachOther(fragmentColliders, true);

        if (debugLog)
            Debug.Log($"[ThrownImpactor] root={pendingFragmentRootName} found={(root != null)} affected={affected} scatterSpeed={pendingScatterSpeed:F1}m/s");

        StartCoroutine(DiagnoseThenCleanup(root, fragmentColliders));
    }

    static void SetFragmentsIgnoreEachOther(System.Collections.Generic.List<Collider> cols, bool ignore) {
        for (int i = 0; i < cols.Count; i++) {
            for (int j = i + 1; j < cols.Count; j++) {
                if (cols[i] == null || cols[j] == null) continue;
                Physics.IgnoreCollision(cols[i], cols[j], ignore);
            }
        }
    }

    // 力を加えた次の物理フレームで破片が実際にどれだけ動いているかを記録し（診断用）、
    // 十分に飛び散ったところで相互衝突を元に戻す。
    IEnumerator DiagnoseThenCleanup(GameObject root, System.Collections.Generic.List<Collider> fragmentColliders) {
        yield return new WaitForFixedUpdate();

        if (debugLog && root != null) {
            int i = 0;
            foreach (Transform child in root.transform) {
                var crb = child.GetComponent<Rigidbody>();
                if (crb == null) continue;
                Debug.Log($"[ThrownImpactor][diag] frag{i} v={crb.linearVelocity.magnitude:F2}m/s spin={crb.angularVelocity.magnitude:F1}rad/s mass={crb.mass:F2}");
                i++;
            }
        }

        // 破片が離れきるまで待ってから衝突を復活させる（重なったまま戻すと弾け飛ぶため）
        yield return new WaitForSeconds(fragmentNoCollideTime);
        SetFragmentsIgnoreEachOther(fragmentColliders, false);

        // まだ十分な速度が残っていれば、次のFractureオブジェクトも壊せるように状態を戻す
        bool canPierceAgain = allowMultiplePierce
            && rb != null
            && rb.linearVelocity.magnitude >= minSpeedToTransfer
            && Time.time < removeAt;

        if (canPierceAgain) {
            if (pendingFracture != null)
                pendingFracture.callbackOptions.onCompleted.RemoveListener(OnFractureCompleted);
            pendingFracture = null;
            impactApplied = false;
            hasImpacted = false;   // 次の衝突を受け付ける
            if (debugLog) Debug.Log($"[ThrownImpactor] 貫通継続 speed={rb.linearVelocity.magnitude:F1}m/s");
        } else {
            Destroy(this);
        }
    }
}
