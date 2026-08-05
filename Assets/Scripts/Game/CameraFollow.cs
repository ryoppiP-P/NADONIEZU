using UnityEngine;
using UnityEngine.InputSystem;

public class CameraFollow : MonoBehaviour {
    [Header("Target")]
    public Transform target;
    public Vector3 focusOffset = new Vector3(0f, 1.6f, 0f);

    [Header("Distance")]
    public float distance = 4f;
    public float minDistance = 0.4f;
    public float followSmooth = 12f;
    public float collisionSmooth = 25f;

    [Header("Collision")]
    public LayerMask collisionMask = ~0;
    public float collisionRadius = 0.3f;
    public float wallBuffer = 0.1f;

    [Tooltip("壁貫通の即時補正を有効にする（Lerpをスキップ）")]
    public bool instantCollisionSnap = true;

    [Header("Look Sensitivity")]
    public float mouseSensitivity = 0.15f;
    public float stickSensitivity = 180f;
    public float pitchMin = -30f;
    public float pitchMax = 60f;

    private float yaw, pitch = 15f;
    private float currentDistance;
    private PlayerInputActions input;
    private Vector2 lookInput;

    void Awake() {
        input = new PlayerInputActions();
        if (target != null) yaw = target.eulerAngles.y;
        currentDistance = distance;
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
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsActive) return;
        if (target == null) return;

        // === 視点入力 ===
        bool isStick = input.Player.Look.activeControl != null
                    && input.Player.Look.activeControl.device is Gamepad;

        float dx = lookInput.x * (isStick ? stickSensitivity * Time.deltaTime : mouseSensitivity);
        float dy = lookInput.y * (isStick ? stickSensitivity * Time.deltaTime : mouseSensitivity);

        yaw += dx;
        pitch -= dy;
        pitch = Mathf.Clamp(pitch, pitchMin, pitchMax);

        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 focus = target.position + focusOffset;
        Vector3 dir = rot * Vector3.back;

        // === 衝突判定（多段階） ===
        float targetDistance = GetSafeDistance(focus, dir);

        // === 距離補間 ===
        if (instantCollisionSnap && targetDistance < currentDistance) {
            // 壁が近づいた時は即座にスナップ（貫通防止）
            currentDistance = targetDistance;
        } else {
            // それ以外は滑らかに補間
            float smooth = (targetDistance < currentDistance) ? collisionSmooth : followSmooth;
            currentDistance = Mathf.Lerp(currentDistance, targetDistance, Time.deltaTime * smooth);
        }

        // === カメラ配置 ===
        Vector3 cameraPos = focus + dir * currentDistance;

        // 最終セーフティ: 配置後にfocus→カメラの直線上に壁があれば手前に戻す
        if (Physics.Linecast(focus, cameraPos, out RaycastHit finalHit, collisionMask, QueryTriggerInteraction.Ignore)) {
            cameraPos = finalHit.point - dir * wallBuffer;
        }

        transform.position = cameraPos;
        transform.rotation = rot;
    }

    /// <summary>
    /// 壁を考慮した安全な距離を計算
    /// </summary>
    float GetSafeDistance(Vector3 focus, Vector3 dir) {
        // 1. まず通常のSphereCast
        if (Physics.SphereCast(focus, collisionRadius, dir, out RaycastHit hit,
                               distance, collisionMask, QueryTriggerInteraction.Ignore)) {
            return Mathf.Max(hit.distance - wallBuffer, minDistance);
        }

        // 2. SphereCastで検出できなかった場合、focus地点がコライダー内部にある可能性
        //    OverlapSphereで確認して、内部にあればカメラを最小距離に貼り付ける
        Collider[] overlaps = Physics.OverlapSphere(focus, collisionRadius, collisionMask, QueryTriggerInteraction.Ignore);
        if (overlaps.Length > 0) {
            // focus地点が壁の内部にある → カメラを最小距離まで寄せる
            return minDistance;
        }

        // 3. 何もなければ通常距離
        return distance;
    }
}
