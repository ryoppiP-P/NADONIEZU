// タッチスクリーンが存在しない環境（PC等）では、スマホ向けのオンスクリーンボタン群を非表示にする。
// TouchInteractor.cs等と同じ判定基準（Touchscreen.current）を使う。
// Application.isMobilePlatformはWebGLビルドだと常にfalseになりPC/スマホの判別に使えないため採用しない。
using UnityEngine;
using UnityEngine.InputSystem;

public class TouchControlsPlatformGate : MonoBehaviour {
    [Tooltip("タッチスクリーンが無い時に非表示にする対象（MoveStick/LookStick/InteractBG/Jump/Esc等）")]
    public GameObject[] targets;

    void Start() {
        if (Touchscreen.current != null) return; // タッチデバイスがあれば何もしない

        if (targets == null) return;
        foreach (var t in targets) {
            if (t != null) t.SetActive(false);
        }
    }
}
