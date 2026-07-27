// ƒQ[ƒ€“àŠÔ•\¦
using TMPro;
using UnityEngine;

public class GameTimeUI : MonoBehaviour {
    [SerializeField] TextMeshProUGUI label;

    void OnEnable() {
        if (GameTimeManager.Instance != null) {
            GameTimeManager.Instance.OnTimeChanged += Refresh;
            // ‰Šú•\¦
            Refresh(GameTimeManager.Instance.GetCurrentMinutes() / 60,
                    GameTimeManager.Instance.GetCurrentMinutes() % 60);
        }
    }

    void OnDisable() {
        if (GameTimeManager.Instance != null)
            GameTimeManager.Instance.OnTimeChanged -= Refresh;
    }

    void Refresh(int h, int m) {
        if (label != null) label.text = $"{h:D2}:{m:D2}";
    }
}
