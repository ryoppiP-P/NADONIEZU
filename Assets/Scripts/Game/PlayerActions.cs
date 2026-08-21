using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerActions : MonoBehaviour {
    [Header("Interact")]
    public float interactRange = 2.5f;
    public float sphereRadius = 0.4f;
    public LayerMask interactMask = ~0;

    [Header("Carry")]
    public Transform holdPoint;          // 持ち上げ位置（キャラ前/カメラ前）

    [Header("Throw Charge")]
    public float maxChargeTime = 1.5f;
    public float minThrowForce = 5f;
    public float maxThrowForce = 20f;
    [Range(0f, 1f)] public float tapThreshold = 0.1f;
    public bool useUpwardBias = true;
    [Range(0f, 1f)] public float upwardBias = 0.15f;

    [Header("Charge FX")]
    public bool showTrajectory = true;
    public LineRenderer trajectoryLine;
    public int trajectoryPointCount = 30;
    public float trajectoryTimeStep = 0.08f;
    public LayerMask trajectoryCollisionMask = ~0;
    public bool slowMoveWhileCharging = true;
    [Range(0f, 1f)] public float chargeMoveSpeedMultiplier = 0.5f;

    [Header("Reference")]
    public Transform cameraTransform;    // 投げる方向に使用
    public PlayerController playerController;   // チャージ中の移動速度低下用
    public PlayerPunch playerPunch;
    public InteractPromptUI promptUI;   // 注視対象に応じたインタラクトUI

    private Rigidbody held;
    private int heldOriginalLayer;   // 持つ前のレイヤーを記憶

    // === チャージ状態 ===
    private bool isCharging = false;
    private float chargeStartTime;
    private float chargeAmount;
    public bool IsCharging => isCharging;
    public float ChargeAmount => chargeAmount;
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
        if (playerController == null)
            playerController = GetComponent<PlayerController>();
    }

    void OnEnable() {
        var p = input.Player;
        p.Enable();
        p.Interact.canceled += OnInteract;
        p.Throw.started += OnThrowStarted;
        p.Throw.canceled += OnThrowCanceled;
        p.Punch.performed += OnPunch;
        p.Jump.performed += OnJump;
    }

    void OnDisable() {
        var p = input.Player;
        p.Interact.canceled -= OnInteract;
        p.Throw.started -= OnThrowStarted;
        p.Throw.canceled -= OnThrowCanceled;
        p.Punch.performed -= OnPunch;
        p.Jump.performed -= OnJump;
        p.Disable();
        CancelCharging();
    }

    void Update() {
        UpdateHighlight();
        UpdateCharge();
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
    // ボタン/タッチを離した瞬間(canceled)に発火する。
    // 押している間は何も起きず、その間は常にUpdateHighlight()のハイライト/プロンプトUIで
    // 「今狙っている対象」が見えているので、スティックを倒して離す感覚になる。
    void OnInteract(InputAction.CallbackContext _) => TryInteract();

    /// <summary>キーボード/ゲームパッドの入力アクションからも、
    /// タッチのHoldDragActionButtonの釈放時イベントからも直接呼べるように公開化。</summary>
    public void TryInteract() {
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

    // ===== Throw (チャージ式) =====
    // 持ち上げている状態でThrowを押しこむとチャージ開始。離した瞬間に投げる。
    void OnThrowStarted(InputAction.CallbackContext _) {
        if (!inputEnabled) return;
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsActive) return;
        if (held == null) return;
        StartCharging();
    }

    void OnThrowCanceled(InputAction.CallbackContext _) {
        if (!isCharging) return;
        FinishChargeAndThrow();
    }

    void StartCharging() {
        isCharging = true;
        chargeStartTime = Time.time;
        chargeAmount = 0f;

        if (showTrajectory && trajectoryLine != null) {
            trajectoryLine.enabled = true;
            UpdateTrajectoryPreview();
        }

        if (slowMoveWhileCharging && playerController != null)
            playerController.SetSpeedMultiplier(chargeMoveSpeedMultiplier);
    }

    // 持ち物を失ったり会話開始などで強制中断する場合
    void CancelCharging() {
        if (!isCharging) return;
        isCharging = false;
        chargeAmount = 0f;
        HideTrajectory();
        RestoreMoveSpeed();
    }

    void FinishChargeAndThrow() {
        isCharging = false;
        HideTrajectory();
        RestoreMoveSpeed();

        if (held == null) { chargeAmount = 0f; return; }

        float ratio = chargeAmount < tapThreshold ? 0f : chargeAmount;
        float force = Mathf.Lerp(minThrowForce, maxThrowForce, ratio);
        Throw(force);
        chargeAmount = 0f;
    }

    void HideTrajectory() {
        if (trajectoryLine != null) trajectoryLine.enabled = false;
    }

    void RestoreMoveSpeed() {
        if (playerController != null) playerController.SetSpeedMultiplier(1f);
    }

    void UpdateCharge() {
        if (!isCharging) return;

        // 持ち物を失った
        if (held == null) { CancelCharging(); return; }
        // 会話が始まった
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsActive) { CancelCharging(); return; }
        // 外部から操作無効化された
        if (!inputEnabled) { CancelCharging(); return; }

        float t = (Time.time - chargeStartTime) / Mathf.Max(0.0001f, maxChargeTime);
        chargeAmount = Mathf.Clamp01(t);

        if (showTrajectory && trajectoryLine != null)
            UpdateTrajectoryPreview();
    }

    // 現在のチャージ量で実際に投げた場合の放物線を予測してLineRendererに反映
    void UpdateTrajectoryPreview() {
        if (trajectoryLine == null || held == null || cameraTransform == null) return;

        float ratio = chargeAmount < tapThreshold ? 0f : chargeAmount;
        float force = Mathf.Lerp(minThrowForce, maxThrowForce, ratio);
        float mass = held.mass > 0f ? held.mass : 1f;

        Vector3 dir = cameraTransform.forward;
        if (useUpwardBias) dir = (dir + Vector3.up * upwardBias).normalized;
        Vector3 velocity = dir * (force / mass);

        Vector3 origin = holdPoint != null ? holdPoint.position : cameraTransform.position;
        Vector3 gravity = Physics.gravity;

        var points = new System.Collections.Generic.List<Vector3>(trajectoryPointCount);
        Vector3 prevPos = origin;
        points.Add(prevPos);

        for (int i = 1; i < trajectoryPointCount; i++) {
            float t = i * trajectoryTimeStep;
            Vector3 pos = origin + velocity * t + 0.5f * gravity * t * t;

            // 持ち上げているオブジェクト自身との自己転列を避けるため、最初の数ポイントは衝突判定しない
            if (i >= 3 && Physics.Linecast(prevPos, pos, out RaycastHit hit, trajectoryCollisionMask, QueryTriggerInteraction.Ignore)) {
                points.Add(hit.point);
                break;
            }

            points.Add(pos);
            prevPos = pos;
        }

        trajectoryLine.useWorldSpace = true;
        trajectoryLine.positionCount = points.Count;
        trajectoryLine.SetPositions(points.ToArray());
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

    // ===== Jump =====
    void OnJump(InputAction.CallbackContext ctx) {
        if (!inputEnabled) return;
        if (!ctx.performed) return;
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsActive) return;
        // 持ち上げ中や投げチャージ中もジャンプ可にするなら以下は不要
        // if (held != null || isCharging) return;

        if (playerController != null) {
            playerController.TryJump();
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

    void Throw(float force) {
        var rb = held;
        held = null;

        rb.isKinematic = false;
        rb.useGravity = true;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // 投げたオブジェクトに衝撃伝播用コンポーネントを付与
        // （Fractureに当たった時に投げた勢いを破片へ伝える）
        var impactor = rb.GetComponent<ThrownImpactor>();
        if (impactor == null) impactor = rb.gameObject.AddComponent<ThrownImpactor>();
        impactor.BeginThrow();

        // 投げる方向（カメラ前方、オプションで山なりに飛ぶ上向き成分を加える）
        Vector3 dir = cameraTransform != null ? cameraTransform.forward : transform.forward;
        if (useUpwardBias) dir = (dir + Vector3.up * upwardBias).normalized;

        rb.AddForce(dir * force, ForceMode.Impulse);

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
        if (!inputEnabled) return;
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsActive) return;
        if (held != null) return;

        // 1. Pickable判定（持ち上げ）
        var rb = hit.collider.attachedRigidbody;
        if (rb != null && rb.CompareTag("Pickable")) {
            PickUp(rb);
            return;
        }

        // 2. IInteractable判定（会話・調べる）
        var interactable = hit.collider.GetComponent<IInteractable>();
        if (interactable != null) {
            interactable.Interact();
            return;
        }

        // 3. Fracture判定（壊せる）
        var fracture = hit.collider.GetComponentInParent<Fracture>();
        if (fracture != null) {
            if (playerPunch != null) playerPunch.HandleHitFromTap(hit);
            return;
        }

        // 4. Deformable判定（へこむ）
        var deformable = hit.collider.GetComponentInParent<Deformable>();
        if (deformable != null) {
            if (playerPunch != null) playerPunch.HandleHitFromTap(hit);
            return;
        }
    }

    // 注視中のハイライト
    void UpdateHighlight() {
        // 会話中や持ち上げ中は消す
        if ((DialogueManager.Instance != null && DialogueManager.Instance.IsActive) || held != null) {
            ClearHighlight();
            promptUI?.Clear();
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

            // インタラクトUIにも同じ判定で通知（会話済みNPC等の除外は・SetTarget側でも判定）
            promptUI?.SetTarget(hit.collider);
        } else {
            ClearHighlight();
            promptUI?.Clear();
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
