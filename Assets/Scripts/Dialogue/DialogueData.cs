// 会話を管理するためのScriptableObject。会話の内容、選択肢、IKDの増減などを定義する。
using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewDialogue", menuName = "Dialogue/Dialogue Data")]
public class DialogueData : ScriptableObject {
    [Header("Speaker")]
    public string speakerName = "???";

    [Header("Lines")]
    [TextArea(2, 5)]
    public List<string> lines = new List<string>();

    [Header("Choices (最大4つ)")]
    public List<DialogueChoice> choices = new List<DialogueChoice>();

    [Header("Game Phase")]
    public bool advancesToMiddlePhase = false; // この会話が終了したらゲームフェーズをMiddleに進めるか
    public bool triggersEnding = false;
}

[System.Serializable]
public class DialogueChoice {
    [TextArea(1, 3)]
    public string text = "選択肢テキスト";

    [Tooltip("この選択肢を選んだ時に増減するIKD")]
    public int ikdDelta = 0;

    [Tooltip("この選択肢を選んだ時のルート傾向ポイント")]
    public RouteType route;    // この選択のルート傾向

    [Tooltip("この選択肢を選んだ後の返答セリフ")]
    [TextArea(2, 5)] public string[] responseLines;
}
