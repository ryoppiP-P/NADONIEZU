// ゲーム内時間管理
using System;
using UnityEngine;

public class GameTimeManager : MonoBehaviour {
    public static GameTimeManager Instance { get; private set; }

    [Header("時間設定")]
    [Tooltip("開始時刻（分単位）例: 8*60 = 8:00")]
    public int startMinutes = 8 * 60;

    [Tooltip("強制終了までの経過時間（ゲーム内分）例: 8*60 = 8時間")]
    public int limitMinutes = 8 * 60;

    [Tooltip("リアル1分あたりに進むゲーム内分数")]
    public float gameMinutesPerRealMinute = 15f;    // 15分

    [Header("状態")]
    [SerializeField] float currentMinutes;
    public bool IsPaused { get; private set; }
    public bool IsTimeUp { get; private set; }

    [Header("フェーズ管理")]
    public GamePhase CurrentPhase { get; private set; } = GamePhase.Start;

    // イベント
    public event Action<int, int> OnTimeChanged; // (hour, minute)
    public event Action OnTimeUp;                // 8時間経過で発火

    void Awake() {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        currentMinutes = startMinutes;
    }

    void Start() => NotifyTime();

    void Update() {
        if (IsPaused || IsTimeUp) return;
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsActive) return;

        int prev = Mathf.FloorToInt(currentMinutes);
        currentMinutes += (gameMinutesPerRealMinute / 60f) * Time.deltaTime;

        int now = Mathf.FloorToInt(currentMinutes);
        if (now != prev) NotifyTime();

        CheckTimeUp();
    }

    void NotifyTime() {
        int total = Mathf.FloorToInt(currentMinutes);
        int h = (total / 60) % 24;
        int m = total % 60;
        OnTimeChanged?.Invoke(h, m);
    }

    void CheckTimeUp() {
        if (currentMinutes - startMinutes >= limitMinutes) {
            IsTimeUp = true;
            Debug.Log("[GameTime] Time's up!");
            OnTimeUp?.Invoke();
        }
    }

    // 会話などで時間を進める
    public void AdvanceMinutes(int minutes) {
        if (IsTimeUp) return;
        currentMinutes += minutes;
        NotifyTime();
        Debug.Log($"[GameTime] +{minutes}min → {GetTimeString()}");
        CheckTimeUp();
    }

    public string GetTimeString() {
        int total = Mathf.FloorToInt(currentMinutes);
        int h = (total / 60) % 24;
        int m = total % 60;
        return $"{h:D2}:{m:D2}";
    }

    // フェーズを進める
    public void AdvancePhase(GamePhase next) {
        if (CurrentPhase == next) return;
        CurrentPhase = next;
        Debug.Log($"[GameTime] Phase changed to {next}");
    }

    public int GetCurrentMinutes() => Mathf.FloorToInt(currentMinutes);

    /// <summary>小数を含む現在時刻（分）。アナログ時計の針を滞りなく動かす用</summary>
    public float GetCurrentMinutesRaw() => currentMinutes;
    public int GetElapsedMinutes() => Mathf.FloorToInt(currentMinutes) - startMinutes;

    public void Pause() => IsPaused = true;
    public void Resume() => IsPaused = false;
}
