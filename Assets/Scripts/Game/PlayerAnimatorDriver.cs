// PlayerのAnimatorに「動いているか」を渡して、待機(Idle)⇄歩き(Walk)を切り替える。
// Playerのルート(CharacterController付き)にアタッチする。モデル側のAnimatorは子から自動で探す。
//
// 前提: モデルのAnimatorはHumanoidアバター(model_man_CharacterAvatar)と player_anime コントローラ。
// アニメ素材はどちらもHumanoid(CopyFromOther)でインポートしたもの。Generic(パス指定)のクリップは
// Humanoidアバターのアニメーターでは動かないので、歩きは man_Walk_01_humanoid.fbx を使う。
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerAnimatorDriver : MonoBehaviour {
    [Tooltip("未設定なら子から自動で探す")]
    public Animator animator;

    [Header("歩き判定(水平速度 m/s)")]
    [Tooltip("これを超えたら歩き扱いにする")]
    public float moveThreshold = 0.3f;
    [Tooltip("歩き中、これを下回ったら待機に戻す(ちらつき防止のヒステリシス)")]
    public float idleThreshold = 0.15f;

    [Header("歩きアニメの速度合わせ")]
    [Tooltip("この速度で歩きアニメが等倍になる。未設定(0)ならPlayerController.moveSpeedを使う")]
    public float referenceSpeed = 0f;
    [Tooltip("ダッシュ等で速くなった時に、歩きアニメを速めすぎ/遅めすぎしない範囲")]
    public Vector2 walkAnimSpeedRange = new Vector2(0.6f, 1.8f);

    static readonly int IsMovingId = Animator.StringToHash("IsMoving");

    CharacterController cc;
    bool moving;

    void Awake() {
        cc = GetComponent<CharacterController>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (referenceSpeed <= 0f) {
            var pc = GetComponent<PlayerController>();
            referenceSpeed = pc != null ? pc.moveSpeed : 5f;
        }
    }

    void Update() {
        if (animator == null) return;

        Vector3 v = cc.velocity;
        v.y = 0f; // 落下・ジャンプは歩き判定に含めない
        float speed = v.magnitude;

        moving = moving ? speed > idleThreshold : speed > moveThreshold;
        animator.SetBool(IsMovingId, moving);
        animator.speed = moving
            ? Mathf.Clamp(speed / Mathf.Max(0.01f, referenceSpeed), walkAnimSpeedRange.x, walkAnimSpeedRange.y)
            : 1f;
    }
}
