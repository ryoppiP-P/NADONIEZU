// Endingのデータ
using UnityEngine;

[CreateAssetMenu(menuName = "Ending/EndingData")]
public class EndingData : ScriptableObject {
    public string endingTitle;              // 例: "狂愛エンド"
    [TextArea(5, 15)] public string bodyText; // 本文

    [Header("マッチング条件")]
    public EndingKind kind;                  // Single / Mixed / Void
    public RouteType[] routes;               // 対象ルート（順不同OK）
}
