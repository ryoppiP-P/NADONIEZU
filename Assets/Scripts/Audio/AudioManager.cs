using UnityEngine;

public class AudioManager : MonoBehaviour {
    public static AudioManager Instance { get; private set; }

    [Header("音源ライブラリ")]
    [Tooltip("AudioLibrary ScriptableObjectをアサイン")]
    [SerializeField] private AudioLibrary library;

    [Header("BGM設定")]
    [SerializeField] private AudioSource bgmSource;

    [Header("SEの3D設定")]
    public float seMinDistance = 1f;
    public float seMaxDistance = 20f;

    // 内部音量（0 1）
    private float masterVolume01 = 1f;
    private float bgmVolume01 = 0.7f;
    private float seVolume01 = 0.7f;

    void Awake() {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (library == null) {
            Debug.LogError("[AudioManager] AudioLibraryが未アサイン");
        } else {
            library.BuildMaps();
        }

        SetupBgmSource();

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

    public void LoadFromSaveData() {
        if (SaveManager.Instance == null || SaveManager.Instance.Current == null) return;

        var s = SaveManager.Instance.Current.settings;
        masterVolume01 = Mathf.Clamp01(s.masterVolume / 100f);
        bgmVolume01 = Mathf.Clamp01(s.bgmVolume / 100f);
        seVolume01 = Mathf.Clamp01(s.seVolume / 100f);

        ApplyBgmVolume();
    }

    // ==========================================================================
    // BGM
    // ==========================================================================

    public void PlayBGM(BGM id, bool restartIfSame = false) {
        if (id == BGM.None) { StopBGM(); return; }
        if (library == null || !library.TryGetBGM(id, out var entry)) {
            Debug.LogWarning($"[AudioManager] BGM未登録: {id}");
            return;
        }
        if (!restartIfSame && bgmSource.clip == entry.clip && bgmSource.isPlaying) return;

        bgmSource.clip = entry.clip;
        bgmSource.volume = entry.volume * bgmVolume01 * masterVolume01;
        bgmSource.Play();
    }

    public void StopBGM() {
        bgmSource.Stop();
    }

    // ==========================================================================
    // SE
    // ==========================================================================

    public void PlaySEAtPosition(SE id, Vector3 position) {
        if (!TryGetSE(id, out var entry)) return;
        SpawnSESource(entry, position, null, spatial3D: true);
    }

    public void PlaySEOnObject(SE id, Transform target) {
        if (target == null) return;
        if (!TryGetSE(id, out var entry)) return;
        SpawnSESource(entry, target.position, target, spatial3D: true);
    }

    public void PlaySE2D(SE id) {
        if (!TryGetSE(id, out var entry)) return;
        SpawnSESource(entry, Vector3.zero, transform, spatial3D: false);
    }

    bool TryGetSE(SE id, out AudioLibrary.SEEntry entry) {
        entry = null;
        if (id == SE.None) return false;
        if (library == null) return false;
        if (!library.TryGetSE(id, out entry)) {
            Debug.LogWarning($"[AudioManager] SE未登録: {id}");
            return false;
        }
        return true;
    }

    void SpawnSESource(AudioLibrary.SEEntry entry, Vector3 position, Transform parent, bool spatial3D) {
        var go = new GameObject($"SE_{entry.id}");
        if (parent != null) {
            go.transform.SetParent(parent);
            go.transform.localPosition = Vector3.zero;
        } else {
            go.transform.position = position;
        }

        var src = go.AddComponent<AudioSource>();
        src.clip = entry.clip;
        src.spatialBlend = spatial3D ? 1f : 0f;
        src.volume = entry.volume * seVolume01 * masterVolume01;
        src.pitch = entry.pitch;
        if (spatial3D) {
            src.minDistance = seMinDistance;
            src.maxDistance = seMaxDistance;
            src.rolloffMode = AudioRolloffMode.Logarithmic;
        }
        src.Play();

        Destroy(go, entry.clip.length / Mathf.Max(0.1f, entry.pitch) + 0.1f);
    }

    // ==========================================================================
    // 音量制御（0 100スケール）
    // ==========================================================================

    public float GetMasterVolume() => masterVolume01 * 100f;
    public float GetBGMVolume() => bgmVolume01 * 100f;
    public float GetSEVolume() => seVolume01 * 100f;

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
        if (bgmSource == null) return;
        // 現在鳴っているBGMのライブラリ側個別音量を再取得したい場合は再取得ロジックが必要だが、
        // 通常は BGM 切替時に反映されればOK。ここでは masterVolume01 と bgmVolume01 のみ反映。
        // → 個別音量を維持するため、現在のvolumeから逆算せず、masterとbgmの掛け算のみ更新
        // ここではシンプルに master × bgm のみ反映（個別volumeは切替時に反映される）
        if (bgmSource.clip != null && library != null) {
            // 個別音量を維持するため、現在のBGMを再度検索して反映
            foreach (var e in library.bgmEntries) {
                if (e.clip == bgmSource.clip) {
                    bgmSource.volume = e.volume * bgmVolume01 * masterVolume01;
                    return;
                }
            }
        }
        bgmSource.volume = bgmVolume01 * masterVolume01;
    }

    void SaveToSaveData() {
        if (SaveManager.Instance == null || SaveManager.Instance.Current == null) return;

        var s = SaveManager.Instance.Current.settings;
        s.masterVolume = masterVolume01 * 100f;
        s.bgmVolume = bgmVolume01 * 100f;
        s.seVolume = seVolume01 * 100f;

        SaveManager.Instance.SaveAuto();
    }
}
