// 街路樹の幹(Fracture)が壊れたら、葉っぱの塊も一緒に砕けて飛び散るようにする小さな連携。
// 葉スフィアは Trunk の子で、かつ Trunk が非uniformスケールなので、葉そのものを Fracture で
// スライスすると破片が正しく描画されない。そのため葉は「小さな葉くずを撒き散らして本体は隠す」
// 方式で破壊する（バカゲーのノリで葉っぱがブワッと散る）。
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Fracture))]
public class TreeTrunk : MonoBehaviour {
    [Tooltip("この幹にぶら下がっている葉オブジェクト")]
    public GameObject[] leaves;

    [Header("葉くずの飛散")]
    [Tooltip("葉1つあたりに撒き散らす葉くずの数")]
    public int shardsPerLeaf = 12;
    [Tooltip("葉くず1個の基準サイズ")]
    public float shardSize = 0.16f;
    [Tooltip("葉くずが弾ける勢い")]
    public float shardForce = 3.5f;
    [Tooltip("葉くずが消えるまでの寿命(秒)")]
    public float shardLifetime = 2.0f;
    [Tooltip("寿命後に縮んで消えるまでの時間(秒)")]
    public float shardShrink = 0.8f;

    Fracture fracture;
    Transform treeRoot;               // 破片の親にする（幹はSetActive(false)されるので使えない）
    Vector3[] leafWorldPos;
    float[] leafWorldRadius;
    bool shattered;

    void Awake() {
        fracture = GetComponent<Fracture>();
        // 幹の破壊はどの経路でも最後に onCompleted が呼ばれる（パンチは ComputeFracture を直接叩くので
        // onFracture は飛ばないことがある）。両方に登録しておき、多重発火は shattered で弾く。
        fracture.callbackOptions.onCompleted.AddListener(OnTrunkBroken);
        fracture.callbackOptions.onFracture.AddListener(OnTrunkBrokenWithArgs);

        treeRoot = transform.parent;

        // 葉のワールド位置と大きさを、まだ壊れていない今のうちに控えておく
        int n = leaves != null ? leaves.Length : 0;
        leafWorldPos = new Vector3[n];
        leafWorldRadius = new float[n];
        for (int i = 0; i < n; i++) {
            var leaf = leaves[i];
            if (leaf == null) continue;
            var r = leaf.GetComponentInChildren<Renderer>();
            if (r != null) {
                leafWorldPos[i] = r.bounds.center;
                leafWorldRadius[i] = r.bounds.extents.magnitude * 0.5f;
            } else {
                leafWorldPos[i] = leaf.transform.position;
                leafWorldRadius[i] = 0.5f;
            }
        }
    }

    void OnTrunkBrokenWithArgs(Collider col, GameObject go, Vector3 point) => OnTrunkBroken();

    void OnTrunkBroken() {
        if (shattered) return;
        shattered = true;

        if (leaves == null) return;
        for (int i = 0; i < leaves.Length; i++) {
            var leaf = leaves[i];
            if (leaf == null) continue;
            SpawnLeafShards(leafWorldPos[i], Mathf.Max(0.15f, leafWorldRadius[i]), GetLeafMaterial(leaf));
            leaf.SetActive(false);
        }
    }

    Material GetLeafMaterial(GameObject leaf) {
        var r = leaf.GetComponentInChildren<Renderer>();
        return r != null ? r.sharedMaterial : null;
    }

    void SpawnLeafShards(Vector3 center, float radius, Material mat) {
        int count = Mathf.Max(1, shardsPerLeaf);
        for (int i = 0; i < count; i++) {
            var shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shard.name = "LeafShard";

            var box = shard.GetComponent<Collider>();
            if (box != null) Destroy(box);   // 見た目だけ。物理は弾道のみで十分＆軽い

            if (treeRoot != null) shard.transform.SetParent(treeRoot, true);
            shard.transform.position = center + Random.insideUnitSphere * (radius * 0.8f);
            shard.transform.rotation = Random.rotation;
            float s = shardSize;
            shard.transform.localScale = new Vector3(
                s * Random.Range(0.6f, 1.4f),
                s * Random.Range(0.4f, 0.9f),
                s * Random.Range(0.6f, 1.4f));

            var mr = shard.GetComponent<MeshRenderer>();
            if (mr != null && mat != null) {
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            var rb = shard.AddComponent<Rigidbody>();
            rb.mass = 0.1f;
            rb.useGravity = true;
            rb.linearDamping = 0.1f;
            rb.angularDamping = 0.05f;
            Vector3 outward = (shard.transform.position - center).normalized;
            if (outward.sqrMagnitude < 0.001f) outward = Random.onUnitSphere;
            rb.linearVelocity = outward * (shardForce * Random.Range(0.7f, 1.4f))
                              + Vector3.up * (shardForce * Random.Range(0.4f, 1.0f));
            rb.angularVelocity = Random.insideUnitSphere * 10f;

            var decay = shard.AddComponent<FragmentDecay>();
            decay.isFragment = true;
            decay.generation = 99;          // これ以上分裂させない
            decay.maxGeneration = 0;
            decay.lifetime = shardLifetime;
            decay.shrinkDuration = shardShrink;
        }
    }

    void OnDestroy() {
        if (fracture != null) {
            fracture.callbackOptions.onCompleted.RemoveListener(OnTrunkBroken);
            fracture.callbackOptions.onFracture.RemoveListener(OnTrunkBrokenWithArgs);
        }
    }
}
