// ÉQÅ[ÉÄì‡éûä‘ï\é¶
using TMPro;
using UnityEngine;

public class GameTimeUI : MonoBehaviour {
    [SerializeField] TextMeshProUGUI label;
    bool subscribed = false;
    
    void Start() {
        TrySubscribe();
    }

    void OnEnable() {
        TrySubscribe();
    }

    void OnDisable() {
        if (subscribed && GameTimeManager.Instance != null) {
            GameTimeManager.Instance.OnTimeChanged -= Refresh;
            subscribed = false;
        }
    }

    void TrySubscribe() {
        if (subscribed) return;
        if (GameTimeManager.Instance == null) return;

        GameTimeManager.Instance.OnTimeChanged += Refresh;
        subscribed = true;

        // èâä˙ï\é¶
        int mins = GameTimeManager.Instance.GetCurrentMinutes();
        Refresh(mins / 60, mins % 60);
    }

    void Refresh(int h, int m) {
        if (label != null) label.text = $"{h:D2}:{m:D2}";
    }
}
