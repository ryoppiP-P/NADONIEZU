// 17:00終了時のカットシーン。GameTimeManagerの時間切れ(OnTimeUp)で
// EndingControllerから呼び出され、Tanaka(上司)のEnd会話を挟んでからEDへ進む。
// 時間切れ時点でプレイヤーがマップのどこにいても違和感が出ないよう、
// 一度フェードアウトしてから、開始時（オープニングカットシーン）と同じ配置に
// プレイヤー・上司を戻し、フェードインしてから会話を始める。
using System.Collections;
using UnityEngine;

public class EndingCutsceneController : MonoBehaviour {
    [Header("参照")]
    [Tooltip("プレイヤーのPlayerController")]
    public PlayerController playerController;

    [Tooltip("プレイヤーのPlayerActions（インタラクト・持ち上げ用）")]
    public PlayerActions playerActions;

    [Tooltip("上司(Tanaka)のTransform")]
    public Transform bossNpc;

    [Tooltip("上司(Tanaka)の会話トリガー")]
    public DialogueTrigger bossTrigger;

    [Tooltip("カットシーン中にプレイヤーの視点操作を止めて上司を注視させるカメラ")]
    public CameraFollow cameraFollow;

    [Header("配置設定")]
    [Tooltip("上司の最終停止位置（OpeningCutsceneControllerのbossStopPointと同じものを指定し、開始時と同じ配置に戻す）")]
    public Transform bossStartPoint;

    [Header("タイミング設定")]
    public float initialWait = 0.3f;
    public float fadeOutDuration = 1f;
    public float fadeInDuration = 1f;
    public float preDialogueWait = 0.3f;

    bool playing = false;

    public static bool IsPlaying { get; private set; }

    /// <summary>EndingControllerから呼ばれる。時間切れ→ED直行の代わりにこちらを再生する。</summary>
    public void PlayEndingCutscene() {
        if (playing) return;
        playing = true;
        StartCoroutine(PlayCutscene());
    }

    IEnumerator PlayCutscene() {
        IsPlaying = true;
        // === 1. プレイヤー操作を停止 ===
        if (playerController != null) playerController.SetInputEnabled(false);
        if (playerActions != null) playerActions.SetInputEnabled(false);
        if (cameraFollow != null) cameraFollow.SetUserControlEnabled(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        yield return new WaitForSeconds(initialWait);

        // === 2. フェードアウト（暗転） ===
        if (FadeManager.Instance != null) {
            yield return FadeManager.Instance.FadeToBlack(fadeOutDuration);
        }

        // === 3. 暗転中に、開始時（オープニングカットシーン）と同じ配置へ戻す ===
        if (playerController != null) playerController.ResetToStartPosition();
        if (bossNpc != null && bossStartPoint != null) {
            bossNpc.position = bossStartPoint.position;
            bossNpc.rotation = bossStartPoint.rotation;
        }
        if (cameraFollow != null && bossNpc != null) cameraFollow.SetForcedLookTarget(bossNpc);

        // === 4. フェードイン ===
        if (FadeManager.Instance != null) {
            yield return FadeManager.Instance.FadeFromBlack(fadeInDuration);
        }

        yield return new WaitForSeconds(preDialogueWait);

        // === 5. フェーズをEndに進めてから会話開始（DialogueTriggerがendDataを選ぶように） ===
        if (GameTimeManager.Instance != null) {
            GameTimeManager.Instance.AdvancePhase(GamePhase.End);
        }

        if (bossTrigger != null) {
            bossTrigger.Interact();

            // 会話終了まで待機（終了時、Dialogue側のtriggersEndingでEDへ進む）
            yield return new WaitUntil(() =>
                DialogueManager.Instance != null && !DialogueManager.Instance.IsActive);
        }

        IsPlaying = false;

        // === 6. 後片付け（EDへのフェードは会話終了時に自動で始まる） ===
        if (playerController != null) playerController.SetInputEnabled(true);
        if (playerActions != null) playerActions.SetInputEnabled(true);
        if (cameraFollow != null) {
            cameraFollow.SetForcedLookTarget(null);
            cameraFollow.SetUserControlEnabled(true);
        }
    }
}
