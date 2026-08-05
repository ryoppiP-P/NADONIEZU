// エレベーター乗降用
using UnityEngine;

public class ElevatorRider : MonoBehaviour {
    [Tooltip("プレイヤーの親にするCage Transform")]
    public Transform cage;

    void Reset() {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other) {
        if (other.CompareTag("Player")) {
            other.transform.SetParent(cage);
            Debug.Log("[Elevator] Player entered");
        }
    }

    void OnTriggerExit(Collider other) {
        if (other.CompareTag("Player")) {
            other.transform.SetParent(null);
            Debug.Log("[Elevator] Player exited");
        }
    }
}
