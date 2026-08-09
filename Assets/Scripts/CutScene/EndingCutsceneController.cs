// 17:00終了時のカットシーン。GameTimeManagerの時間切れ(OnTimeUp)で
// EndingControllerから呼び出され、Tanaka(上司)のEnd会話を挟んでからEDへ進む。
// OpeningCutsceneControllerの構成を踏襲。
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
    [Tooltip("プレイヤー正面からのオフセット距離(m)")]
    public float standDistance = 1f;

    [Header("タイミング設定")]
    public float initialWait = 0.3f;
    public float bossWalkSpeed = 1.5f;
    public float bossStopThreshold = 0.05f;
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
        // === 1. プレイヤー操作を停止し、カメラを上司に固定 ===
        if (playerController != null) playerController.SetInputEnabled(false);
        if (playerActions != null) playerActions.SetInputEnabled(false);
        if (cameraFollow != null && bossNpc != null) {
            cameraFollow.SetUserControlEnabled(false);
            cameraFollow.SetForcedLookTarget(bossNpc);
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        yield return new WaitForSeconds(initialWait);

        // === 2. 上司をプレイヤーの正面standDistance(m)へ歩かせる ===
        if (bossNpc != null && playerController != null) {
            yield return WalkBossInFrontOfPlayer();
        }

        yield return new WaitForSeconds(preDialogueWait);

        // === 3. フェーズをEndに進めてから会話開始（DialogueTriggerがendDataを選ぶように） ===
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

        // === 4. 後片付け（EDへのフェードは会話終了時に自動で始まる） ===
        if (playerController != null) playerController.SetInputEnabled(true);
        if (playerActions != null) playerActions.SetInputEnabled(true);
        if (cameraFollow != null) {
            cameraFollow.SetForcedLookTarget(null);
            cameraFollow.SetUserControlEnabled(true);
        }
    }

    IEnumerator WalkBossInFrontOfPlayer() {
        Transform player = playerController.transform;
        Vector3 flatForward = player.forward;
        flatForward.y = 0f;
        if (flatForward.sqrMagnitude < 0.0001f) flatForward = Vector3.forward;
        flatForward.Normalize();

        Vector3 targetPos = player.position + flatForward * standDistance;
        targetPos.y = bossNpc.position.y; // 上司自身の足元高さを維持

        Vector3 lookDir = player.position - targetPos; // 上司はプレイヤーの方を向く
        lookDir.y = 0f;
        if (lookDir.sqrMagnitude > 0.001f) {
            bossNpc.rotation = Quaternion.LookRotation(lookDir);
        }

        while (Vector3.Distance(bossNpc.position, targetPos) > bossStopThreshold) {
            bossNpc.position = Vector3.MoveTowards(
                bossNpc.position, targetPos, bossWalkSpeed * Time.deltaTime);
            yield return null;
        }

        bossNpc.position = targetPos;
    }
}
