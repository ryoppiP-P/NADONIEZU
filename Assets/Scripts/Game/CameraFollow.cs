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

    [Header("Vertical Damping")]
    [Tooltip("縦方向の追従遅延。大きいほどジャンプで頭アップになりにくい")]
    public float verticalDamping = 0.25f;

    [Tooltip("縦追従の最大遅れ幅。これを超えたら強制追従（着地位置ズレ防止）")]
    public float verticalMaxLag = 1.5f;

    private float smoothedFocusY;
    private float focusYVelocity;
    private bool focusYInitialized;

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

    [Tooltip("設定画面のスライダー値(0-100)、50が基準(1.0倍)")]
    public float sensitivitySetting = 50f;

    float baseMouseSensitivity;
    float baseStickSensitivity;

    private float yaw, pitch = 15f;
    private float currentDistance;
    private PlayerInputActions input;
    private Vector2 lookInput;

    [Header("Cutscene Override")]
    [Tooltip("カットシーン中に強制注視する速さ")]
    public float forcedLookSmooth = 6f;

    private bool userControlEnabled = true;
    private Transform forcedLookTarget;

    /// <summary>カットシーン中などにユーザーの視点操作を止める/再開する</summary>
    public void SetUserControlEnabled(bool enabled) { userControlEnabled = enabled; }

    /// <summary>指定Transformを強制的に見るようにする。nullで解除</summary>
    public void SetForcedLookTarget(Transform t) { forcedLookTarget = t; }

    void Awake() {
        input = new PlayerInputActions();
        if (target != null) yaw = target.eulerAngles.y;
        currentDistance = distance;

        baseMouseSensitivity = mouseSensitivity;
        baseStickSensitivity = stickSensitivity;
        LoadSensitivityFromSave();

        AudioManager.Instance.PlayBGM(BGM.Game);
    }

    /// <summary>設定値(0-100)を適用する、50が基準(1.0倍)。設定画面から呼ばれる</summary>
    public void SetSensitivity(float setting0to100) {
        sensitivitySetting = Mathf.Clamp(setting0to100, 1f, 100f);
        float scale = sensitivitySetting / 50f;
        mouseSensitivity = baseMouseSensitivity * scale;
        stickSensitivity = baseStickSensitivity * scale;
    }

    void LoadSensitivityFromSave() {
        float setting = (SaveManager.Instance != null && SaveManager.Instance.Current != null)
            ? SaveManager.Instance.Current.settings.cameraSensitivity
            : sensitivitySetting;
        SetSensitivity(setting);
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

        Vector3 focus0 = target.position + focusOffset;

        if (userControlEnabled) {
            // === 視点入力（マウス/スティック） ===
            bool isStick = input.Player.Look.activeControl != null
                        && input.Player.Look.activeControl.device is Gamepad;

            float dx = lookInput.x * (isStick ? stickSensitivity * Time.deltaTime : mouseSensitivity);
            float dy = lookInput.y * (isStick ? stickSensitivity * Time.deltaTime : mouseSensitivity);

            yaw += dx;
            pitch -= dy;
            pitch = Mathf.Clamp(pitch, pitchMin, pitchMax);
        } else if (forcedLookTarget != null) {
            // === カットシーン：強制注視 ===
            Vector3 toTarget = (forcedLookTarget.position + Vector3.up * 1.5f) - focus0;
            if (toTarget.sqrMagnitude > 0.0001f) {
                Quaternion lookRot = Quaternion.LookRotation(toTarget.normalized);
                Vector3 e = lookRot.eulerAngles;
                float targetPitch = e.x > 180f ? e.x - 360f : e.x;
                float targetYaw = e.y;
                yaw = Mathf.LerpAngle(yaw, targetYaw, Time.deltaTime * forcedLookSmooth);
                pitch = Mathf.LerpAngle(pitch, targetPitch, Time.deltaTime * forcedLookSmooth);
            }
        }

        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        // ターゲット位置のY成分だけ遅延追従（ジャンプ時の頭アップ防止）
        float rawFocusY = target.position.y + focusOffset.y;

        if (!focusYInitialized) {
            smoothedFocusY = rawFocusY;
            focusYInitialized = true;
        }

        // 大きく離れすぎたら強制追従（高い段差を上った時などにカメラが取り残されないように）
        if (Mathf.Abs(rawFocusY - smoothedFocusY) > verticalMaxLag) {
            smoothedFocusY = Mathf.MoveTowards(smoothedFocusY, rawFocusY, verticalMaxLag);
        }

        smoothedFocusY = Mathf.SmoothDamp(smoothedFocusY, rawFocusY, ref focusYVelocity, verticalDamping);

        Vector3 focus = new Vector3(
            target.position.x + focusOffset.x,
            smoothedFocusY,
            target.position.z + focusOffset.z);
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
