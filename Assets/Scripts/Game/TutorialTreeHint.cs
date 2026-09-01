// 通勤区間で、目印の木に近づいたら一度だけ「殴って壊そう」チュートリアルポップアップを出す。
// 会話の選択肢と同じ考え方で、プレイヤー操作を止めてボタンを1回クリックするまで進めない。
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class TutorialTreeHint : MonoBehaviour {
    [Header("参照")]
    [Tooltip("目印にする木。このTransformとの距離が近づいたら表示する")]
    public Transform target;
    public float triggerRadius = 6f;
    public PlayerController playerController;
    public PlayerActions playerActions;

    [Header("UI")]
    public CanvasGroup popupGroup;
    public Button okButton;
    public float fadeDuration = 0.25f;

    bool shown;
    bool waitingForClick;

    void Awake() {
        if (okButton != null) okButton.onClick.AddListener(OnOkClicked);
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

        if (Vector3.Distance(playerController.transform.position, target.position) <= triggerRadius) {
            shown = true;
            StartCoroutine(ShowPopup());
        }
    }

    IEnumerator ShowPopup() {
        if (playerController != null) playerController.SetInputEnabled(false);
        if (playerActions != null) playerActions.SetInputEnabled(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

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
