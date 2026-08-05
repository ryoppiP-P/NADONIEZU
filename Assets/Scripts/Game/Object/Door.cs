using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(HingeJoint))]
public class Door : MonoBehaviour, IInteractable {
    [Header("Motor")]
    [Tooltip("開ける時の目標角度")]
    public float openAngle = 90f;
    [Tooltip("モーターの力")]
    public float motorForce = 50f;
    [Tooltip("モーターの速度")]
    public float motorSpeed = 200f;

    [Header("Auto Close")]
    public bool autoClose = false;
    public float autoCloseDelay = 3f;

    HingeJoint hinge;
    Rigidbody rb;
    Coroutine autoCloseCoroutine;
    bool isOpen = false;

    void Awake() {
        hinge = GetComponent<HingeJoint>();
        rb = GetComponent<Rigidbody>();

        // 初期状態：閉じ
        SetMotorTarget(0f);
    }

    public void Interact() {
        if (isOpen) CloseDoor();
        else OpenDoor();
    }

    void OpenDoor() {
        isOpen = true;
        SetMotorTarget(openAngle);

        if (autoCloseCoroutine != null) StopCoroutine(autoCloseCoroutine);
        if (autoClose) autoCloseCoroutine = StartCoroutine(AutoCloseTimer());
    }

    void CloseDoor() {
        isOpen = false;
        SetMotorTarget(0f);

        if (autoCloseCoroutine != null) StopCoroutine(autoCloseCoroutine);
    }

    void SetMotorTarget(float targetAngle) {
        // Spring方式で目標角度に引き寄せる
        hinge.useSpring = true;
        JointSpring spring = hinge.spring;
        spring.spring = motorForce;
        spring.damper = motorForce * 0.2f;
        spring.targetPosition = targetAngle;
        hinge.spring = spring;
    }

    IEnumerator AutoCloseTimer() {
        yield return new WaitForSeconds(autoCloseDelay);
        if (isOpen) CloseDoor();
    }

    // 破壊対応
    public void Break() {
        if (autoCloseCoroutine != null) StopCoroutine(autoCloseCoroutine);
        hinge.useSpring = false;   // モーター停止
        Destroy(hinge);            // ヒンジ破壊で扉が自由に
        this.enabled = false;
    }
}
