using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour {
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float gravity = -20f;
    public float rotationSpeed = 12f;

    [Header("Dash")]
    [Tooltip("スティック(タッチ/ゲームパッド)をこの値以上倒すとダッシュ扱いになる(0〜1)。キーボードはWASDの値が常に1になる仕様上対象外。")]
    [Range(0f, 1f)] public float dashStickThreshold = 0.85f;

    [Tooltip("ダッシュ中の移動速度倍率")]
    public float dashSpeedMultiplier = 1.6f;

    [Header("Jump")]
    public float jumpHeight = 1.2f;
    [Tooltip("地面判定に少しの猶予を持たせる。0にすると厳しくなる")]
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

        // v = sqrt(2gh) の物理計算でジャンプ初速から初速を求める
        velocity.y = Mathf.Sqrt(-2f * gravity * jumpHeight);
        lastGroundedTime = 0f; // 連続ジャンプ防止
    }

    // スティック入力がしきい値近くまで倒されているか。
    // キーボードのWASD合成入力は何かのキーを押した瞬間に常に大きさ(ほぼ)1になるため、
    // そのままだとキーボードで常にダッシュ扱いになってしまう。
    // OnScreenStick(タッチ)はcontrolPathで<Gamepad>/leftStickをシミュレートしているため、
    // アクティブデバイスがKeyboardでない場合のみアナログ量として判定する。
    bool IsDashInput() {
        var control = input.Player.Move.activeControl;
        if (control == null || control.device is UnityEngine.InputSystem.Keyboard) return false;
        return moveInput.magnitude >= dashStickThreshold;
    }

    void Awake() {
        cc = GetComponent<CharacterController>();
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        input = new PlayerInputActions();

        // ゲーム開始時の位置・向きを覚えておく（EDカットシーンなどでの復帰用）
        startPosition = transform.position;
        startRotation = transform.rotation;
    }

    private Vector3 startPosition;
    private Quaternion startRotation;

    /// <summary>CharacterControllerを一旦無効化してから安全にテレポートする。
    /// transform.positionを直接書き換えるだけだとCharacterControllerが内部状態と矛盾して問題が起きうるため。</summary>
    public void TeleportTo(Vector3 position, Quaternion rotation) {
        bool wasEnabled = cc.enabled;
        cc.enabled = false;
        transform.position = position;
        transform.rotation = rotation;
        velocity = Vector3.zero;
        cc.enabled = wasEnabled;
    }

    /// <summary>ゲーム開始時の位置・向きへ戻す（EDカットシーンなどから利用）</summary>
    public void ResetToStartPosition() => TeleportTo(startPosition, startRotation);

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
            // 重力だけ処理して地面にキープ
            if (cc.isGrounded && velocity.y < 0) velocity.y = -2f;
            velocity.y += gravity * Time.deltaTime;
            cc.Move(Vector3.up * velocity.y * Time.deltaTime);
            return;
        }

        // カメラからの移動方向（Y成分は無視）
        Vector3 camF = cameraTransform.forward; camF.y = 0; camF.Normalize();
        Vector3 camR = cameraTransform.right; camR.y = 0; camR.Normalize();
        Vector3 moveDir = camF * moveInput.y + camR * moveInput.x;
        if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();

        // 進行方向にキャラを向かせる
        if (moveDir.sqrMagnitude > 0.01f) {
            Quaternion target = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, rotationSpeed * Time.deltaTime);
        }

        // 接地している間、最後の接地時刻を記録（Coyote Time用）
        if (cc.isGrounded) lastGroundedTime = Time.time;

        // 重力
        if (cc.isGrounded && velocity.y < 0) velocity.y = -2f;
        velocity.y += gravity * Time.deltaTime;

        float dashMul = IsDashInput() ? dashSpeedMultiplier : 1f;
        cc.Move((moveDir * moveSpeed * speedMultiplier * dashMul + Vector3.up * velocity.y) * Time.deltaTime);
    }
}
