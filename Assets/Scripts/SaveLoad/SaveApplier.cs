//==============================================================================
//  File   : SaveApplier.cs
//  Brief  : SaveManager のデータを各システムに適用する
//
//  Author : Ryoto Kikuchi
//  Date   : 2026/6/18
//------------------------------------------------------------------------------
//  ゲーム起動時に SaveManager のデータを読んで、Audio / Screen 等に反映する。
//  全てのロード->適用をここで行う。
//  設定変更時の保存（Save系）を集約。
//==============================================================================
using UnityEngine;

public static class SaveApplier {
    //--------------------------------------------------------------------------
    // 全設定を一括適用
    //--------------------------------------------------------------------------
    public static void ApplyAll() {
        if (SaveManager.Instance == null) return;

        ApplyAudio();
        //ApplyScreen();
    }

    // ==========================================================================
    // Audio - 適用（SaveData → AudioManager）
    // ==========================================================================
    public static void ApplyAudio() {
        if (SaveManager.Instance == null) return;
        if (AudioManager.Instance == null) return;

        AudioManager.Instance.LoadFromSaveData();
    }

    // ==========================================================================
    // Audio - 保存（AudioManager → SaveData）
    // ==========================================================================
    public static void SaveAudio() {
        if (SaveManager.Instance == null) return;
        if (AudioManager.Instance == null) return;

        var s = SaveManager.Instance.Current.settings;
        s.masterVolume = AudioManager.Instance.GetMasterVolume();
        s.bgmVolume = AudioManager.Instance.GetBGMVolume();
        s.seVolume = AudioManager.Instance.GetSEVolume();
        SaveManager.Instance.SaveAuto();
    }
}
