using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Aurora.RouteProgress.EditorTools
{
    public static class MenuSetup
    {
        [MenuItem("Aurora/Menu/Setup DifficultySelection Scene")]
        public static void SetupDifficultyScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.name.Contains("DifficultySelection"))
            {
                Debug.LogWarning("[MenuSetup] Abre la escena DifficultySelection primero.");
                return;
            }

            var mc = Object.FindFirstObjectByType<MenuController>();
            if (mc == null)
            {
                Debug.LogError("[MenuSetup] No se encontró MenuController en la escena.");
                return;
            }

            if (mc.GetComponent<DifficultyMenuWirer>() == null)
            {
                mc.gameObject.AddComponent<DifficultyMenuWirer>();
                EditorUtility.SetDirty(mc.gameObject);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[MenuSetup] DifficultyMenuWirer añadido a MenuController. Guarda la escena.");
            Selection.activeObject = mc.gameObject;
        }
    }
}
