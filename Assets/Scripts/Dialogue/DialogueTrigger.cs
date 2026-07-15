// NPCにアタッチする会話トリガー
using UnityEngine;

public class DialogueTrigger : MonoBehaviour, IInteractable {
    [SerializeField] private DialogueData data;

    public void Interact() {
        if (DialogueManager.Instance == null) return;
        if (DialogueManager.Instance.IsActive) return;

        DialogueManager.Instance.StartDialogue(data);
    }
}
