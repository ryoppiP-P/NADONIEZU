using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class SceneChangeButton : MonoBehaviour {
    [SerializeField] private string sceneName;

    void Start() {
        GetComponent<Button>().onClick.AddListener(ChangeScene);
    }

    public void ChangeScene() {
        if (string.IsNullOrEmpty(sceneName)) {
            return;
        }

        FadeManager.FadeOut(sceneName, 1.5f);
    }
}
