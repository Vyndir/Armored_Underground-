using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace AU.EditorTools
{
    /// <summary>
    /// The district, textures and materials are generated, not checked in.
    /// On first open of a fresh clone this builds them and opens the scene.
    /// </summary>
    static class AutoSetup
    {
        [InitializeOnLoadMethod]
        static void Check()
        {
            if (UnityEngine.Application.isBatchMode) return;
            EditorApplication.delayCall += () =>
            {
                if (!File.Exists(SceneBuilder.ScenePath))
                {
                    if (EditorUtility.DisplayDialog("Armored Underground", "Generate the Kessel Ward district and assets now? (takes a minute or two)", "Generate", "Later"))
                    {
                        SceneBuilder.BuildAll();
                        EditorSceneManager.OpenScene(SceneBuilder.ScenePath);
                    }
                }
                else if (EditorSceneManager.GetActiveScene().path != SceneBuilder.ScenePath && string.IsNullOrEmpty(EditorSceneManager.GetActiveScene().path))
                    EditorSceneManager.OpenScene(SceneBuilder.ScenePath);
            };
        }
    }

}
