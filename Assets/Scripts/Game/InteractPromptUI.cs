// 注視中の対象タグに応じて「[キー] アクション名」を画面に表示するUI。
// PlayerActions.UpdateHighlight() からハイライトと同じタイミングで
// SetTarget(hit.collider) / Clear() を呼ばれる想定。
using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class InteractPromptUI : MonoBehaviour {
    [System.Serializable]
    public class PromptEntry {
        public string tag;
        public string actionLabel;
        public string keyLabel = "[E]";
    }

    [Header("タグ → 表示内容の対応表")]
    public List<PromptEntry> promptEntries = new List<PromptEntry> {
        new PromptEntry { tag = "Pickable",       actionLabel = "持つ", keyLabel = "[E]" },
        new PromptEntry { tag = "NPC",            actionLabel = "話す", keyLabel = "[E]" },
        new PromptEntry { tag = "Breakable",       actionLabel = "殴る", keyLabel = "[右クリック]" },
        new PromptEntry { tag = "ElevatorButton", actionLabel = "押す", keyLabel = "[E]" },
    };

    [Header("UI参照")]
    public CanvasGroup canvasGroup;
    public TextMeshProUGUI actionLabel;
    public TextMeshProUGUI keyLabel;

    [Header("フェード")]
    [Tooltip("表示/非表示の切り替えにかける秒数")]
    public float fadeDuration = 0.15f;

    Collider currentTarget;
    float targetAlpha;
    float currentAlpha;

    void Awake() {
        if (canvasGroup != null) canvasGroup.alpha = 0f;
    }

    void Update() {
        if (canvasGroup == null) return;
        currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, Time.deltaTime / Mathf.Max(0.0001f, fadeDuration));
        canvasGroup.alpha = currentAlpha;
    }

    /// <summary>注視対象を通知する。対象が変わらない限りテキストは再設定しない。</summary>
    public void SetTarget(Collider hitCollider) {
        if (hitCollider == null) { Clear(); return; }

        // 会話済みNPCは非表示（ハイライト側の除外条件と揃える）
        if (hitCollider.CompareTag("NPC")) {
            var trigger = hitCollider.GetComponentInParent<DialogueTrigger>();
            if (trigger != null && trigger.HasTalkedInCurrentPhase) { Clear(); return; }
        }

        PromptEntry match = null;
        foreach (var entry in promptEntries) {
            if (hitCollider.CompareTag(entry.tag)) { match = entry; break; }
        }

        if (match == null) { Clear(); return; }

        if (currentTarget != hitCollider) {
            currentTarget = hitCollider;
            if (actionLabel != null) actionLabel.text = match.actionLabel;
            if (keyLabel != null) keyLabel.text = match.keyLabel;
            // プロンプトが出た(対象が変わった)瞬間の控えめな通知音
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySE2D(SE.InteractPromptShow);
        }

        targetAlpha = 1f;
    }

    /// <summary>対象が無い/条件を満たさない場合に呼ぶ。フェードアウトする。</summary>
    public void Clear() {
        currentTarget = null;
        targetAlpha = 0f;
    }
}
