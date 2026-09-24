// 押すと OfficeDemolisher を起動する「全壊スイッチ」。
// プレイヤーが[E]で押せる(既存のIInteractable方式。エレベーターのボタンと同じ経路)。
//
// 配線は不要: demolisher を未設定なら、シーン内のOfficeDemolisherを自動で探す。
// OfficeDemolisherが1個だけなら、このコンポーネントを付けるだけで動く。
// 建物の一部だけ壊したい等で複数ある場合だけ、demolisherに明示的に指定する。
using System.Collections;
using UnityEngine;

public class DemolitionSwitch : MonoBehaviour, IInteractable {
    [Tooltip("未設定ならシーンから自動で探す(1個だけの前提なら空欄でOK)")]
    public OfficeDemolisher demolisher;
    [Tooltip("一度押したらもう押せない(押した後の後片付けが無いので基本ON)")]
    public bool oneShot = true;

    [Header("押し込み演出(任意)")]
    [Tooltip("押した時に沈み込むボタンの見た目。未設定なら何も動かない")]
    public Transform buttonCap;
    public float pressDepth = 0.05f;
    public float pressDuration = 0.12f;

    bool used;
    Vector3 capRestLocalPos;

    void Awake() {
        if (buttonCap != null) capRestLocalPos = buttonCap.localPosition;
    }

    public void Interact() {
        if (used && oneShot) return;

        var d = demolisher != null ? demolisher : FindFirstObjectByType<OfficeDemolisher>();
        if (d == null) {
            Debug.LogWarning("[DemolitionSwitch] OfficeDemolisherが見つからない。親オブジェクトにアタッチしてください");
            return;
        }
        if (d.IsDemolishing) return;
        if (!d.Demolish(transform.position)) return; // 壊す物が無い時は「押した」扱いにしない

        used = true;
        if (buttonCap != null) StartCoroutine(Press());
    }

    IEnumerator Press() {
        Vector3 from = capRestLocalPos;
        Vector3 to = capRestLocalPos + Vector3.down * pressDepth;
        float t = 0f;
        while (t < pressDuration) {
            t += Time.unscaledDeltaTime;
            buttonCap.localPosition = Vector3.Lerp(from, to, Mathf.Clamp01(t / pressDuration));
            yield return null;
        }
        buttonCap.localPosition = to;
        if (oneShot) yield break; // 押しっぱなしのまま残す

        yield return new WaitForSecondsRealtime(0.15f);
        buttonCap.localPosition = from;
    }
}
