using UnityEngine;

public class FragmentDecay : MonoBehaviour {
    [Header("寿命設定")]
    public float lifetime = 10f;
    public float shrinkDuration = 2f;
    public float destroyThreshold = 0.01f;

    [Header("世代設定")]
    [Tooltip("何世代まで自動処理するか（0=元オブジェクトのみ, 1=破片まで, 2=孫破片まで, ...）")]
    public int maxGeneration = 3;

    [HideInInspector] public bool isFragment = false;
    [HideInInspector] public int generation = 0;   // 0=元, 1=破片, 2=孫破片...

    Vector3 initialScale;
    float spawnTime;
    bool shrinking = false;
    bool started = false;

    void Start() {
        // Fractureがあれば onCompleted に登録（元/破片問わず）
        var fracture = GetComponent<Fracture>();
        if (fracture != null && generation < maxGeneration) {
            fracture.callbackOptions.onCompleted.AddListener(OnFractureCompleted);
            Debug.Log($"[FragmentDecay] {name}: Registered (gen={generation})");
        }

        if (isFragment) {
            initialScale = transform.localScale;
            spawnTime = Time.time;
            started = true;
        }
    }

    void Update() {
        if (!isFragment || !started) return;

        float elapsed = Time.time - spawnTime;

        if (!shrinking && elapsed >= lifetime) {
            shrinking = true;
            var rb = GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;
        }

        if (shrinking) {
            float shrinkElapsed = elapsed - lifetime;
            float t = Mathf.Clamp01(shrinkElapsed / shrinkDuration);
            transform.localScale = Vector3.Lerp(initialScale, initialScale * destroyThreshold, t);

            if (t >= 1f) {
                Destroy(gameObject);
            }
        }
    }

    void OnFractureCompleted() {
        string fragmentRootName = $"{gameObject.name}Fragments";
        var root = GameObject.Find(fragmentRootName);
        if (root == null) {
            Debug.LogWarning($"[FragmentDecay] Fragment root not found: {fragmentRootName}");
            return;
        }

        // 自分のHighlight設定を取得（コピー元）
        var myHighlight = GetComponent<InteractableHighlight>();

        int count = 0;
        foreach (Transform child in root.transform) {
            if (child.GetComponent<FragmentDecay>() != null) continue;

            // FragmentDecay追加
            var decay = child.gameObject.AddComponent<FragmentDecay>();
            decay.lifetime = this.lifetime;
            decay.shrinkDuration = this.shrinkDuration;
            decay.destroyThreshold = this.destroyThreshold;
            decay.maxGeneration = this.maxGeneration;
            decay.isFragment = true;
            decay.generation = this.generation + 1;

            // まだ再破壊できる破片なら InteractableHighlight 追加
            if (child.GetComponent<Fracture>() != null
                && child.GetComponent<InteractableHighlight>() == null) {
                var newHighlight = child.gameObject.AddComponent<InteractableHighlight>();

                // 設定値をコピー
                if (myHighlight != null) {
                    newHighlight.highlightColor = myHighlight.highlightColor;
                    newHighlight.minIntensity = myHighlight.minIntensity;
                    newHighlight.maxIntensity = myHighlight.maxIntensity;
                    newHighlight.pulseSpeed = myHighlight.pulseSpeed;
                    newHighlight.fadeSpeed = myHighlight.fadeSpeed;
                }
            }

            count++;
        }
        Debug.Log($"[FragmentDecay] Gen{generation}→{generation + 1}: Attached to {count} fragments of {gameObject.name}");
    }
}
