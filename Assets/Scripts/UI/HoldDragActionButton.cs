// PUBGモバイル版のパンチボタンのような操作感のタッチボタン。
// 押している間は何も実行せず、その間のドラッグでボタン自体がMoveStick/LookStickのHandleと
// 同じように追従して動く。カメラの回転は動かした分だけその場で反映する方式
// （CameraFollow.AddLookDeltaにドラッグの生deltaを渡す。マウスと同じ感覚）。
// 指を離した瞬間にonReleaseに登録されたアクションを実行し、ボタンの見た目は元の位置へ戻る。
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;

public class HoldDragActionButton : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler {
    [Header("参照")]
    public CameraFollow cameraFollow;

    [Header("カメラ感度")]
    [Tooltip("CameraFollow.AddLookDeltaに渡す前に掛ける倍率。1で素のマウスdeltaと同じスケール")]
    public float lookSensitivityMultiplier = 1f;

    [Header("見た目の動き（MoveStickのHandleと同じ考え方）")]
    [Tooltip("ボタンの中心からどれだけ動けるか（anchoredPosition基準）")]
    public float movementRange = 50f;

    [Header("離した瞬間に呼ぶアクション")]
    public UnityEvent onRelease;

    bool isHolding;
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
        isHolding = true;
        if (canvasRect != null)
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, eventData.position, uiCamera, out pointerDownLocalPos);
    }

    public void OnDrag(PointerEventData eventData) {
        if (!isHolding) return;

        if (cameraFollow != null)
            cameraFollow.AddLookDelta(eventData.delta * lookSensitivityMultiplier);

        if (canvasRect != null) {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, eventData.position, uiCamera, out var currentLocalPos);
            Vector2 offset = Vector2.ClampMagnitude(currentLocalPos - pointerDownLocalPos, movementRange);
            selfRect.anchoredPosition = restAnchoredPos + offset;
        }
    }

    public void OnPointerUp(PointerEventData eventData) {
        if (!isHolding) return;
        isHolding = false;
        selfRect.anchoredPosition = restAnchoredPos;
        onRelease?.Invoke();
    }
}
