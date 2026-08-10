using System;
using UnityEngine;

public class IKDManager : MonoBehaviour {
    public static IKDManager Instance { get; private set; }

    [Header("IKD Settings")]
    [SerializeField] private int currentIKD = 0;
    [SerializeField] private int minIKD = 0;
    [SerializeField] private int maxIKD = int.MaxValue;   // 上限なしにしたければ int.MaxValue

    // 現在のIKD値
    public int CurrentIKD => currentIKD;

    // IKDが変化した時に通知（新しい値, 変化量）
    public event Action<int, int> OnIKDChanged;

    void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        // DontDestroyOnLoad はルート(親なし)のGameObjectにしか効かない。
        // Managers配下の子オブジェクトのままだと警告だけ出て何も起きず、
        // シーン遷移のたびにIKDが失われてEndingに引き継がれないため、先にルート化する。
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
    }

    // IKDを増減（負の値もOK）
    public void Add(int amount) {
        int prev = currentIKD;
        currentIKD = Mathf.Clamp(currentIKD + amount, minIKD, maxIKD);
        int diff = currentIKD - prev;

        OnIKDChanged?.Invoke(currentIKD, diff);
    }

    // 強制的に値をセット
    public void Set(int value) {
        int prev = currentIKD;
        currentIKD = Mathf.Clamp(value, minIKD, maxIKD);
        OnIKDChanged?.Invoke(currentIKD, currentIKD - prev);
    }

    // リセット
    public void Reset() => Set(minIKD);
}
