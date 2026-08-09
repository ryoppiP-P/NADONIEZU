#if UNITY_EDITOR
using UnityEditor;

public class ModelReadWriteImporter : AssetPostprocessor {
    private static readonly string[] TargetFolders = {
        "Assets/Resources",
    };

    void OnPreprocessModel() {
        bool inTarget = false;
        foreach (var folder in TargetFolders) {
            if (assetPath.StartsWith(folder)) { inTarget = true; break; }
        }
        if (!inTarget) return;

        var importer = (ModelImporter)assetImporter;
        importer.isReadable = true;
    }
}
#endif
