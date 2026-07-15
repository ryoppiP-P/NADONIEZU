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

    private Rigidbody held;
    private int heldOriginalLayer;   // 持つ前のレイヤーを記憶
    private PlayerInputActions input;

    private const string HELD_LAYER = "HeldObject";

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
    }

    void OnDisable() {
        var p = input.Player;
        p.Interact.performed -= OnInteract;
        p.Throw.performed -= OnThrow;
        p.Disable();
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
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsActive) return;
        if (held == null) return;
        Throw();
    }

    // ===== 内部処理 =====
    bool ForwardCast(out RaycastHit hit) {
        if (cameraTransform == null) {
            hit = default;
            return false;
        }

        Vector3 origin = cameraTransform.position;
        Vector3 dir = cameraTransform.forward;

        bool result = Physics.SphereCast(origin, sphereRadius, dir,
                                         out hit, interactRange, interactMask);

        Debug.DrawRay(origin, dir * interactRange, result ? Color.green : Color.red, 1f);
        return result;
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
