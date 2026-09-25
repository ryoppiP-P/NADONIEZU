// 会話のUI制御
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;

public class DialogueUI : MonoBehaviour {
    [Header("Panel")]
    [SerializeField] private GameObject panel;

    [Header("Text")]
    [SerializeField] private TextMeshProUGUI speakerLabel;
    [SerializeField] private TextMeshProUGUI lineLabel;

    [Header("Next Button")]
    [SerializeField] private Button nextButton;

    [Tooltip("行が全部表示し終わった時だけ出す▼マーカー")]
    [SerializeField] private GameObject nextMarker;

    [Header("Choices")]
    [SerializeField] private GameObject choicesPanel;
    [SerializeField] private Button[] choiceButtons = new Button[4]; // 4個固定

    [Header("選択肢表示中は隠すUI（移動スティック等）")]
    [SerializeField] private GameObject[] hudToHideOnChoices;

    [Header("Typewriter")]
    [SerializeField] private float charInterval = 0.03f;

    private Coroutine typeCoroutine;
    private string currentFullText;
    private bool isTyping;
    [Tooltip("Play the typewriter blip once every N visible characters (1 = every character)")]
    [SerializeField] private int typeSoundEveryN = 2;
    private int typeSoundCounter;

    private Action<int> onChoiceSelected;

    void Awake() {
        // Nextボタン → DialogueManager.Advance()
        nextButton.onClick.AddListener(OnNextClicked);

        // 選択肢ボタン
        for (int i = 0; i < choiceButtons.Length; i++) {
            int index = i; // クロージャ対策
            choiceButtons[i].onClick.AddListener(() => {
                var feel = choiceButtons[index].GetComponent<ChoiceButtonFeel>();
                if (feel != null) feel.PlaySelectBurst();
                onChoiceSelected?.Invoke(index);
            });
        }

        Hide();
    }

    public void Show() {
        panel.SetActive(true);
        choicesPanel.SetActive(false);
        nextButton.gameObject.SetActive(true);
        SetMarkerVisible(false);
    }

    public void Hide() {
        panel.SetActive(false);
        SetHudVisible(true);
    }

    public void SetSpeaker(string name) {
        speakerLabel.text = name;
    }

    public void SetLine(string text) {
        if (typeCoroutine != null) StopCoroutine(typeCoroutine);
        currentFullText = text;
        typeCoroutine = StartCoroutine(TypeText(text));
    }

    // ▼マーカーの表示切替。未接続でも落ちないようnullガードする
    void SetMarkerVisible(bool visible) {
        if (nextMarker != null) nextMarker.SetActive(visible);
    }

    public bool IsTyping => isTyping;

    public void CompleteLine() {
        if (typeCoroutine != null) { StopCoroutine(typeCoroutine); typeCoroutine = null; }
        lineLabel.text = currentFullText;
        isTyping = false;
        SetMarkerVisible(true);
    }

    // タイプライター中のタップは全文表示へスキップ、表示済みのタップで次へ進む
    void OnNextClicked() {
        if (isTyping) CompleteLine();
        else DialogueManager.Instance.Advance();
    }

    public void ShowChoices(List<DialogueChoice> choices, Action<int> callback) {
        onChoiceSelected = callback;
        nextButton.gameObject.SetActive(false);
        SetMarkerVisible(false);
        choicesPanel.SetActive(true);
        SetHudVisible(false);
        Canvas.ForceUpdateCanvases(); // GridLayoutGroupの配置を確定させてから座標を読む（アンビエントFXの生成位置がズレないように）

        // ボタンにテキスト設定、余った分は非表示
        for (int i = 0; i < choiceButtons.Length; i++) {
            if (i < choices.Count) {
                var btnUI0 = choiceButtons[i];

                // route-based button color
                var accent = RouteColor(choices[i].route);
                var fill = accent;

                // バカゲー要素: ルートごとの常時動くアンビエントエフェクトを仕込む。
                // SetActive(true)でOnEnable()が同期的に走ってrouteを読みに行くため、
                // 有効化する前に必ずrouteを確定させておく（順番を間違えると前回のrouteのまま動いてしまう）。
                var feel = btnUI0.GetComponent<ChoiceButtonFeel>();
                if (feel == null) feel = btnUI0.gameObject.AddComponent<ChoiceButtonFeel>();
                feel.route = choices[i].route;
                btnUI0.gameObject.SetActive(true);
                feel.Refresh(); // 親パネルのSetActiveで先にOnEnable済みのケースに備え、routeを確定させてから明示的にも起動し直す

                var label = choiceButtons[i].GetComponentInChildren<TextMeshProUGUI>();
                label.text = $"{i + 1}. {choices[i].text}";

                var btnUI = btnUI0;
                if (btnUI.image != null) btnUI.image.color = fill;
                var cb = btnUI.colors;
                cb.normalColor = fill;
                cb.highlightedColor = Color.Lerp(fill, Color.white, 0.15f);
                cb.pressedColor = Color.Lerp(fill, Color.black, 0.2f);
                cb.selectedColor = fill;
                btnUI.colors = cb;
            }
            else {
                choiceButtons[i].gameObject.SetActive(false);
            }
        }
    }

    public void HideChoices() {
        if (choicesPanel != null) choicesPanel.SetActive(false);
        if (nextButton != null) nextButton.gameObject.SetActive(true);
        SetHudVisible(true);
    }

    // 選択肢の表示/非表示に合わせて、移動スティックなど操作UIをまとめて切り替える
    void SetHudVisible(bool visible) {
        if (hudToHideOnChoices == null) return;
        foreach (var go in hudToHideOnChoices) {
            if (go != null) go.SetActive(visible);
        }
    }

    // route -> button color (Romance/Normal/Madness/Rebel)
    static Color RouteColor(RouteType route) {
        switch (route) {
            case RouteType.Romance: return new Color(0.95f, 0.45f, 0.65f); // renai : pink
            case RouteType.Normal:  return new Color(0.55f, 0.70f, 0.85f); // futsuu: light blue
            case RouteType.Madness: return new Color(0.62f, 0.38f, 0.78f); // kyoujin: purple
            case RouteType.Rebel:   return new Color(0.88f, 0.35f, 0.32f); // hankou: red
            default: return Color.white;
        }
    }

    System.Collections.IEnumerator TypeText(string text) {
        isTyping = true;
        SetMarkerVisible(false);
        lineLabel.text = "";
        foreach (char c in text) {
            lineLabel.text += c;
            if (!char.IsWhiteSpace(c) && typeSoundEveryN > 0 && (++typeSoundCounter % typeSoundEveryN) == 0)
                AudioManager.Instance?.PlaySE2D(SE.DialogueTypewriter);
            yield return new WaitForSeconds(charInterval);
        }
        isTyping = false;
        SetMarkerVisible(true);
        typeCoroutine = null;
    }
}
