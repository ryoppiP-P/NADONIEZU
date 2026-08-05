using UnityEngine;

public class InteractableHighlight : MonoBehaviour {
    [Header("発光色")]
    [ColorUsage(true, true)]
    public Color highlightColor = new Color(1f, 0.95f, 0.6f);

    [Header("点滅設定")]
    public float minIntensity = 1.5f;
    public float maxIntensity = 4f;
    public float pulseSpeed = 1.5f;

    [Header("フェード")]
    [Tooltip("ON/OFF切り替え時の補間速度")]
    public float fadeSpeed = 8f;

    Renderer[] renderers;
    MaterialPropertyBlock propBlock;
    bool isHighlighted = false;
    float currentBlend = 0f;   // 0=OFF, 1=ON への滑らか遷移用

    static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");

    void Awake() {
        renderers = GetComponentsInChildren<Renderer>();
        propBlock = new MaterialPropertyBlock();

        foreach (var r in renderers) {
            foreach (var mat in r.materials) {
                mat.EnableKeyword("_EMISSION");
            }
        }
    }

    void Start() {
        SetEmission(Color.black);
    }

    void Update() {
        // ハイライト状態への補間
        float target = isHighlighted ? 1f : 0f;
        currentBlend = Mathf.MoveTowards(currentBlend, target, fadeSpeed * Time.deltaTime);

        if (currentBlend <= 0f) {
            SetEmission(Color.black);
            return;
        }

        // Sin波で点滅
        float t = (Mathf.Sin(Time.time * Mathf.PI * 2f / pulseSpeed) + 1f) * 0.5f;
        float intensity = Mathf.Lerp(minIntensity, maxIntensity, t);

        // フェード倍率を掛ける
        SetEmission(highlightColor * intensity * currentBlend);
    }

    /// <summary>
    /// 外部（PlayerActions）から呼ばれる。カーソルが合った/離れた時に切り替える。
    /// </summary>
    public void SetHighlight(bool on) {
        isHighlighted = on;
    }

    void SetEmission(Color color) {
        if (renderers == null) return;
        foreach (var r in renderers) {
            r.GetPropertyBlock(propBlock);
            propBlock.SetColor(EmissionColorID, color);
            r.SetPropertyBlock(propBlock);
        }
    }

    void OnDisable() {
        if (renderers != null) SetEmission(Color.black);
        isHighlighted = false;
        currentBlend = 0f;
    }
}
