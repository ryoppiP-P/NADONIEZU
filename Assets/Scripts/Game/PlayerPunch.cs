using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerPunch : MonoBehaviour {
    [Header("参照")]
    public Transform punchOrigin;
    public Transform aimDirection;
    [Tooltip("PlayerActionsを設定すると、interactRangeと同じ距離になる")]
    public PlayerActions playerActions;

    [Header("判定")]
    public float range = 2.0f;
    public float radius = 0.4f;
    public LayerMask hitMask = ~0;

    [Header("巻き添え範囲")]
    [Tooltip("レイキャスト/スフィアキャストで着弾した地点を中心に、この半径の球状範囲にいる対象全部にパンチ効果を適用する")]
    public float splashRadius = 0.7f;

    [Header("威力")]
    [Tooltip("押し込む方向への吹き飛ばす力")]
    public float punchForce = 40f;

    [Tooltip("上向きの追加力（浮き上がり）")]
    public float upwardForce = 8f;

    [Tooltip("着弾点からの回転の強さ")]
    public float torqueForce = 20f;

    [Header("クールダウン")]
    public float cooldown = 0.5f;

    [Header("IKD")]
    [Tooltip("Fractureオブジェクトを殴って破壊したときのIKD加算量")]
    public int ikdGainOnBreak = 10;

    [Header("演出（通常ヒット：押し出し/へこみ）")]
    [Tooltip("ヒットストップ時間(秒)")]
    public float hitStopDuration = 0.08f;

    [Tooltip("ヒットストップ中のタイムスケール")]
    [Range(0f, 1f)] public float hitStopTimeScale = 0.05f;

    [Tooltip("カメラシェイクの強さ")]
    public float cameraShakeMagnitude = 0.15f;

    [Tooltip("カメラシェイクの時間")]
    public float cameraShakeDuration = 0.15f;

    [Header("演出（破壊ヒット：Fractureを壊した瞬間は強化する）")]
    [Tooltip("破壊時のヒットストップ時間(秒)")]
    public float breakHitStopDuration = 0.16f;

    [Tooltip("破壊時のヒットストップ中のタイムスケール")]
    [Range(0f, 1f)] public float breakHitStopTimeScale = 0.02f;

    [Tooltip("破壊時のカメラシェイクの強さ")]
    public float breakCameraShakeMagnitude = 0.4f;

    [Tooltip("破壊時のカメラシェイクの時間")]
    public float breakCameraShakeDuration = 0.28f;

    float lastPunchTime = -999f;

    void Awake() {
        if (punchOrigin == null) punchOrigin = transform;
        if (aimDirection == null && Camera.main != null)
            aimDirection = Camera.main.transform;

        if (playerActions == null)
            playerActions = GetComponent<PlayerActions>();
    }

    public void TryPunch() {
        if (Time.unscaledTime - lastPunchTime < cooldown) return;
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsActive) return;

        lastPunchTime = Time.unscaledTime;

        DoPunchCast();
    }

    void DoPunchCast() {
        // PlayerActions.ForwardCast()と同じように、カメラの位置からカメラの向きにレイを飛ばす。
        // punchOrigin(プレイヤーの体の位置)を原点にすると、肩越しカメラでカメラ位置がプレイヤーのやや後ろにある分、画面中心と実際に狙った位置がパララックスでズレる。
        Vector3 origin = aimDirection != null ? aimDirection.position : punchOrigin.position;
        Vector3 dir = aimDirection != null ? aimDirection.forward : punchOrigin.forward;

        float actualRange = playerActions != null ? playerActions.interactRange : range;

        // 複数の判定で候補を集める
        var rayHits = Physics.RaycastAll(origin, dir, actualRange, hitMask, QueryTriggerInteraction.Ignore);
        var sphereHits = Physics.SphereCastAll(origin, radius, dir, actualRange, hitMask, QueryTriggerInteraction.Ignore);

        // 一番近いものを着弾点として選ぶ
        RaycastHit closest = new RaycastHit();
        float closestDist = float.MaxValue;
        bool found = false;

        foreach (var h in rayHits) {
            if (!IsValidHit(h)) continue;
            if (h.distance < closestDist) {
                closestDist = h.distance;
                closest = h;
                found = true;
            }
        }

        foreach (var h in sphereHits) {
            if (!IsValidHit(h)) continue;
            if (h.distance < closestDist) {
                closestDist = h.distance;
                closest = h;
                found = true;
            }
        }

        if (!found) {
            Debug.Log($"[Punch] Missed (range={actualRange})");
            return;
        }

        // 着弾点を中心にした巻き添え範囲で、該当する対象全部にパンチ効果を適用する
        var splashHits = Physics.OverlapSphere(closest.point, splashRadius, hitMask, QueryTriggerInteraction.Ignore);
        var processedRoots = new System.Collections.Generic.HashSet<Transform>();

        // 演出（カメラシェイク・ヒットストップ・SE）は着弾点で1回だけ。
        // 巻き添え範囲内にFractureがひとつでもあれば「破壊ヒット」として強めの演出にする
        bool willBreak = false;
        foreach (var col in splashHits) {
            if (col == null) continue;
            if (col.transform.root == transform.root) continue;
            if (col.GetComponentInParent<Fracture>() != null) { willBreak = true; break; }
        }
        PlayPunchPresentation(closest.point, willBreak);

        int hitCount = 0;
        foreach (var col in splashHits) {
            if (col == null) continue;
            if (col.transform.root == transform.root) continue;
            if (!processedRoots.Add(col.transform.root)) continue; // 同一オブジェクトの重複コライダーは1回だけ処理

            Vector3 point = (col == closest.collider) ? closest.point : SafeClosestPoint(col, closest.point);
            Vector3 normal = (col == closest.collider) ? closest.normal : (origin - point).normalized;
            ApplyPunchEffect(col, point, normal);
            hitCount++;
        }

        Debug.Log($"[Punch] Impact: {closest.collider.name} at distance {closestDist:F2}, splash hit {hitCount} object(s)");
    }

    // Collider.ClosestPointはBox/Sphere/Capsule/凸なMeshColliderしか対応していない。
    // 非対応なMeshCollider（壁や凹んだモデルなど）で呼ぶと警告が出るので、対応外な形状は着弾点そのもので代用する。
    Vector3 SafeClosestPoint(Collider col, Vector3 fallbackPoint) {
        bool supported = col is BoxCollider || col is SphereCollider || col is CapsuleCollider;
        if (!supported && col is MeshCollider mc) supported = mc.convex;
        return supported ? col.ClosestPoint(fallbackPoint) : fallbackPoint;
    }

    bool IsValidHit(RaycastHit h) {
        if (h.collider == null) return false;
        if (h.collider.transform.root == transform.root) return false;
        if (h.distance <= 0.001f) return false;
        return true;
    }

    // === 演出（カメラシェイク・ヒットストップ・SE）：パンチ1回につき1度だけ呼ぶ ===
    void PlayPunchPresentation(Vector3 point, bool strong) {
        if (strong) {
            GameFeel.HitStop(breakHitStopDuration, breakHitStopTimeScale);
            GameFeel.Shake(breakCameraShakeMagnitude, breakCameraShakeDuration);
        } else {
            GameFeel.HitStop(hitStopDuration, hitStopTimeScale);
            GameFeel.Shake(cameraShakeMagnitude, cameraShakeDuration);
        }
        AudioManager.Instance.PlaySEAtPosition(SE.temp, point);
    }

    // === 対象1件ごとの物理的な効果（破壊/変形/押し飛ばし）。範囲内の対象それぞれに呼ぶ ===
    void ApplyPunchEffect(Collider collider, Vector3 point, Vector3 normal) {
        // 1. Fracture コンポーネントがあれば破壊
        var fracture = collider.GetComponentInParent<Fracture>();
        if (fracture != null) {
            BreakFractureObject(fracture, point);
            return;
        }

        // 2. Deformable ならへこませる
        var deformable = collider.GetComponentInParent<Deformable>();
        if (deformable != null) {
            // normalは面から外向きなので、めり込む方向は逆向き
            deformable.ApplyDent(point, -normal, punchForce);
            return;
        }

        // 3. Rigidbodyがあれば吹き飛ばす
        var rb = collider.attachedRigidbody;
        if (rb != null && !rb.isKinematic) {
            Vector3 forceDir = aimDirection != null ? aimDirection.forward : punchOrigin.forward;

            // 前方への吹き飛ばし + 上向きの追加力
            Vector3 finalForce = forceDir * punchForce + Vector3.up * upwardForce;
            rb.AddForceAtPosition(finalForce, point, ForceMode.Impulse);

            // ランダムな回転を加える
            Vector3 randomTorque = new Vector3(
                Random.Range(-1f, 1f),
                Random.Range(-1f, 1f),
                Random.Range(-1f, 1f)
            ) * torqueForce;
            rb.AddTorque(randomTorque, ForceMode.Impulse);
        }
    }

    void BreakFractureObject(Fracture fracture, Vector3 hitPoint) {
        var rb = fracture.GetComponent<Rigidbody>();
        if (rb != null && rb.isKinematic) {
            rb.isKinematic = false;
        }

        fracture.ComputeFracture();

        if (IKDManager.Instance != null)
            IKDManager.Instance.Add(ikdGainOnBreak);
        RouteTracker.Instance?.RegisterDestruction();

        // FragmentDecayが Fractureの 直後に、アタッチしているので、爆発力はそっちへ渡す
        StartCoroutine(ApplyExplosionToFragments(fracture, hitPoint));
    }

    System.Collections.IEnumerator ApplyExplosionToFragments(Fracture fracture, Vector3 hitPoint) {
        yield return null;
        yield return new WaitForSeconds(0.1f);

        string fragmentRootName = $"{fracture.gameObject.name}Fragments";
        var root = GameObject.Find(fragmentRootName);
        if (root == null) yield break;

        Vector3 forceDir = aimDirection != null ? aimDirection.forward : punchOrigin.forward;

        foreach (Transform child in root.transform) {
            var childRb = child.GetComponent<Rigidbody>();
            if (childRb != null && !childRb.isKinematic) {
                Vector3 explosionDir = (child.position - hitPoint).normalized;
                Vector3 finalForce = (forceDir + explosionDir * 0.5f + Vector3.up * 0.3f).normalized * punchForce;
                childRb.AddForce(finalForce, ForceMode.Impulse);
                childRb.AddTorque(Random.insideUnitSphere * torqueForce, ForceMode.Impulse);
            }
        }
    }

    /// <summary>タップから直接殴る（クールダウンだけ考慮）</summary>
    public void HandleHitFromTap(RaycastHit hit) {
        if (Time.unscaledTime - lastPunchTime < cooldown) return;
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsActive) return;
        lastPunchTime = Time.unscaledTime;

        Debug.Log($"[Punch/Tap] Hit: {hit.collider.name}");
        bool willBreak = hit.collider.GetComponentInParent<Fracture>() != null;
        PlayPunchPresentation(hit.point, willBreak);
        ApplyPunchEffect(hit.collider, hit.point, hit.normal);
    }

    void OnDrawGizmosSelected() {
        if (punchOrigin == null) return;
        Vector3 gizmoOrigin = aimDirection != null ? aimDirection.position : punchOrigin.position;
        Vector3 dir = aimDirection != null ? aimDirection.forward : punchOrigin.forward;

        float actualRange = playerActions != null ? playerActions.interactRange : range;

        Gizmos.color = Color.red;
        Vector3 end = gizmoOrigin + dir * actualRange;
        Gizmos.DrawLine(gizmoOrigin, end);
        Gizmos.DrawWireSphere(end, radius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(gizmoOrigin, 0.1f);

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.8f);
        Gizmos.DrawWireSphere(end, splashRadius);
    }
}
