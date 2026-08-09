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

    [Header("Choices")]
    [SerializeField] private GameObject choicesPanel;
    [SerializeField] private Button[] choiceButtons = new Button[4]; // 4つ想定

    [Header("選択肢表示中は隠すUI（移動スティック等）")]
    [SerializeField] private GameObject[] hudToHideOnChoices;

    [Header("Typewriter")]
    [SerializeField] private float charInterval = 0.03f;

    private Coroutine typeCoroutine;

    private Action<int> onChoiceSelected;

    void Awake() {
        // Nextボタン → DialogueManager.Advance()
        nextButton.onClick.AddListener(() => DialogueManager.Instance.Advance());

        // 選択肢ボタン
        for (int i = 0; i < choiceButtons.Length; i++) {
            int index = i; // クロージャ対策
            choiceButtons[i].onClick.AddListener(() => onChoiceSelected?.Invoke(index));
        }

        Hide();
    }

    public void Show() {
        panel.SetActive(true);
        choicesPanel.SetActive(false);
        nextButton.gameObject.SetActive(true);
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
        typeCoroutine = StartCoroutine(TypeText(text));
    }

    public void ShowChoices(List<DialogueChoice> choices, Action<int> callback) {
        onChoiceSelected = callback;
        nextButton.gameObject.SetActive(false);
        choicesPanel.SetActive(true);
        SetHudVisible(false);

        // ボタンにテキスト設定、余った分は非表示
        for (int i = 0; i < choiceButtons.Length; i++) {
            if (i < choices.Count) {
                choiceButtons[i].gameObject.SetActive(true);
                var label = choiceButtons[i].GetComponentInChildren<TextMeshProUGUI>();
                label.text = $"{i + 1}. {choices[i].text}";

                // route-based button color (each choice colored by its route)
                var rc = RouteColor(choices[i].route);
                var btnUI = choiceButtons[i];
                if (btnUI.image != null) btnUI.image.color = rc;
                var cb = btnUI.colors;
                cb.normalColor = rc;
                cb.highlightedColor = Color.Lerp(rc, Color.white, 0.2f);
                cb.pressedColor = Color.Lerp(rc, Color.black, 0.2f);
                cb.selectedColor = rc;
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

    // 選択肢の表示/非表示に合わせて、移動スティックなど他のUIをまとめて切り替える
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
        lineLabel.text = "";
        foreach (char c in text) {
            lineLabel.text += c;
            yield return new WaitForSeconds(charInterval);
        }
        typeCoroutine = null;
    }
}
