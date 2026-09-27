// エレベーター制御
using System.Collections;
using UnityEngine;

public class ElevatorController : MonoBehaviour {
    public enum State { Idle, DoorClosing, Moving, DoorOpening }

    [Header("Cage & Stops")]
    public Transform cage;              // 動く箱
    public Transform[] floorStops;      // 各階の停止位置 (0=1F, 1=2F, 2=3F)

    [Header("Doors")]
    public Transform doorLeft;
    public Transform doorRight;
    public float doorSlideDistance = 0.8f;
    public float doorSpeed = 2f;

    [Header("Move")]
    public float moveSpeed = 2f;        // m/s
    public float waitBeforeClose = 0.5f;
    public float waitAfterArrive = 0.3f;

    [Header("Outer Doors (各階の安全扉)")]
    [Tooltip("各階の外扉ペア。indexは floorStops と対応")]
    public OuterDoorSet[] outerDoors;

    [System.Serializable]
    public class OuterDoorSet {
        public Transform doorLeft;
        public Transform doorRight;
        [HideInInspector] public Vector3 leftClosedPos;
        [HideInInspector] public Vector3 rightClosedPos;
    }

    [Header("State")]
    [SerializeField] int currentFloor = 0;
    [SerializeField] State state = State.Idle;

    [Tooltip("扉のスライド方向")]
    public Vector3 doorLeftOpenDir = Vector3.left;
    public Vector3 doorRightOpenDir = Vector3.right;

    Vector3 doorLeftClosedPos, doorRightClosedPos;
    AudioSource moveLoop; // moving ambience (looped while the cage moves)

    public bool IsIdle => state == State.Idle;
    public int CurrentFloor => currentFloor;

    void Awake() {
        if (doorLeft != null) doorLeftClosedPos = doorLeft.localPosition;
        if (doorRight != null) doorRightClosedPos = doorRight.localPosition;

        // 初期状態：扉オープン
        if (doorLeft != null)
            doorLeft.localPosition = doorLeftClosedPos + doorLeftOpenDir.normalized * doorSlideDistance;
        if (doorRight != null)
            doorRight.localPosition = doorRightClosedPos + doorRightOpenDir.normalized * doorSlideDistance;

        // 外扉の閉じ位置を記憶（Scene上には閉じた状態で配置）
        if (outerDoors != null) {
            foreach (var set in outerDoors) {
                if (set.doorLeft != null) set.leftClosedPos = set.doorLeft.localPosition;
                if (set.doorRight != null) set.rightClosedPos = set.doorRight.localPosition;
            }
        }
        // 起動時は現在階の外扉だけ開く
        OpenOuterDoorsInstant(currentFloor);
    }

    /// <summary>階選択（0=1F, 1=2F, 2=3F）</summary>
    public void RequestFloor(int floorIndex) {
        if (!IsIdle) return;
        if (floorIndex < 0 || floorIndex >= floorStops.Length) return;
        if (floorIndex == currentFloor) return;

        StartCoroutine(MoveSequence(floorIndex));
    }

    IEnumerator MoveSequence(int targetFloor) {
        // 1. 扉を閉める（内扉と現在階の外扉を並行）
        state = State.DoorClosing;
        yield return new WaitForSeconds(waitBeforeClose);
        StartCoroutine(CloseOuterDoors(currentFloor));
        yield return CloseDoors();

        // 2. 移動
        state = State.Moving;
        if (AudioManager.Instance != null) moveLoop = AudioManager.Instance.PlaySELoop(SE.AmbElevatorInside, cage, true);
        Vector3 targetPos = floorStops[targetFloor].position;
        while (Vector3.Distance(cage.position, targetPos) > 0.01f) {
            cage.position = Vector3.MoveTowards(cage.position, targetPos, moveSpeed * Time.deltaTime);
            yield return null;
        }
        cage.position = targetPos;
        currentFloor = targetFloor;
        if (AudioManager.Instance != null) {
            AudioManager.Instance.StopSELoop(moveLoop);
            AudioManager.Instance.PlaySEAtPosition(SE.ElevatorArrive, cage.position);
        }
        moveLoop = null;

        // 3. 到着後待機
        yield return new WaitForSeconds(waitAfterArrive);

        // 4. 扉を開ける（内扉と到着階の外扉を並行）
        state = State.DoorOpening;
        StartCoroutine(OpenOuterDoors(currentFloor));
        yield return OpenDoors();

        state = State.Idle;
        Debug.Log($"[Elevator] Arrived at floor {currentFloor + 1}F");
    }

    IEnumerator CloseDoors() {
        if (doorLeft == null || doorRight == null) yield break;

        Vector3 lStart = doorLeft.localPosition;
        Vector3 rStart = doorRight.localPosition;
        float t = 0f;
        while (t < 1f) {
            t += Time.deltaTime * doorSpeed;
            doorLeft.localPosition = Vector3.Lerp(lStart, doorLeftClosedPos, t);
            doorRight.localPosition = Vector3.Lerp(rStart, doorRightClosedPos, t);
            yield return null;
        }
    }

    IEnumerator OpenDoors() {
        if (doorLeft == null || doorRight == null) yield break;

        Vector3 lStart = doorLeft.localPosition;
        Vector3 rStart = doorRight.localPosition;
        Vector3 lTarget = doorLeftClosedPos + doorLeftOpenDir.normalized * doorSlideDistance;
        Vector3 rTarget = doorRightClosedPos + doorRightOpenDir.normalized * doorSlideDistance;
        float t = 0f;
        while (t < 1f) {
            t += Time.deltaTime * doorSpeed;
            doorLeft.localPosition = Vector3.Lerp(lStart, lTarget, t);
            doorRight.localPosition = Vector3.Lerp(rStart, rTarget, t);
            yield return null;
        }
    }

    void OpenOuterDoorsInstant(int floor) {
        if (outerDoors == null || floor < 0 || floor >= outerDoors.Length) return;
        var set = outerDoors[floor];
        if (set.doorLeft != null)
            set.doorLeft.localPosition = set.leftClosedPos + doorLeftOpenDir.normalized * doorSlideDistance;
        if (set.doorRight != null)
            set.doorRight.localPosition = set.rightClosedPos + doorRightOpenDir.normalized * doorSlideDistance;
    }

    IEnumerator CloseOuterDoors(int floor) {
        if (outerDoors == null || floor < 0 || floor >= outerDoors.Length) yield break;
        var set = outerDoors[floor];
        if (set.doorLeft == null || set.doorRight == null) yield break;

        Vector3 lStart = set.doorLeft.localPosition;
        Vector3 rStart = set.doorRight.localPosition;
        float t = 0f;
        while (t < 1f) {
            t += Time.deltaTime * doorSpeed;
            set.doorLeft.localPosition = Vector3.Lerp(lStart, set.leftClosedPos, t);
            set.doorRight.localPosition = Vector3.Lerp(rStart, set.rightClosedPos, t);
            yield return null;
        }
        set.doorLeft.localPosition = set.leftClosedPos;
        set.doorRight.localPosition = set.rightClosedPos;
    }

    IEnumerator OpenOuterDoors(int floor) {
        if (outerDoors == null || floor < 0 || floor >= outerDoors.Length) yield break;
        var set = outerDoors[floor];
        if (set.doorLeft == null || set.doorRight == null) yield break;

        Vector3 lStart = set.doorLeft.localPosition;
        Vector3 rStart = set.doorRight.localPosition;
        Vector3 lTarget = set.leftClosedPos + doorLeftOpenDir.normalized * doorSlideDistance;
        Vector3 rTarget = set.rightClosedPos + doorRightOpenDir.normalized * doorSlideDistance;
        float t = 0f;
        while (t < 1f) {
            t += Time.deltaTime * doorSpeed;
            set.doorLeft.localPosition = Vector3.Lerp(lStart, lTarget, t);
            set.doorRight.localPosition = Vector3.Lerp(rStart, rTarget, t);
            yield return null;
        }
        set.doorLeft.localPosition = lTarget;
        set.doorRight.localPosition = rTarget;
    }

}
