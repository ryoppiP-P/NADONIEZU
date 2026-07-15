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

        // ボタンにテキスト設定、余った分は非表示
        for (int i = 0; i < choiceButtons.Length; i++) {
            if (i < choices.Count) {
                choiceButtons[i].gameObject.SetActive(true);
                var label = choiceButtons[i].GetComponentInChildren<TextMeshProUGUI>();
                label.text = $"{i + 1}. {choices[i].text}";
            }
            else {
                choiceButtons[i].gameObject.SetActive(false);
            }
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
