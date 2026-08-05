using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerActions : MonoBehaviour {
    [Header("Interact")]
    public float interactRange = 2.5f;
    public float sphereRadius = 0.4f;
    public LayerMask interactMask = ~0;

    [Header("Carry")]
    public Transform holdPoint;          // 持ち上げ位置（キャラ前/カメラ前）
    public float throwForce = 10f;

    [Header("Reference")]
    public Transform cameraTransform;    // 投げる方向に使用
    public PlayerPunch playerPunch;

    private Rigidbody held;
    private int heldOriginalLayer;   // 持つ前のレイヤーを記憶
    private PlayerInputActions input;

    private const string HELD_LAYER = "HeldObject";

    InteractableHighlight currentHighlight;

    private bool inputEnabled = true;

    /// <summary>外部から操作の有効/無効を切り替え</summary>
    public void SetInputEnabled(bool enabled) { inputEnabled = enabled; }

    public bool IsInputEnabled => inputEnabled;

    void Awake() {
        input = new PlayerInputActions();
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
    }

    void OnEnable() {
        var p = input.Player;
        p.Enable();
        p.Interact.performed += OnInteract;
        p.Throw.performed += OnThrow;
        p.Punch.performed += OnPunch;
    }

    void OnDisable() {
        var p = input.Player;
        p.Interact.performed -= OnInteract;
        p.Throw.performed -= OnThrow;
        p.Punch.performed -= OnPunch;
        p.Disable();
    }

    void Update() {
        UpdateHighlight();
    }

    void FixedUpdate() {
        // 持ち上げ中はholdPointに追従させる
        if (held != null && holdPoint != null)
            held.MovePosition(holdPoint.position);
    }

    // ===== Interact =====
    // 優先順位:
    //   1. 持ち上げ中 → 何もしない（投げる/落とすはThrow側）
    //   2. 前方にPickable → 持ち上げ
    //   3. 前方にIInteractable → 対話/調べる
    void OnInteract(InputAction.CallbackContext _) {
        if (!inputEnabled) return;
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsActive) return;
        if (held != null) return;

        if (!ForwardCast(out var hit)) return;

        // 1. 持ち上げ判定
        var rb = hit.collider.attachedRigidbody;
        if (rb != null && rb.CompareTag("Pickable")) {
            PickUp(rb);
            return;
        }

        // 2. 対話/調べる
        var interactable = hit.collider.GetComponent<IInteractable>();
        interactable?.Interact();
    }

    // ===== Throw =====
    // 持ち上げ中なら投げる。何も持ってない時は無視。
    void OnThrow(InputAction.CallbackContext _) {
        if (!inputEnabled) return;
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsActive) return;
        if (held == null) return;
        Throw();
    }

    void OnPunch(InputAction.CallbackContext ctx) {
        if (!inputEnabled) return;
        if (!ctx.performed) return;
        Debug.Log("[Punch] Mouse Right Button pressed");

        if (playerPunch != null) {
            playerPunch.TryPunch();
        } else {
            Debug.LogWarning("[Punch] PlayerPunch reference is missing!");
        }
    }

    // ===== 内部処理 =====
    bool ForwardCast(out RaycastHit hit) {
        hit = default;
        if (cameraTransform == null) return false;

        Vector3 origin = cameraTransform.position;
        Vector3 dir = cameraTransform.forward;

        // 両方の判定で候補を集める
        var rayHits = Physics.RaycastAll(origin, dir, interactRange, interactMask, QueryTriggerInteraction.Ignore);
        var sphereHits = Physics.SphereCastAll(origin, sphereRadius, dir, interactRange, interactMask, QueryTriggerInteraction.Ignore);

        // 一番近いものを選ぶ
        float closestDist = float.MaxValue;
        bool found = false;

        foreach (var h in rayHits) {
            if (!IsValidHit(h)) continue;
            if (h.distance < closestDist) {
                closestDist = h.distance;
                hit = h;
                found = true;
            }
        }

        foreach (var h in sphereHits) {
            if (!IsValidHit(h)) continue;
            if (h.distance < closestDist) {
                closestDist = h.distance;
                hit = h;
                found = true;
            }
        }

        if (found) {
            Debug.DrawRay(origin, dir * closestDist, Color.green, 0.1f);
            return true;
        }

        Debug.DrawRay(origin, dir * interactRange, Color.red, 0.1f);
        return false;
    }

    /// <summary>
    /// ヒット結果が有効か判定（開始点接触や自分自身を除外）
    /// </summary>
    bool IsValidHit(RaycastHit h) {
        if (h.collider == null) return false;
        // 自分自身（Player階層）を除外
        if (h.collider.transform.root == this.transform.root) return false;
        // SphereCast開始点で既に接触してるとdistance=0が返るのを除外
        if (h.distance <= 0.001f) return false;
        return true;
    }

    void PickUp(Rigidbody rb) {
        held = rb;
        held.useGravity = false;
        held.isKinematic = true;

        // レイヤーを HeldObject に変更（Playerと衝突しなくなる）
        heldOriginalLayer = held.gameObject.layer;
        SetLayerRecursive(held.gameObject, LayerMask.NameToLayer(HELD_LAYER));

        IKDManager.Instance?.Add(21);
    }

    void Throw() {
        var rb = held;
        held = null;

        rb.isKinematic = false;
        rb.useGravity = true;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // 投げる方向
        Vector3 dir = cameraTransform != null ? cameraTransform.forward : transform.forward;

        rb.AddForce(dir * throwForce, ForceMode.Impulse);

        // レイヤーは少し遅らせて戻す（飛んでる最中はPlayerに衝突しない）
        StartCoroutine(RestoreLayerDelayed(rb.gameObject, heldOriginalLayer, 0.5f));
    }

    System.Collections.IEnumerator RestoreLayerDelayed(GameObject obj, int originalLayer, float delay) {
        yield return new WaitForSeconds(delay);
        if (obj != null)
            SetLayerRecursive(obj, originalLayer);
    }

    void SetLayerRecursive(GameObject obj, int layer) {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
            SetLayerRecursive(child.gameObject, layer);
    }

    public void HandleTapInteract(RaycastHit hit) {
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsActive) return;
        if (held != null) return;

        // 1. Pickable判定
        var rb = hit.collider.attachedRigidbody;
        if (rb != null && rb.CompareTag("Pickable")) {
            PickUp(rb);
            return;
        }

        // 2. IInteractable判定
        var interactable = hit.collider.GetComponent<IInteractable>();
        interactable?.Interact();
    }

    // 注視中のハイライト
    void UpdateHighlight() {
        // 会話中や持ち上げ中は消す
        if ((DialogueManager.Instance != null && DialogueManager.Instance.IsActive) || held != null) {
            ClearHighlight();
            return;
        }

        // 前方判定
        if (ForwardCast(out var hit)) {
            // Pickable も光らせたいなら以下も対象に含める
            var highlight = hit.collider.GetComponentInParent<InteractableHighlight>();

            // === 会話済みNPCはハイライト対象外 ===
            if (highlight != null) {
                var trigger = highlight.GetComponentInParent<DialogueTrigger>();
                if (trigger != null && trigger.HasTalkedInCurrentPhase) {
                    highlight = null;
                }
            }

            if (highlight != currentHighlight) {
                // 対象が変わった → 前のをOFF、新しいのをON
                if (currentHighlight != null) currentHighlight.SetHighlight(false);
                currentHighlight = highlight;
                if (currentHighlight != null) currentHighlight.SetHighlight(true);
            }
        } else {
            ClearHighlight();
        }
    }

    void ClearHighlight() {
        if (currentHighlight != null) {
            currentHighlight.SetHighlight(false);
            currentHighlight = null;
        }
    }

    // Gizmos
    void OnDrawGizmos() {
        if (cameraTransform == null) return;

        Vector3 origin = cameraTransform.position;
        Vector3 dir = cameraTransform.forward;

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(origin, origin + dir * interactRange);
        Gizmos.DrawWireSphere(origin + dir * interactRange, sphereRadius);
    }
}

public interface IInteractable {
    void Interact();
}
