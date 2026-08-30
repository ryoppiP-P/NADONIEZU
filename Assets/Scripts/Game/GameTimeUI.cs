// ゲーム内時間表示。現在時刻に加えて、ゴール時刻と進捗バーも任意で出せるようにした。
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameTimeUI : MonoBehaviour {
    [Header("現在時刻")]
    [SerializeField] TextMeshProUGUI label;

    [Header("ゴール時刻（任意、未接続でも動く）")]
    [Tooltip("例: 本日は17:00まで のように表示するTextMeshPro。空なら何もしない")]
    [SerializeField] TextMeshProUGUI goalLabel;
    [SerializeField] string goalLabelFormat = "{0:D2}:{1:D2}まで";

    [Header("進捗バー（任意、未接続でも動く）")]
    [Tooltip("Image Type=FilledのImage。開始時刻～Goal時刻の進捗度でfillAmountを動かす")]
    [SerializeField] Image progressFill;

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

        RefreshGoalLabel();

        // 初期表示
        int mins = GameTimeManager.Instance.GetCurrentMinutes();
        Refresh(mins / 60, mins % 60);
    }

    void RefreshGoalLabel() {
        if (goalLabel == null) return;
        var mgr = GameTimeManager.Instance;
        int goalTotal = mgr.startMinutes + mgr.limitMinutes;
        int gh = (goalTotal / 60) % 24;
        int gm = goalTotal % 60;
        goalLabel.text = string.Format(goalLabelFormat, gh, gm);
    }

    void Refresh(int h, int m) {
        if (label != null) label.text = $"{h:D2}:{m:D2}";

        if (progressFill != null && GameTimeManager.Instance != null) {
            var mgr = GameTimeManager.Instance;
            float t = mgr.limitMinutes > 0
                ? Mathf.Clamp01((float)mgr.GetElapsedMinutes() / mgr.limitMinutes)
                : 0f;
            progressFill.fillAmount = t;
        }
    }
}
