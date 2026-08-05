using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class TouchInteractor : MonoBehaviour {
    [Header("Reference")]
    public Camera cam;
    public PlayerActions playerActions;

    [Header("Tap Detection")]
    [Tooltip("これ以下の移動量ならタップとみなす（ピクセル）")]
    public float tapMoveThreshold = 20f;
    [Tooltip("これ以下の時間ならタップとみなす（秒）")]
    public float tapTimeThreshold = 0.3f;

    [Header("Cast")]
    public float rayRange = 8f;
    public LayerMask interactMask = ~0;

    Vector2 touchStartPos;
    float touchStartTime;
    int trackingFingerId = -1;

    void Awake() {
        if (cam == null) cam = Camera.main;
    }

    void Update() {
        if (Touchscreen.current == null) return;
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsActive) return;

        foreach (var touch in Touchscreen.current.touches) {
            var phase = touch.phase.ReadValue();
            int id = touch.touchId.ReadValue();
            Vector2 pos = touch.position.ReadValue();

            if (phase == UnityEngine.InputSystem.TouchPhase.Began) {
                // UI（Stick、ボタン等）を触ってる場合は無視
                if (IsOverUI(pos)) continue;

                if (trackingFingerId == -1) {
                    trackingFingerId = id;
                    touchStartPos = pos;
                    touchStartTime = Time.time;
                }
            } else if (phase == UnityEngine.InputSystem.TouchPhase.Ended && id == trackingFingerId) {
                float dist = Vector2.Distance(pos, touchStartPos);
                float dur = Time.time - touchStartTime;

                if (dist < tapMoveThreshold && dur < tapTimeThreshold) {
                    TryTapInteract(pos);
                }

                trackingFingerId = -1;
            } else if (phase == UnityEngine.InputSystem.TouchPhase.Canceled && id == trackingFingerId) {
                trackingFingerId = -1;
            }
        }
    }

    bool IsOverUI(Vector2 screenPos) {
        if (EventSystem.current == null) return false;
        var eventData = new PointerEventData(EventSystem.current) { position = screenPos };
        var results = new System.Collections.Generic.List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        return results.Count > 0;
    }

    void TryTapInteract(Vector2 screenPos) {
        if (cam == null || playerActions == null) return;

        Ray ray = cam.ScreenPointToRay(screenPos);
        if (!Physics.Raycast(ray, out RaycastHit hit, rayRange, interactMask,
                             QueryTriggerInteraction.Ignore)) {
            Debug.Log("[TouchInteract] Miss");
            return;
        }

        Debug.Log($"[TouchInteract] Hit: {hit.collider.name}");
        Debug.DrawRay(ray.origin, ray.direction * rayRange, Color.yellow, 1f);

        playerActions.HandleTapInteract(hit);
    }
}
