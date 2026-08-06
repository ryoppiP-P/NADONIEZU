//==============================================================================
//  File   : SaveManager.cs
//  Brief  : セーブ機能の管理（セーブデータ読み込み/書き込み・初期ロード）
//
//  Author : Ryoto Kikuchi
//  Date   : 2026/6/18
//------------------------------------------------------------------------------
//  セーブデータの初期適応は SaveApplier に任せる。
//==============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class SaveManager : MonoBehaviour {
    public static SaveManager Instance { get; private set; }

    [Header("Debug")]
    [SerializeField] private bool useEncryption = true;
    [SerializeField] private bool verboseLog = false;

    private const string FileName = "save.dat";
    private string SavePath => Path.Combine(Application.persistentDataPath, FileName);

    // 現在メモリ上のデータ（アクセス用）
    public SaveData Current { get; private set; }

    // セーブデータの変更を通知するイベント（コイン等の変化をUIに反映させるために使う）
    public event Action OnCurrencyChanged;

    private void Awake() {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 起動時に自動ロード（無ければ新規）
        Load();
    }

    // ===== 公開API =====

    /// <summary>手動セーブ（メニュー等から）</summary>
    public void SaveManual() {
        WriteToDisk();
        if (verboseLog) Debug.Log("[SaveManager] Manual save");
    }

    /// <summary>オートセーブ（チェックポイント等から呼ぶ）</summary>
    public void SaveAuto() {
        WriteToDisk();
        if (verboseLog) Debug.Log("[SaveManager] Auto save");
    }

    /// <summary>ロード（起動時に呼ばれるが、外部から再ロードしたいときも使える）</summary>
    public void Load() {
        if (!File.Exists(SavePath)) {
            Current = new SaveData();
            if (verboseLog) Debug.Log("[SaveManager] セーブデータなし → 新規作成");
            SaveApplier.ApplyAll();
            return;
        }

        try {
            string raw = File.ReadAllText(SavePath);
            string json = useEncryption ? SaveCrypto.Decrypt(raw) : raw;
            Current = JsonUtility.FromJson<SaveData>(json) ?? new SaveData();
            if (verboseLog) Debug.Log($"[SaveManager] Loaded\n{json}");
        } catch (Exception e) {
            Debug.LogError($"[SaveManager] ロード失敗: {e.Message} → 新規作成");
            Current = new SaveData();
        }
        SaveApplier.ApplyAll();
    }

    /// <summary>セーブデータ削除（タイトル画面の「最初から」等で）</summary>
    public void DeleteSave() {
        if (File.Exists(SavePath)) File.Delete(SavePath);
        Current = new SaveData();
    }

    public bool HasSaveData() => File.Exists(SavePath);

    // ===== 内部 =====

    private void WriteToDisk() {
        if (Current == null) Current = new SaveData();
        string json = JsonUtility.ToJson(Current, false);
        string output = useEncryption ? SaveCrypto.Encrypt(json) : json;
        File.WriteAllText(SavePath, output);
        if (verboseLog) Debug.Log($"[SaveManager] Saved to {SavePath}");
    }


    // ===== 便利メソッド（よく使う操作のショートカット） =====
}
