// Endingのデータベース
using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Ending/EndingDatabase")]
public class EndingDatabase : ScriptableObject {
    public EndingData[] endings;
    public EndingData fallback; // マッチしなかった時用

    public EndingData Find(EndingResult result) {
        if (result == null) return fallback;

        foreach (var ed in endings) {
            if (ed.kind != result.kind) continue;

            // Voidは条件マッチだけでOK
            if (result.kind == EndingKind.Void) return ed;

            // ルートの集合が一致するかチェック（順不同）
            if (ed.routes.Length != result.routes.Count) continue;

            var setA = ed.routes.OrderBy(r => (int)r).ToArray();
            var setB = result.routes.OrderBy(r => (int)r).ToArray();

            bool match = true;
            for (int i = 0; i < setA.Length; i++) {
                if (setA[i] != setB[i]) { match = false; break; }
            }

            if (match) return ed;
        }

        Debug.LogWarning($"[Ending] No match for {result.kind} : {string.Join(",", result.routes)}");
        return fallback;
    }
}
