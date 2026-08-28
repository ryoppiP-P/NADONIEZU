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

    [System.Serializable]
    public struct RankTier {
        public int minIKD;
        public string label;
    }

    [Header("Rank")]
    [Tooltip("IKD累計に応じた呼称。minIKDの昇順で並べること（現在値以下で最も高いしきい値のlabelを採用する）")]
    [SerializeField] private RankTier[] rankTiers = new RankTier[] {
        new RankTier { minIKD = 0,    label = "D　平穏無事" },
        new RankTier { minIKD = 50,   label = "C　多少のいざこざ" },
        new RankTier { minIKD = 150,  label = "B　問題社員" },
        new RankTier { minIKD = 350,  label = "A　要注意人物" },
        new RankTier { minIKD = 650,  label = "S　狂人認定" },
        new RankTier { minIKD = 1000, label = "SS　焦土の帝王" },
    };

    // 現在のIKDに対応する呼称
    public string CurrentRankLabel => GetRankLabel(currentIKD);

    public string GetRankLabel(int ikd) {
        string label = rankTiers.Length > 0 ? rankTiers[0].label : "";
        foreach (var tier in rankTiers) {
            if (ikd >= tier.minIKD) label = tier.label;
            else break; // minIKD昇順が前提なので、ここで超えなくなったら以降は見なくてよい
        }
        return label;
    }
}
