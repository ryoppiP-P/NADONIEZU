using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(RectTransform))]
public class OnScreenStickBackgroundFollower : MonoBehaviour,
    IPointerDownHandler, IPointerUpHandler {

    public RectTransform background;
    [Tooltip("ノブ（Stick）のRect。離した時にBGをこの位置に合わせる")]
    public RectTransform knob;
    public bool hideWhenReleased = true;

    RectTransform selfRect;
    Canvas parentCanvas;

    bool isHolding;
    Vector3 fixedWorldPos;

    void Awake() {
        selfRect = GetComponent<RectTransform>();
        parentCanvas = GetComponentInParent<Canvas>();
        if (hideWhenReleased && background != null)
            background.gameObject.SetActive(false);
    }

    public void OnPointerDown(PointerEventData e) {
        if (background == null) return;
        background.gameObject.SetActive(true);
        StartCoroutine(PlaceNextFrame(e.position));
    }

    IEnumerator PlaceNextFrame(Vector2 screenPos) {
        yield return null;

        Camera cam = parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? parentCanvas.worldCamera : null;

        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
            selfRect, screenPos, cam, out Vector3 worldPos)) {
            fixedWorldPos = worldPos;
            background.position = fixedWorldPos;
            isHolding = true;
        }
    }

    public void OnPointerUp(PointerEventData e) {
        isHolding = false;

        // 離した瞬間、BGをノブと同じ位置に合わせる
        if (background != null && knob != null) {
            background.position = knob.position;
        }

        if (background == null) return;
        if (hideWhenReleased) background.gameObject.SetActive(false);
    }

    void LateUpdate() {
        if (!isHolding || background == null) return;
        if (background.position != fixedWorldPos) {
            background.position = fixedWorldPos;
        }
    }
}
