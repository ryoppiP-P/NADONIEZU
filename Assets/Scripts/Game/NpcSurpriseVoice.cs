// NPCに付ける「近くで物が壊れたら驚いた声を出す」担当。
// 壊れた通知は ObjectSfx.PlayBroken → NpcSurpriseVoice.NotifyBreak で届く。
// 全壊スイッチのように一度に大量に壊れても、全員が合唱にならないよう
//   ・NPCごとのクールダウン
//   ・全体で0.5秒に1回まで、1回で驚くのは最大maxReactorsPerEvent人
// で間引く。距離が近い順に驚かせる。
using System.Collections.Generic;
using UnityEngine;

public class NpcSurpriseVoice : MonoBehaviour {
    public enum VoiceType { Male, Female }

    [Tooltip("驚いた時の声。Male=NpcSurpriseMale / Female=NpcSurpriseFemale")]
    public VoiceType voice = VoiceType.Male;

    [Tooltip("この距離(m)以内で壊れたら驚く")]
    public float reactRadius = 14f;

    [Tooltip("同じNPCが再び驚くまでの最短秒数")]
    public float cooldown = 6f;

    [Tooltip("フロアが違う(高さがこれ以上離れている)壊れ音には反応しない")]
    public float maxHeightDiff = 3.5f;

    static readonly List<NpcSurpriseVoice> all = new List<NpcSurpriseVoice>();
    static float lastEventTime = -999f;
    const float GlobalInterval = 0.5f;
    const int MaxReactorsPerEvent = 2;

    float lastReact = -999f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { all.Clear(); lastEventTime = -999f; }

    void OnEnable() { if (!all.Contains(this)) all.Add(this); }
    void OnDisable() { all.Remove(this); }

    /// <summary>物が壊れた位置を通知する。近くのNPCが驚く。</summary>
    public static void NotifyBreak(Vector3 position) {
        float now = Time.unscaledTime;
        if (now - lastEventTime < GlobalInterval) return;
        if (AudioManager.Instance == null || all.Count == 0) return;

        // 近い順に、条件を満たすNPCを最大MaxReactorsPerEvent人まで
        var candidates = new List<NpcSurpriseVoice>();
        foreach (var n in all) {
            if (n == null || now - n.lastReact < n.cooldown) continue;
            Vector3 d = n.transform.position - position;
            if (Mathf.Abs(d.y) > n.maxHeightDiff) continue;
            d.y = 0f;
            if (d.sqrMagnitude > n.reactRadius * n.reactRadius) continue;
            candidates.Add(n);
        }
        if (candidates.Count == 0) return;

        candidates.Sort((a, b) =>
            (a.transform.position - position).sqrMagnitude.CompareTo((b.transform.position - position).sqrMagnitude));

        lastEventTime = now;
        for (int i = 0; i < candidates.Count && i < MaxReactorsPerEvent; i++) candidates[i].React();
    }

    void React() {
        lastReact = Time.unscaledTime;
        var se = voice == VoiceType.Female ? SE.NpcSurpriseFemale : SE.NpcSurpriseMale;
        // 頭のあたりから鳴らす(足元より自然)
        AudioManager.Instance.PlaySEAtPosition(se, transform.position + Vector3.up * 1.5f);
    }
}
