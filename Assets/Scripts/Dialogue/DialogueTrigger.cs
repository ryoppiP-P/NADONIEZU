// NPCにアタッチする会話トリガー
using UnityEngine;

public class DialogueTrigger : MonoBehaviour, IInteractable {
    [Header("フェーズ別の会話データ")]
    [Tooltip("ゲーム開始時の会話")]
    public DialogueData startData;

    [Tooltip("ゲーム中（通常時）の会話")]
    public DialogueData middleData;

    [Tooltip("ゲーム終了時の会話")]
    public DialogueData endData;

    [Header("再会話設定")]
    [Tooltip("同じフェーズ中は一度しか話せなくする")]
    public bool talkOnlyOncePerPhase = true;

    // フェーズごとの会話済みフラグ
    private bool talkedInStart;
    private bool talkedInMiddle;
    private bool talkedInEnd;

    void Start() {
        NPCManager.Instance?.Register(this);
    }

    public void Interact() {
        if (DialogueManager.Instance == null) return;
        if (DialogueManager.Instance.IsActive) return;

        GamePhase phase = GetCurrentPhase();

        // === 現在のフェーズが会話済みならスキップ ===
        if (talkOnlyOncePerPhase && HasTalkedInPhase(phase)) {
            Debug.Log($"[DialogueTrigger] {name}: {phase}フェーズでは会話済み");
            return;
        }

        DialogueData data = SelectDialogueData(phase);
        if (data == null) {
            Debug.LogWarning($"[DialogueTrigger] {name}: {phase}フェーズのDialogueData未設定");
            return;
        }

        DialogueManager.Instance.StartDialogue(data, this);
    }

    /// <summary>DialogueManagerから会話終了時に呼ばれる</summary>
    public void MarkTalked() {
        GamePhase phase = GetCurrentPhase();
        switch (phase) {
            case GamePhase.Start: talkedInStart = true; break;
            case GamePhase.Middle: talkedInMiddle = true; break;
            case GamePhase.End: talkedInEnd = true; break;
        }
    }

    /// <summary>指定フェーズで会話済みか</summary>
    public bool HasTalkedInPhase(GamePhase phase) {
        switch (phase) {
            case GamePhase.Start: return talkedInStart;
            case GamePhase.Middle: return talkedInMiddle;
            case GamePhase.End: return talkedInEnd;
            default: return false;
        }
    }

    /// <summary>現在のフェーズで会話済みか（外部参照用）</summary>
    public bool HasTalkedInCurrentPhase => HasTalkedInPhase(GetCurrentPhase());

    private GamePhase GetCurrentPhase() {
        return GameTimeManager.Instance != null
            ? GameTimeManager.Instance.CurrentPhase
            : GamePhase.Middle;
    }

    private DialogueData SelectDialogueData(GamePhase phase) {
        switch (phase) {
            case GamePhase.Start: return startData ?? middleData;
            case GamePhase.End: return endData ?? middleData;
            case GamePhase.Middle:
            default: return middleData;
        }
    }
}
