using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class Deformable : MonoBehaviour {
    [Header("Subdivision")]
    [Tooltip("起動時のメッシュ細分化レベル。ローポリなら2〜3推奨。上げすぎ注意。")]
    [Range(0, 4)] public int subdivideLevel = 3;

    [Header("Dent Settings")]
    [Tooltip("へこみの影響半径（ワールド単位）")]
    public float radius = 0.3f;

    [Tooltip("力→へこみ深さの倍率")]
    public float depthPerForce = 0.01f;

    [Tooltip("1回のへこみの最小深さ。パンチ/投げの力がどれだけ弱くても、当たった以上は必ずこれだけ見た目に凹む")]
    public float minDentPerHit = 0.24f;

    [Tooltip("1回のへこみの最大深さ")]
    public float maxDentPerHit = 0.4f;

    [Tooltip("累積の最大へこみ深さ（1頂点あたり）")]
    public float maxTotalDent = 0.6f;

    [Header("Collider Update")]
    [Tooltip("MeshColliderもへこませる（重い）")]
    public bool updateMeshCollider = true;

    [Header("Realistic Deformation")]
    [Tooltip("凹みの縁が盛り上がる強さ（塑性変形のリング効果）。0で無効")]
    [Range(0f, 1f)] public float rimBulgeStrength = 0.25f;

    [Tooltip("凹み範囲の外側にも影響を及ぼす広がり倍率。1.0で無効、2.0だと2倍の範囲まで緩やかに凹む")]
    [Range(1f, 4f)] public float wideInfluenceMultiplier = 2.5f;

    [Tooltip("広域影響の強さ。中心の凹みに対する割合。0.1なら中心の10%程度、離れた頂点も動く")]
    [Range(0f, 0.5f)] public float wideInfluenceStrength = 0.8f;

    [Header("IKD")]
    [Tooltip("実際に見た目が凹んだ1ヒットあたりのIKD加算量")]
    public int ikdGain = 2;

    Mesh mesh;
    Vector3[] vertices;
    Vector3[] originalVertices; // 累積計算用に元位置を保持
    MeshCollider meshCollider;

    void Awake() {
        MeshFilter mf = GetComponent<MeshFilter>();

        // 細分化してインスタンス化
        Mesh src = mf.sharedMesh;
        Debug.Log($"[Deformable] Awake {gameObject.name} " +
                  $"srcMesh={src.name} srcVerts={src.vertexCount} " +
                  $"isStatic={gameObject.isStatic}");
        mesh = subdivideLevel > 0
            ? MeshSubdivider.Subdivide(src, subdivideLevel)
            : Object.Instantiate(src);

        mf.mesh = mesh;
        // MeshFilter.mesh のセッターは代入時にさらに複製を作るため、
        // 代入後に mf.mesh を取り直さないと、以降 ApplyDent が書き換えるのが
        // 実際に描画されているインスタンスと別物になり、見た目に反映されない。
        mesh = mf.mesh;
        vertices = mesh.vertices;
        originalVertices = (Vector3[])vertices.Clone();
        Debug.Log($"[Deformable] After subdivide verts={vertices.Length} " +
                  $"finalMesh={mesh.name}");

        if (updateMeshCollider)
            meshCollider = GetComponent<MeshCollider>();
    }

    /// <summary>
    /// 外部から呼ぶへこみ処理。
    /// worldPoint: 衝突点（ワールド）
    /// worldNormal: 押し込み方向（通常は衝突法線の逆向き=面を押す方向）
    /// force: 衝撃の強さ
    /// </summary>
    public void ApplyDent(Vector3 worldPoint, Vector3 worldNormal, float force) {
        Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
        Vector3 localDir = transform.InverseTransformDirection(worldNormal).normalized;

        float dent = Mathf.Clamp(force * depthPerForce, minDentPerHit, maxDentPerHit);

        // depthPerForce/minDentPerHit/maxDentPerHit はワールド単位(m)で設定する値だが、
        // 頂点配列はローカル座標なので、親階層にスケールが掛かっている場合
        // （例：建物モデルの1/100スケールなど）はローカル単位に変換しないと、
        // ワールドに出たときに凹みが縮んでほぼ見えなくなる。radiusと同じ変換を掛ける。
        float lossyScaleX = transform.lossyScale.x;
        if (Mathf.Abs(lossyScaleX) > 0.0001f) dent /= lossyScaleX;

        float localRadius = radius / lossyScaleX;
        float wideRadius = localRadius * wideInfluenceMultiplier;
        float wideSqrRadius = wideRadius * wideRadius;

        bool changed = false;
        int nearestIndex = -1;
        float nearestSqrDist = float.MaxValue;

        for (int i = 0; i < vertices.Length; i++) {
            Vector3 delta = vertices[i] - localPoint;
            float sqrDist = delta.sqrMagnitude;

            if (sqrDist < nearestSqrDist) {
                nearestSqrDist = sqrDist;
                nearestIndex = i;
            }

            // 広域影響範囲より外は完全にスキップ
            if (sqrDist > wideSqrRadius) continue;

            float dist = Mathf.Sqrt(sqrDist);
            float normalized = dist / localRadius; // 0=中心, 1=通常の凹み縁, >1=広域影響ゾーン

            float displacement;

            if (normalized <= 1f) {
                // === ゾーンA：メインの凹み（中心付近） ===
                // 中央が最も深く、縁に向かって滑らかに減衰
                float t = 1f - normalized;
                float coreFalloff = t * t * (3f - 2f * t); // smoothstep
                displacement = dent * coreFalloff;

            } else if (normalized <= 1.3f && rimBulgeStrength > 0f) {
                // === ゾーンB：凹み縁の盛り上がり（リング状の膨らみ） ===
                // 塑性変形で押しのけられた材料が縁に集まる効果
                // 1.0〜1.3の範囲でsin波形の盛り上がりを作る
                float rimT = (normalized - 1f) / 0.3f; // 0→1
                float rimShape = Mathf.Sin(rimT * Mathf.PI); // 0→1→0の山形
                displacement = -dent * rimBulgeStrength * rimShape; // マイナス=押し込みと逆向き

            } else {
                // === ゾーンC：広域の緩やかな歪み ===
                // パネル全体が応力で撓む効果。凹みと同方向に、ごく弱く
                float wideT = 1f - (normalized - 1.3f) / (wideInfluenceMultiplier - 1.3f);
                wideT = Mathf.Clamp01(wideT);
                float wideFalloff = wideT * wideT;
                displacement = dent * wideInfluenceStrength * wideFalloff;
            }

            if (Mathf.Abs(displacement) > 0.0001f) {
                DisplaceVertex(i, localDir, displacement);
                changed = true;
            }
        }

        // 半径内に頂点が無かった場合のフォールバック
        if (!changed && nearestIndex >= 0) {
            DisplaceVertex(nearestIndex, localDir, dent);
            changed = true;
        }

        if (changed) {
            mesh.vertices = vertices;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            if (updateMeshCollider && meshCollider != null) {
                meshCollider.sharedMesh = null;
                meshCollider.sharedMesh = mesh;
            }

            if (IKDManager.Instance != null)
                IKDManager.Instance.Add(ikdGain);
        }
    }

    void DisplaceVertex(int i, Vector3 localDir, float amount) {
        Vector3 newPos = vertices[i] + localDir * amount;

        // maxTotalDentもワールド単位(m)なので、dentと同様にローカル単位へ変換して比較する
        float lossyScaleX = transform.lossyScale.x;
        float localMaxTotalDent = Mathf.Abs(lossyScaleX) > 0.0001f ? maxTotalDent / lossyScaleX : maxTotalDent;

        // 元位置からの累積制限
        Vector3 totalDisp = newPos - originalVertices[i];
        if (totalDisp.magnitude > localMaxTotalDent)
            newPos = originalVertices[i] + totalDisp.normalized * localMaxTotalDent;

        vertices[i] = newPos;
    }
}
