// ゲーム内時間をアナログ時計として表示する。
// 12時間で1周する短針・60分で1周する長針・60秒で1周する秒針に加えて、
// 退勤時刻(ゴール)の位置を赤いマークで文字盤上に示す。
using UnityEngine;

public class AnalogClockUI : MonoBehaviour {
    [Header("針")]
    [Tooltip("短針。上向き(12時方向)をデフォルト姿勢にしておくこと")]
    public RectTransform hourHand;
    public RectTransform minuteHand;
    public RectTransform secondHand;

    [Header("ゴール表示")]
    [Tooltip("退勤時刻を指す赤いマーク。短針と同じ回転で終業時刻の位置に固定する")]
    public RectTransform goalMarker;

    void LateUpdate() {
        var mgr = GameTimeManager.Instance;
        if (mgr == null) return;

        float totalMinutes = mgr.GetCurrentMinutesRaw();

        // 秒はゲーム内分の小数部から作る（実時間の秒ではなくゲーム内時間に同期させる）
        float seconds = (totalMinutes - Mathf.Floor(totalMinutes)) * 60f;
        float minutes = totalMinutes % 60f;
        float hours12 = (totalMinutes / 60f) % 12f;

        // UIは時計回りに回すのでZは負方向
        if (secondHand != null) secondHand.localRotation = Quaternion.Euler(0f, 0f, -seconds * 6f);      // 360/60
        if (minuteHand != null) minuteHand.localRotation = Quaternion.Euler(0f, 0f, -minutes * 6f);      // 360/60
        if (hourHand != null) hourHand.localRotation = Quaternion.Euler(0f, 0f, -hours12 * 30f);         // 360/12

        if (goalMarker != null) {
            float goalTotal = mgr.startMinutes + mgr.limitMinutes;
            float goalHours12 = (goalTotal / 60f) % 12f;
            goalMarker.localRotation = Quaternion.Euler(0f, 0f, -goalHours12 * 30f);
        }
    }
}
