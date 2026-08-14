// LookStick用。見た目の動き（追従して離すと中心へ戻る）はMoveStick/LookStickの
// Handleと同じ考え方を保ちつつ、カメラはUnityのOnScreenStick(traditionalな
// スティック傾き量×時間で回り続ける方式)ではなく、動かした分だけその場で反映する
// CameraFollow.AddLookDeltaを使う（Interactボタンと同じ操作感で統一）。
using UnityEngine;
using UnityEngine.EventSystems;

public class DragLookStick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler {
    [Header("参照")]
    public CameraFollow cameraFollow;

    [Header("カメラ感度")]
    [Tooltip("CameraFollow.AddLookDeltaに渡す前に掛ける倍率。1で素のマウスdeltaと同じスケール")]
    public float lookSensitivityMultiplier = 1f;

    [Header("見た目の動き")]
    [Tooltip("中心からどれだけ動けるか（anchoredPosition基準）")]
    public float movementRange = 50f;

    bool isDragging;
    RectTransform selfRect;
    RectTransform canvasRect;
    Camera uiCamera;
    Vector2 restAnchoredPos;
    Vector2 pointerDownLocalPos;

    void Awake() {
        selfRect = GetComponent<RectTransform>();
        restAnchoredPos = selfRect.anchoredPosition;

        var canvas = GetComponentInParent<Canvas>();
        if (canvas != null) {
            var root = canvas.rootCanvas;
            canvasRect = root.transform as RectTransform;
            uiCamera = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
        }
    }

    public void OnPointerDown(PointerEventData eventData) {
        isDragging = true;
        if (canvasRect != null)
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, eventData.position, uiCamera, out pointerDownLocalPos);
    }

    public void OnDrag(PointerEventData eventData) {
        if (!isDragging) return;

        if (cameraFollow != null)
            cameraFollow.AddLookDelta(eventData.delta * lookSensitivityMultiplier);

        if (canvasRect != null) {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, eventData.position, uiCamera, out var currentLocalPos);
            Vector2 offset = Vector2.ClampMagnitude(currentLocalPos - pointerDownLocalPos, movementRange);
            selfRect.anchoredPosition = restAnchoredPos + offset;
        }
    }

    public void OnPointerUp(PointerEventData eventData) {
        isDragging = false;
        selfRect.anchoredPosition = restAnchoredPos;
    }
}
