// 会話の進行管理
using UnityEngine;
using System;

public class DialogueManager : MonoBehaviour {
    public static DialogueManager Instance { get; private set; }

    [SerializeField] private DialogueUI ui;

    public bool IsActive { get; private set; }

    private DialogueData currentData;
    private int currentLineIndex;

    // 会話開始/終了の通知オブザーブ
    public event Action OnDialogueStarted;
    public event Action OnDialogueEnded;

    DialogueTrigger currentTrigger;

    // 選択後の返答表示中フラグ
    bool inResponsePhase = false;
    string[] responseQueue;
    int responseIndex;

    void Awake() {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>会話開始</summary>
    public void StartDialogue(DialogueData data, DialogueTrigger trigger = null) {
        if (IsActive || data == null) return;

        currentTrigger = trigger;

        currentData = data;
        currentLineIndex = 0;
        inResponsePhase = false;
        responseQueue = null;
        responseIndex = 0;
        IsActive = true;

        // カーソル解放
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        ui.Show();
        ui.SetSpeaker(data.speakerName);
        ShowCurrentLine();

        OnDialogueStarted?.Invoke();
    }

    /// <summary>次のセリフへ（UIから呼ばれる）</summary>
    public void Advance() {
        if (!IsActive) return;

        // === 返答フェーズ ===
        if (inResponsePhase) {
            responseIndex++;
            if (responseIndex < responseQueue.Length) {
                ui.SetLine(responseQueue[responseIndex]);
            } else {
                // 返答終わり → 会話終了
                EndDialogue();
            }
            return;
        }

        // === 通常フェーズ ===
        currentLineIndex++;

        if (currentLineIndex < currentData.lines.Count) {
            ShowCurrentLine();
        } else {
            // セリフ終了 → 選択肢があれば表示、なければ会話終了
            if (currentData.choices != null && currentData.choices.Count > 0)
                ui.ShowChoices(currentData.choices, OnChoiceSelected);
            else
                EndDialogue();
        }
    }

    void ShowCurrentLine() {
        ui.SetLine(currentData.lines[currentLineIndex]);
    }

    void OnChoiceSelected(int index) {
        var choice = currentData.choices[index];

        // IKD加算
        if (choice.ikdDelta != 0 && IKDManager.Instance != null)
            IKDManager.Instance.Add(choice.ikdDelta);

        // ルート加算
        RouteTracker.Instance?.Add(choice.route);

        // 返答セリフがあれば返答フェーズへ、なければ即終了
        if (choice.responseLines != null && choice.responseLines.Length > 0) {
            inResponsePhase = true;
            responseQueue = choice.responseLines;
            responseIndex = 0;

            // 選択肢UIを閉じて通常セリフ画面に戻す
            ui.HideChoices();
            ui.SetLine(responseQueue[0]);
        } else {
            EndDialogue();
        }
    }

    void EndDialogue() {
        // === フラグを先に取り出す ===
        bool shouldAdvancePhase = currentData != null
            && currentData.advancesToMiddlePhase
            && GameTimeManager.Instance != null
            && GameTimeManager.Instance.CurrentPhase == GamePhase.Start;

        bool shouldTriggerEnding = currentData != null && currentData.triggersEnding;

        // === 状態リセット ===
        IsActive = false;
        currentData = null;
        inResponsePhase = false;
        responseQueue = null;
        responseIndex = 0;

        ui.Hide();

        // カーソル再ロック
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // NPCが会話済みマーク（AdvancePhase()より必ず先に行う。
        // DialogueTrigger.MarkTalked()は「今のフェーズ」を見て記録するため、
        // 先にフェーズを進めてしまうと、今終わったStart会話がMiddle会話済みとして
        // 誤って記録されてしまう＝プレイヤーが本来のMiddle会話を一生行えなくなるバグになる）
        if (currentTrigger != null) {
            NPCManager.Instance?.MarkTalked(currentTrigger);
            currentTrigger.MarkTalked();
            currentTrigger = null;
        }

        // 開始フェーズなら中盤へ遷移
        if (shouldAdvancePhase) {
            GameTimeManager.Instance.AdvancePhase(GamePhase.Middle);
        }

        // 会話終了後にゲーム時間を30分進める
        GameTimeManager.Instance?.AdvanceMinutes(30);

        // ED発動判定
        if (shouldTriggerEnding) {
            EndingController.Instance?.ForceStartEnding();
        }

        OnDialogueEnded?.Invoke();
    }
}
