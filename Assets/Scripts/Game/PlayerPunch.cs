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

    [Header("威力")]
    [Tooltip("水平方向の吹き飛ばし力")]
    public float punchForce = 40f;

    [Tooltip("上方向の追加力（浮き上がり）")]
    public float upwardForce = 8f;

    [Tooltip("ランダム回転の強さ")]
    public float torqueForce = 20f;

    [Header("クールダウン")]
    public float cooldown = 0.5f;

    [Header("演出")]
    [Tooltip("ヒットストップ時間(秒)")]
    public float hitStopDuration = 0.08f;

    [Tooltip("ヒットストップ時のタイムスケール")]
    [Range(0f, 1f)] public float hitStopTimeScale = 0.05f;

    [Tooltip("カメラシェイクの強さ")]
    public float cameraShakeMagnitude = 0.15f;

    [Tooltip("カメラシェイクの時間")]
    public float cameraShakeDuration = 0.15f;

    float lastPunchTime = -999f;
    Transform cameraTransform;
    Vector3 cameraOriginalLocalPos;

    void Awake() {
        if (punchOrigin == null) punchOrigin = transform;
        if (aimDirection == null && Camera.main != null)
            aimDirection = Camera.main.transform;

        cameraTransform = aimDirection;
        if (cameraTransform != null)
            cameraOriginalLocalPos = cameraTransform.localPosition;
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
        Vector3 origin = punchOrigin.position;
        Vector3 dir = aimDirection != null ? aimDirection.forward : punchOrigin.forward;

        float actualRange = playerActions != null ? playerActions.interactRange : range;

        // 両方の判定で候補を集める
        var rayHits = Physics.RaycastAll(origin, dir, actualRange, hitMask, QueryTriggerInteraction.Ignore);
        var sphereHits = Physics.SphereCastAll(origin, radius, dir, actualRange, hitMask, QueryTriggerInteraction.Ignore);

        // 一番近いものを選ぶ
        RaycastHit closest = default;
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

        if (found) {
            Debug.Log($"[Punch] Hit: {closest.collider.name} at distance {closestDist:F2}");
            HandleHit(closest);
        } else {
            Debug.Log($"[Punch] Missed (range={actualRange})");
        }
    }

    bool IsValidHit(RaycastHit h) {
        if (h.collider == null) return false;
        if (h.collider.transform.root == transform.root) return false;
        if (h.distance <= 0.001f) return false;
        return true;
    }


    void HandleHit(RaycastHit hit) {
        // カメラシェイク
        StartCoroutine(CameraShake());

        // ヒットストップ
        StartCoroutine(HitStop());

        // 1. Fracture コンポーネントがあれば破壊
        var fracture = hit.collider.GetComponentInParent<Fracture>();
        if (fracture != null) {
            BreakFractureObject(fracture, hit);
            return;
        }

        // 2. Rigidbodyがあれば強力に吹き飛ばす
        var rb = hit.collider.attachedRigidbody;
        if (rb != null && !rb.isKinematic) {
            Vector3 forceDir = aimDirection != null ? aimDirection.forward : punchOrigin.forward;

            // 前方への強い力 + 上方向の追加力
            Vector3 finalForce = forceDir * punchForce + Vector3.up * upwardForce;
            rb.AddForceAtPosition(finalForce, hit.point, ForceMode.Impulse);

            // ランダムな回転を加える
            Vector3 randomTorque = new Vector3(
                Random.Range(-1f, 1f),
                Random.Range(-1f, 1f),
                Random.Range(-1f, 1f)
            ) * torqueForce;
            rb.AddTorque(randomTorque, ForceMode.Impulse);
        }
    }

    void BreakFractureObject(Fracture fracture, RaycastHit hit) {
        var rb = fracture.GetComponent<Rigidbody>();
        if (rb != null && rb.isKinematic) {
            rb.isKinematic = false;
        }

        fracture.ComputeFracture();

        // FragmentDecayは Fracture側 が自動アタッチしてくれるので、爆発力だけ加える
        StartCoroutine(ApplyExplosionToFragments(fracture, hit));
    }

    System.Collections.IEnumerator ApplyExplosionToFragments(Fracture fracture, RaycastHit originalHit) {
        yield return null;
        yield return new WaitForSeconds(0.1f);

        string fragmentRootName = $"{fracture.gameObject.name}Fragments";
        var root = GameObject.Find(fragmentRootName);
        if (root == null) yield break;

        Vector3 forceDir = aimDirection != null ? aimDirection.forward : punchOrigin.forward;
        Vector3 hitPoint = originalHit.point;

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

    // === ヒットストップ ===
    System.Collections.IEnumerator HitStop() {
        Time.timeScale = hitStopTimeScale;
        yield return new WaitForSecondsRealtime(hitStopDuration);
        Time.timeScale = 1f;
    }

    // === カメラシェイク ===
    System.Collections.IEnumerator CameraShake() {
        if (cameraTransform == null) yield break;

        float elapsed = 0f;
        while (elapsed < cameraShakeDuration) {
            float strength = cameraShakeMagnitude * (1f - elapsed / cameraShakeDuration);
            Vector3 offset = Random.insideUnitSphere * strength;
            offset.z = 0f;
            cameraTransform.localPosition = cameraOriginalLocalPos + offset;

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        cameraTransform.localPosition = cameraOriginalLocalPos;
    }

    void OnDrawGizmosSelected() {
        if (punchOrigin == null) return;
        Vector3 dir = aimDirection != null ? aimDirection.forward : punchOrigin.forward;

        float actualRange = playerActions != null ? playerActions.interactRange : range;

        Gizmos.color = Color.red;
        Vector3 end = punchOrigin.position + dir * actualRange;
        Gizmos.DrawLine(punchOrigin.position, end);
        Gizmos.DrawWireSphere(end, radius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(punchOrigin.position, 0.1f);
    }
}
