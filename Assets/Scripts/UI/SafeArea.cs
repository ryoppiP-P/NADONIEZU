// Screen.safeAreaに追従してRectTransformを内側に収める（ノッチ/カメラカットアウト対策）。
// 画面全体を覆うダイアログ背景等はこのコンポーネントの対象外にし、
// タッチ操作UI・HUD・ダイアログ等「実際に見える/触れる必要がある」要素だけをこの子にする。
using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class SafeArea : MonoBehaviour {
    RectTransform rt;
    Rect lastSafeArea;
    Vector2Int lastScreenSize;

    void Awake() {
        rt = GetComponent<RectTransform>();
        Apply();
    }

    void Update() {
        if (Screen.safeArea != lastSafeArea || Screen.width != lastScreenSize.x || Screen.height != lastScreenSize.y) {
            Apply();
        }
    }

    void Apply() {
        lastSafeArea = Screen.safeArea;
        lastScreenSize = new Vector2Int(Screen.width, Screen.height);

        if (Screen.width <= 0 || Screen.height <= 0) return;

        Vector2 anchorMin = lastSafeArea.position;
        Vector2 anchorMax = lastSafeArea.position + lastSafeArea.size;
        anchorMin.x /= Screen.width;
        anchorMin.y /= Screen.height;
        anchorMax.x /= Screen.width;
        anchorMax.y /= Screen.height;

        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
    }
}
