// エレベーターの行き先階ボタン
using UnityEngine;

public class ElevatorButton : MonoBehaviour, IInteractable {
    public ElevatorController elevator;
    [Tooltip("何階？ 0=1F, 1=2F, 2=3F")]
    public int floorIndex;

    public void Interact() {
        if (elevator == null) return;
        if (!elevator.IsIdle) {
            Debug.Log("[Elevator] Busy");
            return;
        }
        Debug.Log($"[Elevator] Button pressed: {floorIndex + 1}F");
        elevator.RequestFloor(floorIndex);
    }
}
