using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour {
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float gravity = -20f;
    public float rotationSpeed = 12f;

    [Header("Jump")]
    public float jumpHeight = 1.2f;
    [Tooltip("地面判定に少しの猶予を持たせる。0にすると厳密判定")]
    public float coyoteTime = 0.1f;

    private float lastGroundedTime;

    [Header("Reference")]
    public Transform cameraTransform;

    private CharacterController cc;
    private Vector3 velocity;

    private PlayerInputActions input;
    private Vector2 moveInput;

    // === 追加: 外部からの操作制御 ===
    private bool inputEnabled = true;
    public bool IsInputEnabled => inputEnabled;

    // チャージ中など、一時的に移動速度を倍率で落とすためのフック
    private float speedMultiplier = 1f;
    public void SetSpeedMultiplier(float multiplier) { speedMultiplier = multiplier; }

    public void SetInputEnabled(bool enabled) {
        inputEnabled = enabled;
        if (!enabled) moveInput = Vector2.zero;
    }

    /// <summary>外部から呼ぶジャンプ実行。地面にいる場合のみ有効。</summary>
    public void TryJump() {
        if (!inputEnabled) return;
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsActive) return;

        // Coyote Time：接地から少しの間はジャンプ可能（プラットフォーマー的な操作感）
        bool canJump = cc.isGrounded || (Time.time - lastGroundedTime <= coyoteTime);
        if (!canJump) return;

        // v = sqrt(2gh) の物理計算でジャンプ高さから初速を求める
        velocity.y = Mathf.Sqrt(-2f * gravity * jumpHeight);
        lastGroundedTime = 0f; // 連続ジャンプ防止
    }

    void Awake() {
        cc = GetComponent<CharacterController>();
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        input = new PlayerInputActions();
    }

    void OnEnable() {
        input.Player.Enable();
        input.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        input.Player.Move.canceled += ctx => moveInput = Vector2.zero;
    }

    void OnDisable() => input.Player.Disable();

    void Update() {
        // 会話中はプレイヤー操作を無効化
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsActive) return;

        // 接地時刻の更新（inputEnabledに関わらず記録する）
        if (cc.isGrounded) lastGroundedTime = Time.time;

        // === 追加: 外部から無効化されている間は移動しない ===
        if (!inputEnabled) {
            // 重力だけ効かせて地面にキープ
            if (cc.isGrounded && velocity.y < 0) velocity.y = -2f;
            velocity.y += gravity * Time.deltaTime;
            cc.Move(Vector3.up * velocity.y * Time.deltaTime);
            return;
        }

        // カメラ基準の移動方向（Y成分は無視）
        Vector3 camF = cameraTransform.forward; camF.y = 0; camF.Normalize();
        Vector3 camR = cameraTransform.right; camR.y = 0; camR.Normalize();
        Vector3 moveDir = camF * moveInput.y + camR * moveInput.x;
        if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();

        // 進行方向にキャラを向ける
        if (moveDir.sqrMagnitude > 0.01f) {
            Quaternion target = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, rotationSpeed * Time.deltaTime);
        }

        // 接地している間、最後の接地時刻を記録（Coyote Time用）
        if (cc.isGrounded) lastGroundedTime = Time.time;

        // 重力
        if (cc.isGrounded && velocity.y < 0) velocity.y = -2f;
        velocity.y += gravity * Time.deltaTime;

        cc.Move((moveDir * moveSpeed * speedMultiplier + Vector3.up * velocity.y) * Time.deltaTime);
    }
}
