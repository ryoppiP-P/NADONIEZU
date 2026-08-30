// PlayerActions(ハイライト/インタラクト)とPlayerPunch(攻撃)が、同じ狙い方向に対して
// 必ず同じ対象を選ぶようにするための共通の的選定ロジック。
// 別々に実装していると、半径や優先順位の微妙な差で「ハイライトされている物と
// 実際に殴って壊れる物が違う」というズレが起きるため、選定処理そのものを一箇所に集約する。
using UnityEngine;

public static class AimCast {
    /// <summary>
    /// 画面中心の狙い線(Raycast)を最優先で判定し、そこに何も無い時だけ
    /// 多少のズレを許容するSphereCastにフォールバックする。
    /// </summary>
    /// <param name="ignoreRoot">この階層配下のコライダーは対象から除外する（通常はプレイヤー自身）</param>
    public static bool TryGetClosestHit(Vector3 origin, Vector3 dir, float range, float sphereRadius, LayerMask mask, Transform ignoreRoot, out RaycastHit hit) {
        hit = default;

        var rayHits = Physics.RaycastAll(origin, dir, range, mask, QueryTriggerInteraction.Ignore);
        float closestRayDist = float.MaxValue;
        bool foundRay = false;
        RaycastHit rayHit = default;

        foreach (var h in rayHits) {
            if (!IsValid(h, ignoreRoot)) continue;
            if (h.distance < closestRayDist) {
                closestRayDist = h.distance;
                rayHit = h;
                foundRay = true;
            }
        }

        if (foundRay) {
            hit = rayHit;
            return true;
        }

        var sphereHits = Physics.SphereCastAll(origin, sphereRadius, dir, range, mask, QueryTriggerInteraction.Ignore);
        float closestSphereDist = float.MaxValue;
        bool foundSphere = false;

        foreach (var h in sphereHits) {
            if (!IsValid(h, ignoreRoot)) continue;
            if (h.distance < closestSphereDist) {
                closestSphereDist = h.distance;
                hit = h;
                foundSphere = true;
            }
        }

        return foundSphere;
    }

    static bool IsValid(RaycastHit h, Transform ignoreRoot) {
        if (h.collider == null) return false;
        if (ignoreRoot != null && h.collider.transform.root == ignoreRoot) return false;
        if (h.distance <= 0.001f) return false;
        return true;
    }
}
