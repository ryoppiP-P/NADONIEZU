// 殴った音/壊れた音を、オブジェクトの名前(ObjectSfxTable)から決めて鳴らす窓口。
// 呼び出し側(PlayerPunch / FragmentDecay)は「この物に対して鳴らして」と渡すだけ。
//
// 全壊(OfficeDemolisher)のように一度に大量の物が壊れても音が団子にならないよう、
// 同じSEは minInterval 秒以内に連続で鳴らさない。
using System.Collections.Generic;
using UnityEngine;

public static class ObjectSfx {
    // Assets/Resources/Audio/ObjectSfxTable.asset
    const string TableResourcePath = "Audio/ObjectSfxTable";

    /// <summary>同じSEを連続で鳴らさない最短間隔(秒)</summary>
    public static float minInterval = 0.06f;

    static ObjectSfxTable table;
    static bool loaded;
    static readonly Dictionary<SE, float> lastPlayed = new Dictionary<SE, float>();
    static readonly HashSet<string> reportedNames = new HashSet<string>();

    // Domain Reloadを切っている場合にstaticが残るのを防ぐ
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() {
        table = null; loaded = false;
        lastPlayed.Clear(); reportedNames.Clear();
    }

    static ObjectSfxTable Table {
        get {
            if (!loaded) {
                loaded = true;
                table = Resources.Load<ObjectSfxTable>(TableResourcePath);
                if (table == null) Debug.LogWarning("[ObjectSfx] " + TableResourcePath + " が見つからない。殴る/壊れる音は鳴らない");
            }
            return table;
        }
    }

    /// <summary>殴った/蹴った時(ヒット音)。collider は当たった相手</summary>
    public static void PlayHit(Collider target, Vector3 position) {
        if (target == null) return;
        PlayFor(IdentityOf(target), position, hit: true);
    }

    /// <summary>壊れた時。go は壊れた物</summary>
    public static void PlayBroken(GameObject go, Vector3 position) {
        if (go == null) return;
        PlayFor(go.transform, position, hit: false);
    }

    // 当たったコライダーが子メッシュでも、壊れる本体(Fracture/Deformable)の名前から引く
    static Transform IdentityOf(Collider c) {
        var f = c.GetComponentInParent<Fracture>();
        if (f != null) return f.transform;
        var d = c.GetComponentInParent<Deformable>();
        if (d != null) return d.transform;
        return c.transform;
    }

    static void PlayFor(Transform t, Vector3 position, bool hit) {
        var tbl = Table;
        var am = AudioManager.Instance;
        if (tbl == null || am == null) return;

        SE se;
        if (tbl.TryResolve(t, out var rule)) {
            se = hit ? rule.hit : rule.broken;
            if (se == SE.None || !am.HasSE(se)) se = hit ? tbl.defaultHit : tbl.defaultBroken;
        } else {
            // 名前に対応するルールが無い物は一度だけ知らせる(対応表に足すべき名前が分かるように)
            if (reportedNames.Add(t.name))
                Debug.Log("[ObjectSfx] 名前に一致するルール無し → 既定の音: " + t.name);
            se = hit ? tbl.defaultHit : tbl.defaultBroken;
        }

        if (se == SE.None || !am.HasSE(se)) return;

        float now = Time.unscaledTime; // ヒットストップ中(timeScale≒0)でも間引きが働くように
        if (lastPlayed.TryGetValue(se, out float last) && now - last < minInterval) return;
        lastPlayed[se] = now;

        am.PlaySEAtPosition(se, position);
    }
}
