// 各階からのエレベーター呼び出しボタン
using UnityEngine;

public class ElevatorCallButton : MonoBehaviour, IInteractable {
    public ElevatorController elevator;
    [Tooltip("このボタンが設置されてる階 0=1F, 1=2F, 2=3F")]
    public int floorIndex;

    public void Interact() {
        if (elevator == null) return;
        if (!elevator.IsIdle) {
            Debug.Log("[Elevator] Busy");
            return;
        }
        if (elevator.CurrentFloor == floorIndex) {
            Debug.Log("[Elevator] Already here");
            return;
        }
        Debug.Log($"[Elevator] Called to {floorIndex + 1}F");
        elevator.RequestFloor(floorIndex);
    }
}
