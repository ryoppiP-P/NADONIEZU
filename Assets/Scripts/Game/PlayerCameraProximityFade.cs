// カメラが壁などの衝突回避でプレイヤーに近づいた時、プレイヤーモデルを半透明にして視界を確保する。
using UnityEngine;

public class PlayerCameraProximityFade : MonoBehaviour {
    public CameraFollow cameraFollow;

    [Tooltip("フェード対象のRenderer（プレイヤーの見た目メッシュのみ。TrajectoryLine等は含めない）")]
    public Renderer[] renderers;

    [Tooltip("この距離からフェードを開始する")]
    public float fadeStartDistance = 1.5f;
    [Tooltip("この距離で最も薄くなる")]
    public float fadeEndDistance = 0.6f;
    [Tooltip("最も近づいた時の最低アルファ（0で完全透明）")]
    [Range(0f, 1f)] public float minAlpha = 0.15f;
    [Tooltip("アルファが変化する速さ")]
    public float fadeSpeed = 8f;

    static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");

    Material[] instancedMaterials;
    float[] baseAlphas;
    float currentAlpha = 1f;

    void Awake() {
        if (cameraFollow == null && Camera.main != null)
            cameraFollow = Camera.main.GetComponent<CameraFollow>();

        var mats = new System.Collections.Generic.List<Material>();
        var alphas = new System.Collections.Generic.List<float>();

        if (renderers != null) {
            foreach (var r in renderers) {
                if (r == null) continue;
                foreach (var mat in r.materials) { // .materialsアクセスで自動的にインスタンス化される
                    MakeTransparent(mat);
                    mats.Add(mat);
                    alphas.Add(mat.HasProperty(BaseColorID) ? mat.GetColor(BaseColorID).a : 1f);
                }
            }
        }

        instancedMaterials = mats.ToArray();
        baseAlphas = alphas.ToArray();
    }

    // URP Lit/Simple Lit系のマテリアルをTransparentサーフェスに切り替える
    // （Unityのマテリアルインスペクタで"Surface Type"をTransparentにした時と同じ設定を再現）
    void MakeTransparent(Material mat) {
        if (!mat.HasProperty("_Surface")) return;

        mat.SetFloat("_Surface", 1f); // 0=Opaque, 1=Transparent
        mat.SetFloat("_Blend", 0f);   // 0=Alpha
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    void LateUpdate() {
        if (cameraFollow == null || instancedMaterials == null) return;

        float d = cameraFollow.CurrentDistance;
        float t = Mathf.InverseLerp(fadeStartDistance, fadeEndDistance, d);
        float targetAlpha = Mathf.Lerp(1f, minAlpha, t);

        currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, fadeSpeed * Time.deltaTime);

        for (int i = 0; i < instancedMaterials.Length; i++) {
            var mat = instancedMaterials[i];
            if (mat == null || !mat.HasProperty(BaseColorID)) continue;
            Color c = mat.GetColor(BaseColorID);
            c.a = baseAlphas[i] * currentAlpha;
            mat.SetColor(BaseColorID, c);
        }
    }
}
