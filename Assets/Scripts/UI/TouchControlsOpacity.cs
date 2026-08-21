// タッチ操作UI（MoveStick/InteractBG/Jump）の不透明度を設定値から反映する。
// GameSceneのCanvas/Controlに付ける想定。SettingsPanelUIから即時プレビュー用に呼ばれる。
using UnityEngine;

public class TouchControlsOpacity : MonoBehaviour {
    public CanvasGroup[] targets;

    void Start() {
        float opacity = 100f;
        if (SaveManager.Instance != null && SaveManager.Instance.Current != null)
            opacity = SaveManager.Instance.Current.settings.controlOpacity;

        Apply(opacity);
    }

    public void Apply(float opacity0to100) {
        float alpha = Mathf.Clamp01(opacity0to100 / 100f);
        if (targets == null) return;
        foreach (var group in targets) {
            if (group != null) group.alpha = alpha;
        }
    }
}
