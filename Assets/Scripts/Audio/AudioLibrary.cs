using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "AudioLibrary", menuName = "Audio/Audio Library")]
public class AudioLibrary : ScriptableObject {

    [Serializable]
    public class BGMEntry {
        public BGM id;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
    }

    [Serializable]
    public class SEEntry {
        public SE id;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
        [Range(0.5f, 2f)] public float pitch = 1f;
    }

    [Header("BGMクリップ登録")]
    public List<BGMEntry> bgmEntries = new List<BGMEntry>();

    [Header("SEクリップ登録")]
    public List<SEEntry> seEntries = new List<SEEntry>();

    // === ランタイムアクセス用（内部キャッシュ）===
    private Dictionary<BGM, BGMEntry> bgmMap;
    private Dictionary<SE, SEEntry> seMap;

    /// <summary>Dictionary構築（初回アクセス時 or 明示的な再構築時）</summary>
    public void BuildMaps() {
        bgmMap = new Dictionary<BGM, BGMEntry>();
        foreach (var e in bgmEntries) {
            if (e == null || e.clip == null) continue;
            bgmMap[e.id] = e;
        }

        seMap = new Dictionary<SE, SEEntry>();
        foreach (var e in seEntries) {
            if (e == null || e.clip == null) continue;
            seMap[e.id] = e;
        }
    }

    public bool TryGetBGM(BGM id, out BGMEntry entry) {
        if (bgmMap == null) BuildMaps();
        return bgmMap.TryGetValue(id, out entry);
    }

    public bool TryGetSE(SE id, out SEEntry entry) {
        if (seMap == null) BuildMaps();
        return seMap.TryGetValue(id, out entry);
    }
}
