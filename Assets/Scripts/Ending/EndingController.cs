// Ending管理　ED発動判定とEDシーンへの遷移を担当
using System.Collections;
using UnityEngine;

public class EndingController : MonoBehaviour {
    public static EndingController Instance { get; private set; }

    [Header("設定")]
    [Tooltip("ED発動から遷移までの待ち時間(秒)")]
    public float delayBeforeEnding = 60f;

    [Tooltip("EDシーン名")]
    public string endingSceneName = "EndingScene";

    [Tooltip("フェードアウト時間")]
    public float fadeDuration = 1.5f;

    bool triggered = false;

    void Awake() {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnEnable() {
        if (GameTimeManager.Instance != null)
            GameTimeManager.Instance.OnTimeUp += OnTimeUp;
    }

    void OnDisable() {
        if (GameTimeManager.Instance != null)
            GameTimeManager.Instance.OnTimeUp -= OnTimeUp;
    }

    /// <summary>会話終了時に呼ばれる</summary>
    public void CheckTrigger() {
        if (triggered) return;
        if (NPCManager.Instance != null && NPCManager.Instance.IsAllTalked) {
            Debug.Log("[Ending] All NPCs talked → ending triggered");
            StartEnding();
        }
    }

    /// <summary>時間切れで呼ばれる</summary>
    void OnTimeUp() {
        if (triggered) return;
        Debug.Log("[Ending] Time's up → ending triggered");
        StartEnding();
    }

    void StartEnding() {
        triggered = true;
        StartCoroutine(WaitAndLoad());
    }

    IEnumerator WaitAndLoad() {
        Debug.Log($"[Ending] Waiting {delayBeforeEnding}s before ED...");
        yield return new WaitForSeconds(delayBeforeEnding);

        // 判定結果を保存
        var result = RouteTracker.Instance?.Judge();
        EndingResultHolder.Result = result;

        Debug.Log($"[Ending] Loading scene: {endingSceneName}");

        FadeManager.FadeOut(endingSceneName, fadeDuration);
    }
}
