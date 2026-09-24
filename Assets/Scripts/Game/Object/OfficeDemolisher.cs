// オフィスの壊せる物を、親に1個アタッチするだけで全部木っ端みじんにする。
//
// 設計の要点: 「個別に登録しない」。壊す瞬間に targetRoot の子孫から対象を探して集める。
//  - オブジェクトを別モデルに置き換えても、追加しても、勝手に対象になる。リストのメンテ・個別アタッチは不要。
//  - 壊せる物 = Fracture付き。加えて「へこむだけ」の物(Deformable)も、壊す瞬間に Fracture を自動で足して砕く
//    (includeDeformables)。へこむ物を全壊対応にするために各オブジェクトへFractureを付けて回る必要はない。
//  - 建物全体ならmodel_Building_vBetaに、フロア単位ならF2_kariのような階層グループに付ければ、
//    同じコンポーネントで範囲を変えられる。街路樹など建物の外は対象外になる(親の子孫だけを見るため)。
//
// 壊し方はPlayerPunch.BreakFractureObjectと同じ(kinematic解除→ComputeFracture)。
// 一斉に壊すとフレームが固まるので、震源からの距離順に波紋状に時間差で壊す(maxBreaksPerFrameで上限)。
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class OfficeDemolisher : MonoBehaviour {
    [Header("対象")]
    [Tooltip("この階層の下にあるものを全部壊す。未設定なら自分自身(=このオブジェクトの子孫全部)")]
    public Transform targetRoot;
    [Tooltip("この階層の下は壊さない(壁・床など、誤爆させたくないものがあればここへ)")]
    public Transform[] excludeRoots;
    [Tooltip("へこむだけのオブジェクト(Deformable)も砕く。Fractureが無ければ壊す瞬間に自動で付ける")]
    public bool includeDeformables = true;

    [Header("波紋")]
    [Tooltip("震源から外へ広がる速さ(m/s)。大きいほど一斉に壊れる")]
    public float rippleSpeed = 25f;
    [Tooltip("1フレームに壊す数の上限(WebGLでの一時的なカクつき対策)")]
    public int maxBreaksPerFrame = 6;

    [Header("吹き飛び")]
    [Tooltip("破片を震源から外向きに吹き飛ばす初速(m/s)。質量に依存しない")]
    public float blastSpeed = 10f;
    [Tooltip("上向きの偏り(0=真横、1=斜め45度上)")]
    public float upwardBias = 0.6f;
    [Tooltip("破片の回転の初速(rad/s)")]
    public float spinSpeed = 6f;

    [Header("演出")]
    public float shakeMagnitude = 0.5f;
    public float shakeDuration = 1.2f;
    [Tooltip("波紋の間、この間隔で軽い揺れを入れ続ける(0で無効)")]
    public float rollingShakeInterval = 0.35f;

    [Header("スコア(パンチで壊した時と同じ扱い)")]
    [Tooltip("1個壊すごとのIKD加算。パンチと同じ10だと全壊でかなり跳ねる。0で無効")]
    public int ikdPerObject = 10;
    [Tooltip("狂気ルートの破壊カウントに入れる(RouteTracker側に加点の上限あり)")]
    public bool registerDestruction = true;

    [Header("イベント")]
    public UnityEvent onStarted;
    public UnityEvent onFinished;

    public bool IsDemolishing { get; private set; }

    class Target {
        public GameObject go;
        public Fracture fracture; // Deformableのみの物は壊す瞬間まで null(その時に自動で付ける)
        public float delay;
    }

    // Fractureを自動で付ける時にオプションを揃える手本(対象の中で最初に見つかった普通のFracture)
    Fracture templateFracture;
    FragmentDecay templateDecay;
    int uniqueCounter;

    /// <summary>UnityEvent(スイッチ等)から呼ぶ用。このオブジェクトの位置を震源にする</summary>
    public void DemolishAll() { Demolish(transform.position); }

    /// <summary>震源epicenterから波紋状に全壊させる。壊す対象が無い/実行中ならfalse</summary>
    public bool Demolish(Vector3 epicenter) {
        if (IsDemolishing) return false;

        Transform root = targetRoot != null ? targetRoot : transform;
        var targets = CollectTargets(root, epicenter);
        if (targets.Count == 0) {
            Debug.LogWarning("[OfficeDemolisher] 壊せる物が見つからない: " + root.name);
            return false;
        }

        StartCoroutine(DemolishRoutine(root, epicenter, targets));
        return true;
    }

    List<Target> CollectTargets(Transform root, Vector3 epicenter) {
        var list = new List<Target>();
        var seen = new HashSet<GameObject>();
        templateFracture = null;
        templateDecay = null;

        // 非アクティブは除外(=既に壊れて消えた物・元から無効の物)。実行時に毎回拾うのでリスト管理は不要
        foreach (var f in root.GetComponentsInChildren<Fracture>(false)) {
            var decay = f.GetComponent<FragmentDecay>();
            if (decay != null && decay.isFragment) continue; // 破片は対象にしない
            if (IsExcluded(f.transform)) continue;
            if (!seen.Add(f.gameObject)) continue;

            if (templateFracture == null) { templateFracture = f; templateDecay = decay; }
            list.Add(new Target { go = f.gameObject, fracture = f, delay = DelayFor(f.transform, epicenter) });
        }

        if (includeDeformables) {
            foreach (var d in root.GetComponentsInChildren<Deformable>(false)) {
                var go = d.gameObject;
                if (IsExcluded(go.transform)) continue;
                if (!seen.Add(go)) continue; // Fractureも付いてる物は上で拾い済み
                if (!CanBeFractured(go)) continue;
                list.Add(new Target { go = go, fracture = null, delay = DelayFor(go.transform, epicenter) });
            }
        }

        list.Sort((a, b) => a.delay.CompareTo(b.delay));
        return list;
    }

    float DelayFor(Transform t, Vector3 epicenter) {
        return Vector3.Distance(t.position, epicenter) / Mathf.Max(0.01f, rippleSpeed);
    }

    // Fractureが動くのに最低限必要な構成(メッシュ・レンダラー・コライダー)
    static bool CanBeFractured(GameObject go) {
        var mf = go.GetComponent<MeshFilter>();
        return mf != null && mf.sharedMesh != null
            && go.GetComponent<MeshRenderer>() != null
            && go.GetComponent<Collider>() != null;
    }

    bool IsExcluded(Transform t) {
        if (excludeRoots == null) return false;
        foreach (var ex in excludeRoots) {
            if (ex != null && t.IsChildOf(ex)) return true;
        }
        return false;
    }

    IEnumerator DemolishRoutine(Transform root, Vector3 epicenter, List<Target> targets) {
        IsDemolishing = true;
        onStarted?.Invoke();

        // 既に転がってる破片はこれから壊す物とは無関係なので、押し出し対象から外しておく
        var pushed = new HashSet<Rigidbody>();
        foreach (var d in FindObjectsByType<FragmentDecay>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) {
            if (!d.isFragment) continue;
            var rb0 = d.GetComponent<Rigidbody>();
            if (rb0 != null) pushed.Add(rb0);
        }

        GameFeel.Shake(shakeMagnitude, shakeDuration);
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySEAtPosition(SE.temp, epicenter);

        float elapsed = 0f;
        float nextSweep = 0f;
        float nextShake = rollingShakeInterval;
        float lastSe = 0f;
        int next = 0;

        // 全部壊し終わったあとも、非同期で遅れて出てくる破片を押し出すために少し余韻を残す
        const float tail = 2.5f;
        float endTime = float.MaxValue;

        while (elapsed < endTime) {
            elapsed += Time.unscaledDeltaTime; // ヒットストップ等のtimeScaleに左右されない

            int brokenThisFrame = 0;
            Vector3 lastPos = epicenter;
            while (next < targets.Count && targets[next].delay <= elapsed && brokenThisFrame < maxBreaksPerFrame) {
                var t = targets[next++];
                if (BreakOne(t)) {
                    brokenThisFrame++;
                    lastPos = t.go.transform.position;
                }
            }

            if (brokenThisFrame > 0 && elapsed - lastSe >= 0.08f && AudioManager.Instance != null) {
                lastSe = elapsed;
                AudioManager.Instance.PlaySEAtPosition(SE.temp, lastPos);
            }

            if (rollingShakeInterval > 0f && next < targets.Count && elapsed >= nextShake) {
                nextShake = elapsed + rollingShakeInterval;
                GameFeel.Shake(shakeMagnitude * 0.5f, rollingShakeInterval * 1.2f);
            }

            if (elapsed >= nextSweep) {
                nextSweep = elapsed + 0.1f;
                PushNewFragments(root, epicenter, pushed);
            }

            if (next >= targets.Count && endTime == float.MaxValue) endTime = elapsed + tail;
            yield return null;
        }

        IsDemolishing = false;
        onFinished?.Invoke();
    }

    bool BreakOne(Target t) {
        // 待ってる間に別の要因(パンチ等)で壊れた/消えた物はスキップ
        if (t.go == null || !t.go.activeInHierarchy) return false;

        var f = t.fracture != null ? t.fracture : EnsureFracture(t.go);
        if (f == null) return false;

        // Fractureは破片ルートを「{名前}Fragments」という名前で作り、FragmentDecayもその名前で
        // GameObject.Find して破片を探す。Door_Elevator_Lのように同名の物が複数あると別の物の破片ルートに
        // 当たってしまい、破片が取りこぼされる(消えない・押し出せない)ので、壊す直前にユニーク名へ改名する。
        // どのみちこの後すぐ非アクティブになる元オブジェクトなので、名前が変わっても実害は無い。
        f.gameObject.name = f.gameObject.name + "_d" + (++uniqueCounter);

        var rb = f.GetComponent<Rigidbody>();
        if (rb != null && rb.isKinematic) rb.isKinematic = false;
        f.ComputeFracture();

        if (ikdPerObject != 0 && IKDManager.Instance != null) IKDManager.Instance.Add(ikdPerObject);
        if (registerDestruction && RouteTracker.Instance != null) RouteTracker.Instance.RegisterDestruction();
        return true;
    }

    // へこむだけの物に、壊せる構成(Fracture+FragmentDecay)を壊す瞬間に足す。
    // 破片の数・中身のマテリアル等は、普通のFracture(手本)に揃えて見た目を統一する。
    Fracture EnsureFracture(GameObject go) {
        var existing = go.GetComponent<Fracture>();
        if (existing != null) return existing;
        if (!CanBeFractured(go)) return null;

        var tf = templateFracture;
        var mr = go.GetComponent<MeshRenderer>();

        var f = go.AddComponent<Fracture>(); // RequireComponentでRigidbodyも自動で付く
        f.triggerOptions = new TriggerOptions();
        f.fractureOptions = new FractureOptions {
            fragmentCount = tf != null ? tf.fractureOptions.fragmentCount : 5,
            xAxis = tf != null ? tf.fractureOptions.xAxis : true,
            yAxis = tf != null ? tf.fractureOptions.yAxis : true,
            zAxis = tf != null ? tf.fractureOptions.zAxis : true,
            detectFloatingFragments = false,
            asynchronous = true, // 破片へのFragmentDecay付与がonCompleted(非同期完了)前提のため必ず非同期
            insideMaterial = tf != null && tf.fractureOptions.insideMaterial != null
                ? tf.fractureOptions.insideMaterial : mr.sharedMaterial,
            textureScale = tf != null ? tf.fractureOptions.textureScale : Vector2.one,
            textureOffset = tf != null ? tf.fractureOptions.textureOffset : Vector2.zero,
        };
        f.refractureOptions = new RefractureOptions {
            enableRefracturing = tf != null && tf.refractureOptions.enableRefracturing,
            maxRefractureCount = tf != null ? tf.refractureOptions.maxRefractureCount : 1,
            invokeCallbacks = tf != null && tf.refractureOptions.invokeCallbacks,
        };
        // FragmentDecayがonCompletedへ登録するので、UnityEventは必ず作っておく
        f.callbackOptions = new CallbackOptions {
            onFracture = new UnityEvent<Collider, GameObject, Vector3>(),
            onCompleted = new UnityEvent(),
        };

        if (go.GetComponent<FragmentDecay>() == null) {
            var decay = go.AddComponent<FragmentDecay>();
            if (templateDecay != null) {
                decay.lifetime = templateDecay.lifetime;
                decay.shrinkDuration = templateDecay.shrinkDuration;
                decay.destroyThreshold = templateDecay.destroyThreshold;
                decay.maxGeneration = templateDecay.maxGeneration;
            }
        }
        return f;
    }

    // 破片は Fracture の非同期処理が終わってから遅れて現れ、FragmentDecayが付くのもその後。
    // 「新しく現れた破片(FragmentDecay付きで未押し出し)」を定期的に拾って外向きに押し出す。
    void PushNewFragments(Transform root, Vector3 epicenter, HashSet<Rigidbody> pushed) {
        foreach (var d in FindObjectsByType<FragmentDecay>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) {
            if (!d.isFragment) continue;
            if (!d.transform.IsChildOf(root)) continue; // 建物の外(街路樹の破片等)は押さない
            var rb = d.GetComponent<Rigidbody>();
            if (rb == null || rb.isKinematic) continue;
            if (!pushed.Add(rb)) continue;

            Vector3 dir = rb.worldCenterOfMass - epicenter;
            if (dir.sqrMagnitude < 0.01f) dir = Random.onUnitSphere;
            dir = (dir.normalized + Vector3.up * upwardBias).normalized;

            rb.AddForce(dir * blastSpeed * Random.Range(0.6f, 1.3f), ForceMode.VelocityChange);
            rb.AddTorque(Random.insideUnitSphere * spinSpeed, ForceMode.VelocityChange);
        }
    }
}
