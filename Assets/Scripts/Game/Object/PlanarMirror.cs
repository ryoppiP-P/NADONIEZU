// 鏡（プレイヤーを映す簡易ミラー）。
//
// 最初はゲームプレイ用カメラをそのまま鏡映しする「本物のプラナー反射」で作っていたが、
// CameraFollowの肩越しオフセット(shoulderOffset)のせいでカメラ自体が横にズレた位置にあり、
// それをそのまま鏡映しすると映る位置がおかしくなる問題が出た。
// スクリーン座標で合わせ込む方式は「今まさに描画してるカメラ＝鏡映しした本人」という前提が
// 崩れると成立しない(肩越しオフセットで前提が崩れる)ので、方式そのものを変更した。
//
// 新方式: 鏡の部屋側にカメラを1台置き、常にプレイヤー(subject)の方を向かせる「証明写真/監視カメラ」方式。
// ゲームプレイ用カメラの位置・向きには一切依存しないので、肩越しオフセットだろうが
// どんな視点からこの鏡を覗き込もうが、映る中身は常にプレイヤー基準で安定する。
// カメラの位置はプレイヤーを鏡面で鏡映しした座標に置くので、鏡に近づくと鏡の中の自分も近づいてくる、
// という自然な距離感は保たれる。ただし完全な物理正しさ(视差)は捨てている＝鏡のUV座標にそのまま
// 映すだけで、見る角度によって中身が変わったりはしない（普通の防犯カメラ映像と同じ）。
using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
public class PlanarMirror : MonoBehaviour {
    [Header("鏡が向いている方向")]
    [Tooltip("trueなら起動時にRaycastで自動判定する(壁の中側 vs 部屋側)。置き場所を変えても手動調整不要にするため")]
    public bool autoDetectFace = true;
    [Tooltip("鏡面の法線は このTransformのforward × faceSign。autoDetectFace=trueなら起動時に上書きされる。" +
              "自動判定が誤爆する(壁が無い/両側とも開けている等)場合はfalseにして手動指定する")]
    public float faceSign = -1f;
    [Tooltip("自動判定用Raycastの最大探索距離。壁の厚み方向はすぐ当たり、部屋側はこれより開けている前提")]
    public float autoDetectProbeDistance = 3f;

    [Header("映す対象")]
    [Tooltip("未設定なら実行時に tag=Player のオブジェクトを使う。ゲームプレイ用カメラではなく" +
              "プレイヤー本体を基準にすることで、CameraFollowの肩越しオフセットの影響を受けないようにしている")]
    public Transform subject;
    [Tooltip("subjectの足元からこの高さを「目線」として使う。カメラ自身もこの高さに揃えるので、" +
              "視線がほぼ水平になり、近くの天井や階段裏などを見上げてしまうのを防ぐ")]
    public float eyeHeight = 1.4f;

    [Header("鏡カメラのレンズ")]
    public float fieldOfView = 45f;
    public float nearClipPlane = 0.05f;
    public float farClipPlane = 30f;
    [Tooltip("反射カメラが描画するレイヤー。UI等の不要なレイヤーは外しておく")]
    public LayerMask cullingMask = ~0;
    [Tooltip("鏡カメラを鏡面から部屋側へ固定でこれだけ浮かせる。プレイヤーの奥行きは反射しない" +
              "(反射させると、部屋が狭い場所では鏡の裏の壁の中にカメラが出てしまい、壁に視界を塞がれることがあるため)")]
    public float mirrorCamStandoff = 0.3f;

    [Header("パフォーマンス")]
    [Tooltip("subjectがこの距離より遠い間は鏡カメラを丸ごと無効化する(レンダリングコスト0)")]
    public float activationDistance = 6f;
    [Tooltip("反射テクスチャの一辺(px)。低いほど軽い")]
    public int textureSize = 512;

    Camera mirrorCamera;
    RenderTexture mirrorTexture;
    Material instanceMaterial;
    Vector3 planePosWorld; // 鏡面の実際の中心(メッシュのworld bounds中心)
    Vector3 roomNormal;    // 部屋側(映したい側)を向く法線。faceSign適用済み

    static readonly int ReflectionTexId = Shader.PropertyToID("_ReflectionTex");
    static readonly int LocalUvRectId = Shader.PropertyToID("_LocalUvRect");

    void Awake() {
        var meshRenderer = GetComponent<MeshRenderer>();
        instanceMaterial = meshRenderer.material; // sharedMaterialを汚さないようインスタンス化

        // 鏡面の実中心。model_MirrorのようにPivotが端(下端等)にあるモデルでも
        // transform.positionではなくこちらを基準点として使う
        planePosWorld = meshRenderer.bounds.center;

        // インポート元FBXのUV0は共有アトラス前提の狭い範囲(例: U 0〜0.65, V 0.67〜0.68)にしか
        // マッピングされておらず、そのまま使うと反射テクスチャが細い帯にしか映らない。
        // なのでUVはメッシュのUV0を使わず、ローカル座標(平面内のXY)から0〜1に作り直す
        var mesh = GetComponent<MeshFilter>().sharedMesh;
        Bounds lb = mesh.bounds;
        instanceMaterial.SetVector(LocalUvRectId, new Vector4(
            lb.min.x, lb.min.y,
            Mathf.Max(0.0001f, lb.size.x), Mathf.Max(0.0001f, lb.size.y)));

        if (autoDetectFace) faceSign = DetectFaceSign();
        roomNormal = (transform.forward * Mathf.Sign(faceSign)).normalized;

        if (subject == null) {
            // このプロジェクトのPlayerはtag未設定(Untagged)なので、tag検索ではなく名前で探す
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p == null) p = GameObject.Find("Player");
            if (p != null) subject = p.transform;
        }

        var camGO = new GameObject(name + "_MirrorCamera");
        camGO.hideFlags = HideFlags.HideAndDontSave;
        camGO.transform.SetParent(transform, false);

        mirrorCamera = camGO.AddComponent<Camera>();
        mirrorCamera.fieldOfView = fieldOfView;
        mirrorCamera.nearClipPlane = nearClipPlane;
        mirrorCamera.farClipPlane = farClipPlane;
        mirrorCamera.cullingMask = cullingMask;
        mirrorCamera.enabled = false; // activationDistance判定でON/OFFする

        var camData = camGO.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        camData.renderShadows = false;
        camData.renderPostProcessing = false;

        int size = Mathf.Max(64, textureSize);
        mirrorTexture = new RenderTexture(size, size, 16, RenderTextureFormat.Default);
        mirrorTexture.name = name + "_MirrorRT";
        mirrorCamera.targetTexture = mirrorTexture;
        instanceMaterial.SetTexture(ReflectionTexId, mirrorTexture);
    }

    /// <summary>
    /// 鏡は壁に対して裏表どちらを向いているか分からない(コピー配置のたびに手動-1/1調整するのは非現実的)ので、
    /// 起動時に両面へRaycastして「壁の中(すぐ何かに当たる)」側と「部屋(開けている)」側を自動判定する。
    /// </summary>
    float DetectFaceSign() {
        Vector3 fwd = transform.forward;
        const float skin = 0.03f; // 鏡自身の厚みぶん面から少し浮かせてから撃つ(自己ヒット防止)
        float distFwd = ProbeDistance(fwd, skin);
        float distBack = ProbeDistance(-fwd, skin);
        // 両側とも同じ距離(＝コライダーが無い/両側とも開けている等で判定材料なし)なら、
        // 誤った既定値で上書きしないよう、Inspectorで設定済みのfaceSignをそのまま使う
        if (Mathf.Approximately(distFwd, distBack)) return faceSign;
        // すぐ何かに当たった(=壁の中)側の反対、より開けている側を部屋側とみなす
        return (distBack > distFwd) ? -1f : 1f;
    }

    float ProbeDistance(Vector3 dir, float skin) {
        Vector3 origin = planePosWorld + dir * skin;
        if (Physics.Raycast(origin, dir, out RaycastHit hit, autoDetectProbeDistance, ~0, QueryTriggerInteraction.Ignore))
            return hit.distance;
        return autoDetectProbeDistance; // 何にも当たらない=十分開けている
    }

    void LateUpdate() {
        if (subject == null) { mirrorCamera.enabled = false; return; }

        float dist = Vector3.Distance(subject.position, planePosWorld);
        if (dist > activationDistance) { mirrorCamera.enabled = false; return; }
        mirrorCamera.enabled = true;

        // 鏡カメラは鏡面から部屋側(自動判定済みで開けていると確認済みの方向)へ固定距離だけ浮かせた位置に置く。
        // プレイヤーの奥行きを反射して距離を再現する方式も試したが、部屋が狭い場所(壁のすぐ裏)だと
        // 反射後の位置が壁の中に出てしまい、鏡カメラ自身が壁でふさがれて何も映らなくなる罠を踏んだ。
        // 固定距離にすることで、自動判定で確認済みの「開けている側」から絶対に動かないので安全。
        Vector3 camPos = planePosWorld + roomNormal * mirrorCamStandoff;
        camPos.y = subject.position.y + eyeHeight;
        mirrorCamera.transform.position = camPos;

        // 注視点もカメラと同じ高さ(目線)にして、視線がほぼ水平になるようにする
        Vector3 lookPoint = subject.position + Vector3.up * eyeHeight;
        mirrorCamera.transform.LookAt(lookPoint, Vector3.up);
    }

    void OnDestroy() {
        if (mirrorTexture != null) {
            mirrorTexture.Release();
            Destroy(mirrorTexture);
        }
        if (instanceMaterial != null) Destroy(instanceMaterial);
    }
}
