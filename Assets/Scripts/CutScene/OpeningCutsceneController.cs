using System.Collections;
using UnityEngine;
using TMPro;

public class OpeningCutsceneController : MonoBehaviour {
    [Header("参照")]
    [Tooltip("プレイヤーのPlayerController")]
    public PlayerController playerController;

    [Tooltip("プレイヤーのPlayerActions（インタラクト・パンチ用）")]
    public PlayerActions playerActions;

    [Tooltip("上司NPCのTransform")]
    public Transform bossNpc;

    [Tooltip("上司NPCの会話トリガー")]
    public DialogueTrigger bossTrigger;

    [Tooltip("上司の最終停止位置（プレイヤー前方に配置した空GameObject）")]
    public Transform bossStopPoint;

    [Tooltip("カットシーン中にプレイヤーの視点操作を止めて上司を注視させるカメラ")]
    public CameraFollow cameraFollow;

    [Header("クビ理由Object")]
    public GameObject reasonObject;

    [Tooltip("理由メッセージのCanvasGroup（フェード制御用）")]
    public CanvasGroup reasonTextGroup;

    [Tooltip("理由メッセージのテキスト表示先")]
    public TextMeshProUGUI reasonText;

    [TextArea(3, 6)]
    public string reasonMessage = "本日付で、貴殿を解雇とする。";

    [Header("タイミング設定")]
    public float initialWait = 0.5f;
    public float fadeInDuration = 1.0f;   // FadeManagerによる黒→透明
    public float textFadeInDuration = 1.0f;
    public float textHoldDuration = 3.0f;
    public float textFadeOutDuration = 1.0f;
    public float bossWalkSpeed = 1.5f;
    public float bossStopThreshold = 0.05f;
    public float preDialogueWait = 0.5f;

    void Start() {
        StartCoroutine(PlayCutscene());
    }

    IEnumerator PlayCutscene() {
        // === 1. 初期状態 ===
        // プレイヤー操作停止
        if (playerController != null) playerController.SetInputEnabled(false);
        if (playerActions != null) playerActions.SetInputEnabled(false);
        if (cameraFollow != null && bossNpc != null) {
            cameraFollow.SetUserControlEnabled(false);
            cameraFollow.SetForcedLookTarget(bossNpc);
        }

        // カーソル非表示
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // テキストは透明でスタート
        if (reasonTextGroup != null) reasonTextGroup.alpha = 0f;
        if (reasonText != null) reasonText.text = reasonMessage;

        // 上司を初期位置に（Inspectorで配置済みの位置を使用）

        yield return new WaitForSeconds(initialWait);

        // === 2. 画面フェードイン（黒→透明） ===
        if (FadeManager.Instance != null) {
            yield return FadeManager.Instance.FadeFromBlack(fadeInDuration);
        }

        // === 3. クビ理由テキスト表示 ===
        if (reasonTextGroup != null) {
            yield return FadeCanvasGroup(reasonTextGroup, 0f, 1f, textFadeInDuration);
            yield return new WaitForSeconds(textHoldDuration);
            yield return FadeCanvasGroup(reasonTextGroup, 1f, 0f, textFadeOutDuration);
        }

        // === 4. 上司が歩いてくる ===
        if (bossNpc != null && bossStopPoint != null) {
            yield return WalkBossToStopPoint();
        }

        yield return new WaitForSeconds(preDialogueWait);

        // === 5. 会話開始 ===
        if (bossTrigger != null) {
            bossTrigger.Interact();

            // 会話終了まで待機
            yield return new WaitUntil(() =>
                DialogueManager.Instance != null && !DialogueManager.Instance.IsActive);
        }

        // === 6. プレイヤー操作解禁 ===
        if (playerController != null) playerController.SetInputEnabled(true);
        if (playerActions != null) playerActions.SetInputEnabled(true);
        if (cameraFollow != null) {
            cameraFollow.SetForcedLookTarget(null);
            cameraFollow.SetUserControlEnabled(true);
        }

        // 自身は役目終了
        gameObject.SetActive(false);
    }

    IEnumerator WalkBossToStopPoint() {
        Vector3 targetPos = bossStopPoint.position;

        // 上司をプレイヤー方向に向ける
        Vector3 lookDir = (targetPos - bossNpc.position);
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

        // 停止位置到着後、プレイヤーの方を向く（停止点のrotationを使う）
        bossNpc.rotation = bossStopPoint.rotation;
    }

    IEnumerator FadeCanvasGroup(CanvasGroup cg, float from, float to, float duration) {
        float t = 0f;
        cg.alpha = from;
        while (t < duration) {
            t += Time.deltaTime;
            cg.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        cg.alpha = to;
    }
}
