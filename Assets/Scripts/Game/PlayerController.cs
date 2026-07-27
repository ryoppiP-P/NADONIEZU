using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour {
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float gravity = -20f;
    public float rotationSpeed = 12f;

    [Header("Reference")]
    public Transform cameraTransform;

    private CharacterController cc;
    private Vector3 velocity;

    private PlayerInputActions input;
    private Vector2 moveInput;

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

        // 重力
        if (cc.isGrounded && velocity.y < 0) velocity.y = -2f;
        velocity.y += gravity * Time.deltaTime;

        cc.Move((moveDir * moveSpeed + Vector3.up * velocity.y) * Time.deltaTime);
    }
}
