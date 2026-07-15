using UnityEngine;
using UnityEngine.InputSystem;

public class CameraFollow : MonoBehaviour {
    [Header("Target")]
    public Transform target;
    public Vector3 focusOffset = new Vector3(0f, 1.6f, 0f);

    [Header("Distance")]
    public float distance = 4f;
    public float followSmooth = 12f;

    [Header("Look Sensitivity")]
    public float mouseSensitivity = 0.15f;   // Mouse Delta用（フレーム差分）
    public float stickSensitivity = 180f;    // Gamepad用（度/秒）
    public float pitchMin = -30f;
    public float pitchMax = 60f;

    private float yaw, pitch = 15f;
    private PlayerInputActions input;
    private Vector2 lookInput;

    void Awake() {
        input = new PlayerInputActions();
        if (target != null) yaw = target.eulerAngles.y;
    }

    void OnEnable() {
        input.Player.Enable();
        input.Player.Look.performed += ctx => lookInput = ctx.ReadValue<Vector2>();
        input.Player.Look.canceled += ctx => lookInput = Vector2.zero;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void OnDisable() => input.Player.Disable();

    void LateUpdate() {
        // 会話中はカメラ操作を無効化
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsActive) return;

        if (target == null) return;

        // Mouse Deltaかスティックかで感度切り替え
        bool isStick = input.Player.Look.activeControl != null
                    && input.Player.Look.activeControl.device is Gamepad;

        float dx = lookInput.x * (isStick ? stickSensitivity * Time.deltaTime : mouseSensitivity);
        float dy = lookInput.y * (isStick ? stickSensitivity * Time.deltaTime : mouseSensitivity);

        yaw += dx;
        pitch -= dy;
        pitch = Mathf.Clamp(pitch, pitchMin, pitchMax);

        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 focus = target.position + focusOffset;
        Vector3 desired = focus - rot * Vector3.forward * distance;

        // 壁めり込み防止
        if (Physics.Linecast(focus, desired, out RaycastHit hit))
            desired = hit.point + hit.normal * 0.2f;

        transform.position = Vector3.Lerp(transform.position, desired, followSmooth * Time.deltaTime);
        transform.rotation = rot;
    }
}
