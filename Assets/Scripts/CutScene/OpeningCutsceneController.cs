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

    [Tooltip("上司の最終停止位置（プレイヤー前方に配置したGameObject）")]
    public Transform bossStopPoint;

    [Tooltip("カットシーン中にプレイヤーの視点操作を止めて上司を注視させるカメラ")]
    public CameraFollow cameraFollow;

    [Tooltip("カットシーン中だけ乗るTV風のGlobal Volume(通常はweight=0)")]
    public UnityEngine.Rendering.Volume cutsceneVolume;
    public float volumeFadeDuration = 0.4f;

    [Header("クビ理由Object")]
    public GameObject reasonObject;

    [Tooltip("理由メッセージのCanvasGroup（フェード制御用）")]
    public CanvasGroup reasonTextGroup;

    [Tooltip("理由メッセージのテキスト表示先")]
    public TextMeshProUGUI reasonText;

    [TextArea(3, 6)]
    public string reasonMessage = "リストラ、だってさ^^";

    [Header("通勤区間（街を歩いてオフィスへ向かう）")]
    [Tooltip("この地点に近づいたらオフィスへ入ったとみなす（オフィス前の歩道あたりに置く）")]
    public Transform officeEntrancePoint;

    [Tooltip("officeEntrancePointからこの距離以内に来たら入館判定")]
    public float officeEntranceRadius = 3f;

    [Tooltip("オフィスに入った直後、屋内のどこにプレイヤーを立たせるか（今までの初期スポーン位置）")]
    public Transform interiorStartPoint;

    [Tooltip("オフィスに入る時の暗転の長さ")]
    public float officeEnterFadeDuration = 0.4f;

    [Header("タイミング設定")]
    public float initialWait = 0.5f;
    public float fadeInDuration = 1.0f;   // FadeManagerによる街並みの表示
    public float textFadeInDuration = 1.0f;
    public float textHoldDuration = 3.0f;
    public float textFadeOutDuration = 1.0f;
    public float bossWalkSpeed = 1.5f;
    public float bossStopThreshold = 0.05f;
    public float preDialogueWait = 0.5f;

    public static bool IsPlaying { get; private set; }

    void Start() {
        StartCoroutine(PlayCutscene());
    }

    IEnumerator PlayCutscene() {
        IsPlaying = true;
        // === 1. 開始準備 ===
        // 街を歩き始めるまではプレイヤー操作を止める
        if (playerController != null) playerController.SetInputEnabled(false);
        if (playerActions != null) playerActions.SetInputEnabled(false);

        // カーソルを隠す
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // テキストは透明でスタート
        if (reasonTextGroup != null) reasonTextGroup.alpha = 0f;
        if (reasonText != null) reasonText.text = reasonMessage;

        yield return new WaitForSeconds(initialWait);

        // === 2. 画面フェードイン（街並みが見える） ===
        if (FadeManager.Instance != null) {
            yield return FadeManager.Instance.FadeFromBlack(fadeInDuration);
        }

        // === 3. クビ理由テキスト表示 ===
        if (reasonTextGroup != null) {
            yield return FadeCanvasGroup(reasonTextGroup, 0f, 1f, textFadeInDuration);
            yield return new WaitForSeconds(textHoldDuration);
            yield return FadeCanvasGroup(reasonTextGroup, 1f, 0f, textFadeOutDuration);
        }

        // === 4. 通勤区間：プレイヤーに操作を渡し、街を歩いてオフィス入口まで向かわせる ===
        // ここが操作チュートリアルを兼ねる区間（移動/視点/パンチ等を、実際に歩きながら覚えてもらう）
        if (playerController != null) playerController.SetInputEnabled(true);
        if (playerActions != null) playerActions.SetInputEnabled(true);

        if (officeEntrancePoint != null && playerController != null) {
            yield return new WaitUntil(() =>
                Vector3.Distance(playerController.transform.position, officeEntrancePoint.position) <= officeEntranceRadius);
        }

        // === 5. オフィスに入る演出：暗転してから屋内のスタート位置へテレポート ===
        if (playerController != null) playerController.SetInputEnabled(false);
        if (playerActions != null) playerActions.SetInputEnabled(false);

        if (FadeManager.Instance != null) {
            yield return FadeManager.Instance.FadeToBlack(officeEnterFadeDuration);
        }

        if (playerController != null && interiorStartPoint != null) {
            playerController.TeleportTo(interiorStartPoint.position, interiorStartPoint.rotation);
        }

        // 上司を注視させるカメラ演出は、屋内に入ってからセットする
        if (cameraFollow != null && bossNpc != null) {
            cameraFollow.SetUserControlEnabled(false);
            cameraFollow.SetForcedLookTarget(bossNpc);
        }

        if (cutsceneVolume != null) StartCoroutine(FadeVolumeWeight(cutsceneVolume, 1f, volumeFadeDuration));

        if (FadeManager.Instance != null) {
            yield return FadeManager.Instance.FadeFromBlack(officeEnterFadeDuration);
        }

        // === 6. 上司が歩いてくる ===
        if (bossNpc != null && bossStopPoint != null) {
            yield return WalkBossToStopPoint();
        }

        yield return new WaitForSeconds(preDialogueWait);

        // === 7. 会話開始 ===
        if (bossTrigger != null) {
            bossTrigger.Interact();

            // 会話終了まで待機
            yield return new WaitUntil(() =>
                DialogueManager.Instance != null && !DialogueManager.Instance.IsActive);
        }

        // === 8. プレイヤー操作を戻す ===
        if (cutsceneVolume != null) yield return FadeVolumeWeight(cutsceneVolume, 0f, volumeFadeDuration);
        if (playerController != null) playerController.SetInputEnabled(true);
        if (playerActions != null) playerActions.SetInputEnabled(true);
        if (cameraFollow != null) {
            cameraFollow.SetForcedLookTarget(null);
            cameraFollow.SetUserControlEnabled(true);
        }

        IsPlaying = false;

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

        // 停止位置到着後、プレイヤーの方向を向く（停止点のrotationを使う）
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
    IEnumerator FadeVolumeWeight(UnityEngine.Rendering.Volume vol, float to, float duration) {
        float from = vol.weight;
        float t = 0f;
        while (t < duration) {
            t += Time.unscaledDeltaTime;
            vol.weight = Mathf.Lerp(from, to, duration > 0f ? t / duration : 1f);
            yield return null;
        }
        vol.weight = to;
    }
}
