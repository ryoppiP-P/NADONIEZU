using UnityEngine;

public class AudioManager : MonoBehaviour {
    public static AudioManager Instance { get; private set; }

    [Header("BGM設定")]
    [SerializeField] private AudioSource bgmSource;

    [Header("SEの3D設定")]
    [Tooltip("SEのデフォルト最小距離（この距離まで最大音量）")]
    public float seMinDistance = 1f;
    [Tooltip("SEのデフォルト最大距離（この距離で無音）")]
    public float seMaxDistance = 20f;

    // 内部音量（0 1）。save.datは0 100スケールなので変換する
    private float masterVolume01 = 1f;
    private float bgmVolume01 = 0.7f;
    private float seVolume01 = 0.7f;

    void Awake() {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        SetupBgmSource();

        // SaveManagerが先に初期化されていれば設定を読み込む
        if (SaveManager.Instance != null) {
            LoadFromSaveData();
        }
    }

    void SetupBgmSource() {
        if (bgmSource == null) {
            var go = new GameObject("BGMSource");
            go.transform.SetParent(transform);
            bgmSource = go.AddComponent<AudioSource>();
        }
        bgmSource.loop = true;
        bgmSource.spatialBlend = 0f;
        bgmSource.playOnAwake = false;
    }

    /// <summary>SaveDataから音量を読み込んで反映</summary>
    public void LoadFromSaveData() {
        if (SaveManager.Instance == null || SaveManager.Instance.Current == null) return;

        var s = SaveManager.Instance.Current.settings;
        masterVolume01 = Mathf.Clamp01(s.masterVolume / 100f);
        bgmVolume01 = Mathf.Clamp01(s.bgmVolume / 100f);
        seVolume01 = Mathf.Clamp01(s.seVolume / 100f);

        ApplyBgmVolume();
    }

    // === BGM ===

    public void PlayBGM(AudioClip clip, bool restartIfSame = false) {
        if (clip == null) return;
        if (!restartIfSame && bgmSource.clip == clip && bgmSource.isPlaying) return;

        bgmSource.clip = clip;
        ApplyBgmVolume();
        bgmSource.Play();
    }

    public void StopBGM() {
        bgmSource.Stop();
    }

    // === SE ===

    /// <summary>指定位置でSEを鳴らす（3D）</summary>
    public void PlaySEAtPosition(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 1f) {
        if (clip == null) return;

        var go = new GameObject($"SE_{clip.name}");
        go.transform.position = position;

        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.spatialBlend = 1f;
        src.volume = volume * seVolume01 * masterVolume01;
        src.pitch = pitch;
        src.minDistance = seMinDistance;
        src.maxDistance = seMaxDistance;
        src.rolloffMode = AudioRolloffMode.Logarithmic;
        src.Play();

        Destroy(go, clip.length / Mathf.Max(0.1f, pitch) + 0.1f);
    }

    /// <summary>Transformに追従してSEを鳴らす</summary>
    public void PlaySEOnObject(AudioClip clip, Transform target, float volume = 1f, float pitch = 1f) {
        if (clip == null || target == null) return;

        var go = new GameObject($"SE_{clip.name}");
        go.transform.SetParent(target);
        go.transform.localPosition = Vector3.zero;

        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.spatialBlend = 1f;
        src.volume = volume * seVolume01 * masterVolume01;
        src.pitch = pitch;
        src.minDistance = seMinDistance;
        src.maxDistance = seMaxDistance;
        src.rolloffMode = AudioRolloffMode.Logarithmic;
        src.Play();

        Destroy(go, clip.length / Mathf.Max(0.1f, pitch) + 0.1f);
    }

    /// <summary>2DのSE（UIクリック音など）</summary>
    public void PlaySE2D(AudioClip clip, float volume = 1f, float pitch = 1f) {
        if (clip == null) return;

        var go = new GameObject($"SE2D_{clip.name}");
        go.transform.SetParent(transform);

        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.spatialBlend = 0f;
        src.volume = volume * seVolume01 * masterVolume01;
        src.pitch = pitch;
        src.Play();

        Destroy(go, clip.length / Mathf.Max(0.1f, pitch) + 0.1f);
    }

    // === 音量取得（0 100スケール、SettingsDataと同スケール）===

    public float GetMasterVolume() => masterVolume01 * 100f;
    public float GetBGMVolume() => bgmVolume01 * 100f;
    public float GetSEVolume() => seVolume01 * 100f;

    // === 音量設定（0 100スケールで受け取る）===

    public void SetMasterVolume(float v0to100) {
        masterVolume01 = Mathf.Clamp01(v0to100 / 100f);
        ApplyBgmVolume();
        SaveToSaveData();
    }

    public void SetBGMVolume(float v0to100) {
        bgmVolume01 = Mathf.Clamp01(v0to100 / 100f);
        ApplyBgmVolume();
        SaveToSaveData();
    }

    public void SetSEVolume(float v0to100) {
        seVolume01 = Mathf.Clamp01(v0to100 / 100f);
        SaveToSaveData();
    }

    void ApplyBgmVolume() {
        if (bgmSource != null)
            bgmSource.volume = bgmVolume01 * masterVolume01;
    }

    // === SaveManagerへの書き込み ===

    void SaveToSaveData() {
        if (SaveManager.Instance == null || SaveManager.Instance.Current == null) return;

        var s = SaveManager.Instance.Current.settings;
        s.masterVolume = masterVolume01 * 100f;
        s.bgmVolume = bgmVolume01 * 100f;
        s.seVolume = seVolume01 * 100f;

        SaveManager.Instance.SaveAuto();
    }
}
