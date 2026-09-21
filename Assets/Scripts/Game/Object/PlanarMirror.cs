// 平面鏡（本物のプラナー反射）。
//
// 鏡の物理: 入射角＝反射角。視点から鏡面上の点へ向かう視線は、鏡面の法線に対して対称な向きへ跳ね返り、
// その先にある物が映る。これは「視点を鏡面で鏡映しした点(虚像側の視点)から、鏡を窓にして向こうを覗いた絵」と
// 等価なので、次の3手で再現する。
//   1) 視点カメラ(ゲームプレイ用カメラ)を鏡面で鏡映しした位置・向きに反射カメラを置く
//   2) 鏡面ちょうどを反射カメラのニアクリップ面にして(斜め投影)、鏡の裏側の壁などを映さない
//   3) その映像を「視点カメラから見た鏡面のスクリーン座標」でそのまま貼る(シェーダー側)
// 映る対象(プレイヤー含む世界全体)も、見る角度による見え方の変化(視差)も、幾何学どおりに決まる。
// 肩越しオフセットのカメラも特別扱い不要で、「実際のカメラ位置と向き」をそのまま鏡映しするだけで正しい。
//
// 過去の失敗(同じ罠を踏まないためのメモ):
//  - 反射カメラをLookRotationで作ると右手系の普通のカメラになり、本物の鏡像(左右が反転した座標系)とは
//    左右が逆の絵になる。だからサンプリング時にUを反転する(シェーダーの_FlipU)。これを忘れると、
//    画面中心から外れた物ほど左右逆の位置に映ってズレて見える(肩越しカメラでプレイヤーが画面の端寄りに
//    映る構図で「位置がおかしい」と見えた真因)。
//  - 普通のカメラなのでGL.invertCullingは使わない。使うと裏面が描かれ、法線が逆を向いて真っ暗に沈む。
//  - 鏡の表がどちら向きかは「視点が居る側」で毎フレーム決める。置き場所や回転ごとの手動調整は不要。
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(MeshRenderer))]
public class PlanarMirror : MonoBehaviour {
    [Header("映すカメラ")]
    [Tooltip("未設定なら実行時に Camera.main を使う")]
    public Camera targetCamera;

    [Header("品質 / パフォーマンス")]
    [Tooltip("視点カメラが鏡からこの距離より遠い間は反射レンダリングを丸ごとスキップする(コスト0)")]
    public float activationDistance = 8f;
    [Tooltip("反射テクスチャの横幅(px)。高さは視点カメラのアスペクト比から決まる。低いほど軽い")]
    public int textureSize = 768;
    [Tooltip("反射カメラが描画するレイヤー。UI等の不要なレイヤーは外しておく")]
    public LayerMask cullingMask = ~0;
    [Tooltip("反射にも影を落とす。オフにすると軽いが、本物の部屋よりのっぺりして見える")]
    public bool renderShadows = true;
    [Tooltip("鏡面ぎりぎりのちらつきを防ぐための斜めクリップの余白(m)")]
    public float clipPlaneOffset = 0.01f;

    Camera reflectionCamera;
    RenderTexture reflectionTexture;
    Material instanceMaterial;
    MeshRenderer meshRenderer;
    Vector3 normalAxisLocal; // 鏡の厚み方向(メッシュのbounds.sizeが最小の軸)=法線の軸
    int lastTexW = -1, lastTexH = -1;

    static readonly int ReflectionTexId = Shader.PropertyToID("_ReflectionTex");

    void Awake() {
        meshRenderer = GetComponent<MeshRenderer>();
        instanceMaterial = meshRenderer.material; // sharedMaterialを汚さないようインスタンス化

        // 板状メッシュの「一番薄い軸」が鏡面の法線。モデルごとにforwardとは限らないのでbounds.sizeから決める
        Vector3 s = GetComponent<MeshFilter>().sharedMesh.bounds.size;
        if (s.x <= s.y && s.x <= s.z) normalAxisLocal = Vector3.right;
        else if (s.y <= s.z) normalAxisLocal = Vector3.up;
        else normalAxisLocal = Vector3.forward;

        var camGO = new GameObject(name + "_ReflectionCamera");
        camGO.hideFlags = HideFlags.HideAndDontSave;
        camGO.transform.SetParent(transform, false);

        reflectionCamera = camGO.AddComponent<Camera>();
        reflectionCamera.enabled = false; // RenderSingleCameraで手動描画するので自動描画はさせない
        reflectionCamera.useOcclusionCulling = false;

        var camData = camGO.AddComponent<UniversalAdditionalCameraData>();
        camData.renderShadows = renderShadows;
        camData.renderPostProcessing = false;
        camData.requiresColorOption = CameraOverrideOption.Off;
        camData.requiresDepthOption = CameraOverrideOption.Off;
    }

    void OnEnable() {
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
    }

    void OnDisable() {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
    }

    void OnDestroy() {
        if (reflectionTexture != null) {
            reflectionTexture.Release();
            Destroy(reflectionTexture);
        }
        if (instanceMaterial != null) Destroy(instanceMaterial);
    }

    void EnsureTexture(Camera src) {
        int w = Mathf.Max(64, textureSize);
        int h = Mathf.Max(64, Mathf.RoundToInt(w / Mathf.Max(0.01f, src.aspect)));
        if (reflectionTexture != null && (w != lastTexW || h != lastTexH)) {
            reflectionCamera.targetTexture = null;
            reflectionTexture.Release();
            Destroy(reflectionTexture);
            reflectionTexture = null;
        }
        if (reflectionTexture == null) {
            reflectionTexture = new RenderTexture(w, h, 16, RenderTextureFormat.Default);
            reflectionTexture.name = name + "_ReflectionRT";
            reflectionCamera.targetTexture = reflectionTexture;
            lastTexW = w; lastTexH = h;
        }
        instanceMaterial.SetTexture(ReflectionTexId, reflectionTexture);
    }

    /// <summary>
    /// 視点カメラ cam を鏡面で鏡映しして、反射カメラの位置・向き・投影を決める。
    /// 鏡の表(法線の向き)は「視点が居る側」。鏡の後ろから見ている場合は何もせず false を返す。
    /// </summary>
    bool SetupReflectionCamera(Camera cam) {
        Vector3 n0 = transform.TransformDirection(normalAxisLocal).normalized;
        Bounds b = meshRenderer.bounds;
        Vector3 toCam = cam.transform.position - b.center;
        float side = Vector3.Dot(toCam, n0);
        if (Mathf.Abs(side) < 1e-4f) return false;
        Vector3 n = side > 0f ? n0 : -n0; // 視点が居る側=鏡の表

        // 鏡面(表側の面)上の点。板の厚みの半分だけ中心から表へ寄せる
        Vector3 e = b.extents;
        float halfThickness = Mathf.Abs(n.x) * e.x + Mathf.Abs(n.y) * e.y + Mathf.Abs(n.z) * e.z;
        Vector3 planePos = b.center + n * halfThickness;

        // 入射角＝反射角: 位置は鏡面に対して点対称に、向きは法線に対して反射させる
        Vector3 camPos = cam.transform.position;
        Vector3 reflectedPos = camPos - 2f * Vector3.Dot(camPos - planePos, n) * n;
        Vector3 reflectedFwd = Vector3.Reflect(cam.transform.forward, n);
        Vector3 reflectedUp = Vector3.Reflect(cam.transform.up, n);
        reflectionCamera.transform.SetPositionAndRotation(reflectedPos, Quaternion.LookRotation(reflectedFwd, reflectedUp));

        reflectionCamera.nearClipPlane = cam.nearClipPlane;
        reflectionCamera.farClipPlane = cam.farClipPlane;
        reflectionCamera.cullingMask = cullingMask;
        reflectionCamera.clearFlags = cam.clearFlags == CameraClearFlags.Skybox ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
        reflectionCamera.backgroundColor = cam.backgroundColor;

        // 鏡面ちょうどを反射カメラのニアクリップ面にする斜め投影。
        // 反射カメラは鏡の裏側にいるので、これが無いと鏡の裏の壁が反射像を塞ぐ。
        // 平面は「残したい側(=視点側=n側)」が正になるよう法線nのまま渡す。
        Vector4 clipPlane = CameraSpacePlane(reflectionCamera, planePos, n, clipPlaneOffset);
        reflectionCamera.projectionMatrix = cam.CalculateObliqueMatrix(clipPlane);
        return true;
    }

    void OnBeginCameraRendering(ScriptableRenderContext context, Camera cam) {
        if (!isActiveAndEnabled) return;
        if (cam == reflectionCamera) return; // 自分自身の再帰防止

        Camera cameraToMirror = targetCamera != null ? targetCamera : Camera.main;
        if (cameraToMirror == null || cam != cameraToMirror) return;

        if (Vector3.Distance(cam.transform.position, meshRenderer.bounds.center) > activationDistance) return;

        EnsureTexture(cam);
        if (!SetupReflectionCamera(cam)) return;

#pragma warning disable CS0618 // RenderSingleCameraはobsoleteだが、beginCameraRendering内で追加カメラを描く現役の手段
        UniversalRenderPipeline.RenderSingleCamera(context, reflectionCamera);
#pragma warning restore CS0618
    }

    static Vector4 CameraSpacePlane(Camera cam, Vector3 pos, Vector3 normal, float offset) {
        Vector3 offsetPos = pos + normal * offset;
        Matrix4x4 m = cam.worldToCameraMatrix;
        Vector3 cameraPos = m.MultiplyPoint(offsetPos);
        Vector3 cameraNormal = m.MultiplyVector(normal).normalized;
        return new Vector4(cameraNormal.x, cameraNormal.y, cameraNormal.z, -Vector3.Dot(cameraPos, cameraNormal));
    }
}
