// ゲーム中にEscape(またはGamepad Start)でポーズメニューを開閉する。
// 開いている間はTime.timeScale=0で完全停止し、プレイヤー操作も無効化する。
using UnityEngine;
using UnityEngine.InputSystem;

public class PauseMenuController : MonoBehaviour {
    [Header("参照")]
    public GameObject menuRoot;
    public PlayerController playerController;
    public PlayerActions playerActions;
    public SettingsPanelUI settingsPanel;
    public CameraFollow cameraFollow;

    [Header("シーン")]
    public string titleSceneName = "TitleScene";
    public float fadeDuration = 1.5f;

    PlayerInputActions input;
    bool isPaused;

    public bool IsPaused => isPaused;

    void Awake() {
        input = new PlayerInputActions();
    }

    void OnEnable() {
        input.Player.Enable();
        input.Player.Pause.performed += OnPausePerformed;
    }

    void OnDisable() {
        input.Player.Pause.performed -= OnPausePerformed;
        input.Player.Disable();
    }

    void OnPausePerformed(InputAction.CallbackContext ctx) {
        // カットシーン中はポーズ不可
        if (OpeningCutsceneController.IsPlaying) return;
        if (EndingCutsceneController.IsPlaying) return;

        // 会話中はポーズ不可（会話UIとの競合を避ける）
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsActive) return;

        if (isPaused) Resume();
        else Open();
    }

    public void Open() {
        if (isPaused) return;

        // カットシーン中・会話中は開かない。
        // OnPausePerformed(キーボード/ゲームパッド)側にも同じガードがあるが、
        // 左上のハンバーガーボタンはonClickからここを直接呼ぶためガードを素通りしていた。
        if (OpeningCutsceneController.IsPlaying) return;
        if (EndingCutsceneController.IsPlaying) return;
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsActive) return;

        isPaused = true;

        Time.timeScale = 0f;
        if (menuRoot != null) menuRoot.SetActive(true);
        settingsPanel?.Close();

        if (playerController != null) playerController.SetInputEnabled(false);
        if (playerActions != null) playerActions.SetInputEnabled(false);
        if (cameraFollow != null) cameraFollow.SetUserControlEnabled(false); // メニュー操作中のマウス移動でカメラが回らないように

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Resume() {
        if (!isPaused) return;
        isPaused = false;

        Time.timeScale = 1f;
        if (menuRoot != null) menuRoot.SetActive(false);
        settingsPanel?.Close();

        if (playerController != null) playerController.SetInputEnabled(true);
        if (playerActions != null) playerActions.SetInputEnabled(true);
        if (cameraFollow != null) cameraFollow.SetUserControlEnabled(true);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    /// <summary>ポーズメニューの「設定」ボタンから呼ぶ</summary>
    public void OpenSettings() {
        settingsPanel?.Open();
    }

    /// <summary>ポーズメニューの「タイトルへ戻る」ボタンから呼ぶ</summary>
    public void ReturnToTitle() {
        Time.timeScale = 1f; // シーン遷移前に必ず戻す
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        FadeManager.FadeOut(titleSceneName, fadeDuration);
    }
}
