// オブジェクトの「名前」から、殴った音/壊れた音を引くための対応表。
//
// 設計: 各オブジェクトに音の設定を付けて回らない。名前の一部(キーワード)で対応表を引く。
//  - モデルを置き換えても、名前さえ規則どおりなら自動で正しい音が鳴る。
//  - 一致判定はそのオブジェクト自身の名前から始め、見つからなければ親の名前へ遡る
//    (壊れるのが子メッシュ "Screen"、名前を持つのが親 "model_PcMonitor" のような階層でも当たる)。
//  - ルールは上から順に判定する。より具体的な名前を上に置く。
//  - ルールが指すSEにクリップが未登録(音素材がまだ無い)の時は、既定の音にフォールバックする。
using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ObjectSfxTable", menuName = "Audio/Object Sfx Table")]
public class ObjectSfxTable : ScriptableObject {
    [Serializable]
    public class Rule {
        [Tooltip("管理用のメモ(ゲームでは使わない)")]
        public string label;
        [Tooltip("名前にこのどれかが含まれていれば一致(大文字小文字は区別しない)")]
        public string[] nameKeywords;
        [Tooltip("殴った/蹴った時の音。None または素材未登録なら既定の音")]
        public SE hit = SE.None;
        [Tooltip("壊れた(倒れた)時の音。None または素材未登録なら既定の音")]
        public SE broken = SE.None;
    }

    public List<Rule> rules = new List<Rule>();

    [Header("どのルールにも一致しない物(=まだ名前が付いていない仮オブジェクト等)")]
    public SE defaultHit = SE.None;
    public SE defaultBroken = SE.None;

    [Header("一致判定で名前を遡る親の階層数")]
    [Range(0, 6)] public int parentLevels = 3;

    /// <summary>t自身→親…の順に名前を調べ、最初に一致したルールを返す</summary>
    public bool TryResolve(Transform t, out Rule rule) {
        rule = null;
        Transform cur = t;
        for (int level = 0; cur != null && level <= parentLevels; level++, cur = cur.parent) {
            var r = FindByName(cur.name);
            if (r != null) { rule = r; return true; }
        }
        return false;
    }

    Rule FindByName(string objName) {
        foreach (var r in rules) {
            if (r == null || r.nameKeywords == null) continue;
            foreach (var kw in r.nameKeywords) {
                if (string.IsNullOrEmpty(kw)) continue;
                if (objName.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0) return r;
            }
        }
        return null;
    }
}
