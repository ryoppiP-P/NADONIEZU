// 通勤区間で、目印(木・NPCなど)に近づいたら一度だけチュートリアルのポップアップを出す汎用スクリプト。
// 会話UIと同じ考え方で、ポップアップ表示中はプレイヤー操作・視点(カメラ)操作を止め、
// 「わかった！」ボタンを1回クリックするまで進めない。
// もとは TutorialTreeHint（木を殴るヒント専用）だったものを、テキスト差し替え可能な汎用版にした。
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TutorialHint : MonoBehaviour {
    [Header("トリガー")]
    [Tooltip("目印にする対象。このTransformとの距離が triggerRadius 以内に入ったら表示する")]
    public Transform target;
    public float triggerRadius = 6f;

    [Header("表示テキスト（空欄なら現在のポップアップの文言をそのまま使う）")]
    [TextArea] public string title = "";
    [TextArea] public string body = "";

    [Header("参照")]
    public PlayerController playerController;
    public PlayerActions playerActions;
    [Tooltip("ポップアップ表示中は視点操作も止める。未設定ならシーンから自動取得")]
    public CameraFollow cameraFollow;
    [Tooltip("この会話トリガーと会話済みならヒントを出さない（任意。話す系ヒント用）")]
    public DialogueTrigger skipIfTalked;

    [Header("UI")]
    public CanvasGroup popupGroup;
    public Button okButton;
    [Tooltip("title を流し込む先。未設定ならテキストは変更しない")]
    public TMP_Text titleLabel;
    [Tooltip("body を流し込む先。未設定ならテキストは変更しない")]
    public TMP_Text bodyLabel;
    public float fadeDuration = 0.25f;

    bool shown;
    bool waitingForClick;

    void Awake() {
        if (okButton != null) okButton.onClick.AddListener(OnOkClicked);
        if (cameraFollow == null) cameraFollow = FindFirstObjectByType<CameraFollow>();
        if (popupGroup != null) {
            popupGroup.alpha = 0f;
            popupGroup.gameObject.SetActive(false);
        }
    }

    void Update() {
        if (shown || target == null || playerController == null) return;
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsActive) return;
        if (OpeningCutsceneController.IsPlaying == false) return; // 通勤区間以外では出さない
        if (playerActions != null && !playerActions.IsInputEnabled) return; // クビ理由テキスト表示中などはまだ出さない
        if (skipIfTalked != null && skipIfTalked.HasTalkedInCurrentPhase) { shown = true; return; } // もう会話済みなら不要

        if (Vector3.Distance(playerController.transform.position, target.position) <= triggerRadius) {
            shown = true;
            StartCoroutine(ShowPopup());
        }
    }

    IEnumerator ShowPopup() {
        if (playerController != null) playerController.SetInputEnabled(false);
        if (playerActions != null) playerActions.SetInputEnabled(false);
        if (cameraFollow != null) cameraFollow.SetUserControlEnabled(false); // 視点操作を止める
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (titleLabel != null && !string.IsNullOrEmpty(title)) titleLabel.text = title;
        if (bodyLabel != null && !string.IsNullOrEmpty(body)) bodyLabel.text = body;

        waitingForClick = true;
        popupGroup.gameObject.SetActive(true);
        yield return Fade(0f, 1f);

        yield return new WaitUntil(() => !waitingForClick);

        yield return Fade(1f, 0f);
        popupGroup.gameObject.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        if (playerController != null) playerController.SetInputEnabled(true);
        if (playerActions != null) playerActions.SetInputEnabled(true);
        if (cameraFollow != null) cameraFollow.SetUserControlEnabled(true);
    }

    IEnumerator Fade(float from, float to) {
        float t = 0f;
        while (t < fadeDuration) {
            t += Time.unscaledDeltaTime;
            popupGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / fadeDuration));
            yield return null;
        }
        popupGroup.alpha = to;
    }

    public void OnOkClicked() { waitingForClick = false; }
}
