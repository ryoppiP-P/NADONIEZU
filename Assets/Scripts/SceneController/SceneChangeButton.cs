using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

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
        SceneManager.LoadScene(sceneName);
    }
}
