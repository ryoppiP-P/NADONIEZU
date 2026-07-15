// 壊れるオブジェクトの破片に付ける。
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BreakablePiece : MonoBehaviour {
    [HideInInspector] public BreakableObject parent;

    void OnCollisionEnter(Collision collision) {
        if (parent != null)
            parent.NotifyCollision(collision);
    }
}