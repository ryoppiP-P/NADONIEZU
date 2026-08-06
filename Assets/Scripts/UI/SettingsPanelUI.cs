// 設定パネル（マスター/BGM/SE音量、カメラ感度）。
// TitleSceneとGameScene(ポーズメニュー)の両方から同じスクリプトで使う想定。
// 音量はAudioManagerが自前でSaveManagerへ即時オートセーブする。
// カメラ感度はCameraFollowが無いシーン（Title）でも調整できるよう、
// SaveManagerへ直接書き込む方式にしている（cameraFollowはGameScene用の任意参照）。
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SettingsPanelUI : MonoBehaviour {
    [Header("UI")]
    public GameObject panelRoot;
    public Slider masterSlider;
    public Slider bgmSlider;
    public Slider seSlider;
    public Slider sensitivitySlider;
    public Button closeButton;

    [Header("数値表示（任意）")]
    public TextMeshProUGUI masterValueText;
    public TextMeshProUGUI bgmValueText;
    public TextMeshProUGUI seValueText;
    public TextMeshProUGUI sensitivityValueText;

    [Header("GameSceneのみ：即時プレビュー用（任意）")]
    public CameraFollow cameraFollow;

    void Start() {
        if (masterSlider != null) masterSlider.onValueChanged.AddListener(OnMasterChanged);
        if (bgmSlider != null) bgmSlider.onValueChanged.AddListener(OnBgmChanged);
        if (seSlider != null) seSlider.onValueChanged.AddListener(OnSeChanged);
        if (sensitivitySlider != null) sensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);
        if (closeButton != null) closeButton.onClick.AddListener(Close);

        if (panelRoot != null) panelRoot.SetActive(false);
    }

    public void Open() {
        RefreshFromCurrentValues();
        if (panelRoot != null) panelRoot.SetActive(true);
    }

    public void Close() {
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    public void Toggle() {
        bool willOpen = panelRoot == null || !panelRoot.activeSelf;
        if (willOpen) Open(); else Close();
    }

    void RefreshFromCurrentValues() {
        float master = 70f, bgm = 50f, se = 50f, sens = 50f;

        if (AudioManager.Instance != null) {
            master = AudioManager.Instance.GetMasterVolume();
            bgm = AudioManager.Instance.GetBGMVolume();
            se = AudioManager.Instance.GetSEVolume();
        } else if (SaveManager.Instance != null && SaveManager.Instance.Current != null) {
            var s = SaveManager.Instance.Current.settings;
            master = s.masterVolume; bgm = s.bgmVolume; se = s.seVolume;
        }

        if (SaveManager.Instance != null && SaveManager.Instance.Current != null)
            sens = SaveManager.Instance.Current.settings.cameraSensitivity;

        if (masterSlider != null) masterSlider.SetValueWithoutNotify(master);
        if (bgmSlider != null) bgmSlider.SetValueWithoutNotify(bgm);
        if (seSlider != null) seSlider.SetValueWithoutNotify(se);
        if (sensitivitySlider != null) sensitivitySlider.SetValueWithoutNotify(sens);

        SetValueText(masterValueText, master);
        SetValueText(bgmValueText, bgm);
        SetValueText(seValueText, se);
        SetValueText(sensitivityValueText, sens);
    }

    void OnMasterChanged(float v) { AudioManager.Instance?.SetMasterVolume(v); SetValueText(masterValueText, v); }
    void OnBgmChanged(float v)    { AudioManager.Instance?.SetBGMVolume(v);    SetValueText(bgmValueText, v); }
    void OnSeChanged(float v)     { AudioManager.Instance?.SetSEVolume(v);     SetValueText(seValueText, v); }

    void OnSensitivityChanged(float v) {
        if (cameraFollow != null) cameraFollow.SetSensitivity(v);

        if (SaveManager.Instance != null && SaveManager.Instance.Current != null) {
            SaveManager.Instance.Current.settings.cameraSensitivity = v;
            SaveManager.Instance.SaveAuto();
        }

        SetValueText(sensitivityValueText, v);
    }

    void SetValueText(TextMeshProUGUI label, float v) {
        if (label != null) label.text = Mathf.RoundToInt(v).ToString();
    }
}
