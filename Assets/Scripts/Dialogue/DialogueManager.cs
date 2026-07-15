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

    void Awake() {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>会話開始</summary>
    public void StartDialogue(DialogueData data) {
        if (IsActive || data == null) return;

        currentData = data;
        currentLineIndex = 0;
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

        currentLineIndex++;

        if (currentLineIndex < currentData.lines.Count) {
            ShowCurrentLine();
        }
        else {
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

        EndDialogue();
    }

    void EndDialogue() {
        IsActive = false;
        currentData = null;
        ui.Hide();

        // カーソル再ロック
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        OnDialogueEnded?.Invoke();
    }
}
