// NPC‚Æ‚Ì‰ï˜b‚ª‘SI—¹‚µ‚½‚©ŠÇ—
using System.Collections.Generic;
using UnityEngine;

public class NPCManager : MonoBehaviour {
    public static NPCManager Instance { get; private set; }

    HashSet<DialogueTrigger> allNpcs = new HashSet<DialogueTrigger>();
    HashSet<DialogueTrigger> talkedNpcs = new HashSet<DialogueTrigger>();

    public bool IsAllTalked => allNpcs.Count > 0 && talkedNpcs.Count >= allNpcs.Count;

    void Awake() {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void Register(DialogueTrigger npc) {
        allNpcs.Add(npc);
        Debug.Log($"[NPC] Registered: {npc.name} (total: {allNpcs.Count})");
    }

    public void MarkTalked(DialogueTrigger npc) {
        if (!allNpcs.Contains(npc)) return;
        if (talkedNpcs.Add(npc)) {
            Debug.Log($"[NPC] Talked: {npc.name} ({talkedNpcs.Count}/{allNpcs.Count})");
        }
    }

    public bool HasTalked(DialogueTrigger npc) => talkedNpcs.Contains(npc);
}
