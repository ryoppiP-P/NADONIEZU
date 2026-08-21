//==============================================================================
//  File   : SaveData.cs
//  Brief  : セーブデータの管理
//
//  Author : Ryoto Kikuchi
//  Date   : 2026/6/18
//------------------------------------------------------------------------------
//==============================================================================
using System;
using System.Collections.Generic;

[Serializable]
public class SaveData {
    // バージョン管理
    public int saveVersion = 1;

    // 各システム
    public SettingsData settings = new SettingsData();
}

// 設定
[Serializable]
public class SettingsData {
    public float masterVolume = 70f;
    public float bgmVolume = 50f;
    public float seVolume = 50f;
    public float cameraSensitivity = 50f;   // 0-100スケール、50が基準(1.0倍)
    // タッチ操作UI（MoveStick/InteractBG/Jump）の不透明度、2Dスケール、100が不透明
    public float controlOpacity = 100f;
}
