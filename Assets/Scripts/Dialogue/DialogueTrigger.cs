// NPCにアタッチする会話トリガー
using UnityEngine;

public class DialogueTrigger : MonoBehaviour, IInteractable {
    public DialogueData data;

    void Start() {
        NPCManager.Instance?.Register(this);
    }

    public void Interact() {
        if (data == null) return;
        if (DialogueManager.Instance == null) return;

        DialogueManager.Instance.StartDialogue(data, this);
    }
}
