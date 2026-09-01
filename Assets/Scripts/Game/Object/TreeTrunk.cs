// 街路樹の幹(Fracture)が壊れたら、支えを失った葉っぱも一緒に消して
// 「木ごと殴り倒した」ように見せるための小さな連携役。
using UnityEngine;

[RequireComponent(typeof(Fracture))]
public class TreeTrunk : MonoBehaviour {
    public GameObject[] leaves;

    Fracture fracture;

    void Awake() {
        fracture = GetComponent<Fracture>();
        fracture.callbackOptions.onFracture.AddListener(OnFractured);
    }

    void OnFractured(Collider col, GameObject go, Vector3 point) {
        foreach (var leaf in leaves) {
            if (leaf != null) leaf.SetActive(false);
        }
    }

    void OnDestroy() {
        if (fracture != null) fracture.callbackOptions.onFracture.RemoveListener(OnFractured);
    }
}
