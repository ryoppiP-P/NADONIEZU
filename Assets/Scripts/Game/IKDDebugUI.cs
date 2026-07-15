#if UNITY_EDITOR
using UnityEngine;
using TMPro;

public class IKDDebugUI : MonoBehaviour {
    [SerializeField] private TextMeshProUGUI label;

    void Start() {
        if (IKDManager.Instance != null) {
            IKDManager.Instance.OnIKDChanged += HandleIKDChanged;
            UpdateLabel(IKDManager.Instance.CurrentIKD);
        }
    }

    void OnDestroy() {
        if (IKDManager.Instance != null)
            IKDManager.Instance.OnIKDChanged -= HandleIKDChanged;
    }

    void HandleIKDChanged(int newValue, int diff) {
        UpdateLabel(newValue);
    }

    void UpdateLabel(int value) {
        if (label != null)
            label.text = $"IKD: {value}";
    }
}
#endif