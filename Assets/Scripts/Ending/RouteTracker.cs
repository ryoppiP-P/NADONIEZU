// ルート獲得ptを管理するシングルトン
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class RouteTracker : MonoBehaviour {
    public static RouteTracker Instance { get; private set; }

    // 各ルートの獲得pt
    Dictionary<RouteType, int> points = new Dictionary<RouteType, int>();

    void Awake() {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject); // EDシーンに情報を持ち越す

        // 全ルートを0で初期化
        foreach (RouteType r in System.Enum.GetValues(typeof(RouteType)))
            points[r] = 0;
    }

    public void Add(RouteType route) {
        points[route]++;
        Debug.Log($"[Route] +1 {route} (total: {points[route]})");
    }

    public int Get(RouteType route) => points[route];

    public Dictionary<RouteType, int> GetAll() => new Dictionary<RouteType, int>(points);

    // 最終判定
    public EndingResult Judge() {
        int total = points.Values.Sum();

        // 虚無: 全部0pt
        if (total == 0)
            return new EndingResult { kind = EndingKind.Void, routes = new List<RouteType>() };

        // 最大値を取るルートを列挙
        int max = points.Values.Max();
        var winners = points.Where(kv => kv.Value == max)
                            .Select(kv => kv.Key)
                            .OrderBy(r => (int)r) // 順序を安定させる
                            .ToList();

        if (winners.Count == 1)
            return new EndingResult { kind = EndingKind.Single, routes = winners };
        else
            return new EndingResult { kind = EndingKind.Mixed, routes = winners };
    }
}

public enum EndingKind {
    Single,  // 単独最多
    Mixed,   // 同率複数
    Void     // 全0pt
}

public class EndingResult {
    public EndingKind kind;
    public List<RouteType> routes; // 該当ルート（Singleなら1つ、Mixedなら複数、Voidなら空）

    // 混合ルート識別用のキー（例: "Romance_Madness"）
    public string GetMixKey() {
        return string.Join("_", routes);
    }
}
