using UnityEngine;

public class HitDebug : MonoBehaviour {
    void OnCollisionEnter(Collision c) {
        Debug.Log($"Hit by {c.gameObject.name}, impulse={c.impulse.magnitude}");
    }
}
