// 壊れるオブジェクトの親空オブジェクトに付ける。
// 破片は子オブジェクトとして配置しておく。子の破片のRigidbodyはKinematicにしておく。
// 破片のRigidbodyは、壊れるときにKinematicを解除して物理挙動させる。
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class BreakableObject : MonoBehaviour {
    [Header("Break Settings")]
    [Tooltip("この力以上の衝撃で壊れる")]
    public float breakThreshold = 10f;

    [Tooltip("破片に加える力の最小値")]
    public float minBurstForce = 1f;

    [Tooltip("破片に加える力の最大値")]
    public float maxBurstForce = 4f;

    [Tooltip("破片に加える回転トルクの最大値")]
    public float maxBurstTorque = 3f;

    [Header("IKD")]
    public int ikdGain = 10;

    private Rigidbody mainRb;          // 親のRigidbody（固まり状態用）
    private Rigidbody[] pieceRbs;      // 子の破片
    private bool isBroken = false;

    void Awake() {
        mainRb = GetComponent<Rigidbody>();

        // 子の破片を集める（自分自身は除外）
        var allRbs = GetComponentsInChildren<Rigidbody>();
        pieceRbs = System.Array.FindAll(allRbs, r => r != mainRb);

        // 親Colliderを自動生成（無ければ）
        if (GetComponent<Collider>() == null)
            CreateBoundingCollider();

        // 破片はKinematicで親に追従
        foreach (var rb in pieceRbs) {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.None;  // 補間OFF

            // 固まり状態では子Colliderを無効化
            foreach (var col in rb.GetComponents<Collider>())
                col.enabled = false;

            // 衝突検知用コンポーネント
            var detector = rb.gameObject.AddComponent<BreakablePiece>();
            detector.parent = this;
        }
        // 親の重心を中心に固定（子Rigidbodyの影響を排除）
        mainRb.ResetCenterOfMass();
        mainRb.ResetInertiaTensor();
    }

    private void Update() {
        if (Keyboard.current.iKey.wasPressedThisFrame) {
            Break(transform.position);
        }
    }

    public void NotifyCollision(Collision collision) {
        if (isBroken) return;

        if (collision.relativeVelocity.magnitude >= breakThreshold)
            Break(collision.contacts[0].point);
    }

    // 親自身に何かがぶつかった時（こっちでも検知）
    void OnCollisionEnter(Collision collision) {
        if (isBroken) return;

        if (collision.relativeVelocity.magnitude >= breakThreshold)
            Break(collision.contacts[0].point);
    }

    void Break(Vector3 impactPoint) {
        isBroken = true;

        // 親のRigidbodyを無効化（破片に物理を委ねる）
        mainRb.isKinematic = true;
        mainRb.detectCollisions = false;

        foreach (var rb in pieceRbs) {
            // 子Colliderを復活
            foreach (var col in rb.GetComponents<Collider>())
                col.enabled = true;

            // 個別の物理オブジェクトに変身
            rb.isKinematic = false;
            rb.useGravity = true;

            // ランダム方向の力
            Vector3 dir = (rb.position - impactPoint).normalized
                          + Random.insideUnitSphere * 0.5f;
            float force = Random.Range(minBurstForce, maxBurstForce);
            rb.AddForce(dir * force, ForceMode.Impulse);

            // ランダムなトルク
            rb.AddTorque(Random.insideUnitSphere * maxBurstTorque, ForceMode.Impulse);
        }

        if (IKDManager.Instance != null)
            IKDManager.Instance.Add(ikdGain);
    }

    void CreateBoundingCollider() {
        // 子のRendererから全体のBoundsを計算
        var renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        // BoxColliderを追加してサイズと中心をセット
        var box = gameObject.AddComponent<BoxCollider>();

        // ワールド座標 → ローカル座標に変換
        box.center = transform.InverseTransformPoint(bounds.center);
        box.size = transform.InverseTransformVector(bounds.size);

        // サイズが負になる場合に備えて絶対値
        box.size = new Vector3(
            Mathf.Abs(box.size.x),
            Mathf.Abs(box.size.y),
            Mathf.Abs(box.size.z)
        );
    }
}