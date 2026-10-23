using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using Component = UnityEngine.Component;

namespace PsycheVR.Modes.Editor
{
    public sealed class ReleaseBuildValidator : IPreprocessBuildWithReport
    {
        private const string ScenePath = "Assets/Scenes/Bedroom.unity";
        private const string SoundLibraryPath = "Assets/Resources/InteractionSoundLibrary.asset";
        private const string PackageId = "com.missiontopsyche.psyche20d";
        private const string Prefix = "[ReleaseBuildValidator]";
        public int callbackOrder => 0;

        [MenuItem("Tools/Release/Check Build")]
        private static void CheckFromMenu()
        {
            RunChecks(null, false);
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.Android) return;
            RunChecks(report, true);
        }

        private static void RunChecks(BuildReport report, bool isBuild)
        {
            var errors = new List<string>();
            CheckPrefabs(errors);
            CheckScene(errors);
            CheckSounds(errors);
            CheckSettings(errors, report, isBuild);

            foreach (string error in errors)
                Debug.LogError($"{Prefix} {error}");

            if (errors.Count > 0)
            {
                string summary = $"{Prefix} {errors.Count} issue(s). " +
                    "See Console for every asset/object and reason.";
                if (isBuild) throw new BuildFailedException(summary);
                Debug.LogError(summary);
            }
            else
            {
                Debug.Log($"{Prefix} All checks passed.");
            }
        }

        private static void CheckPrefabs(List<string> errors)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    errors.Add($"Cannot load prefab: {path}");
                    continue;
                }
                CheckHierarchy(prefab, path, errors);
            }
        }

        private static void CheckScene(List<string> errors)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                errors.Add($"Missing scene: {ScenePath}");
                return;
            }

            Scene scene = EditorSceneManager.OpenPreviewScene(ScenePath);
            try
            {
                foreach (GameObject root in scene.GetRootGameObjects())
                    CheckHierarchy(root, ScenePath, errors);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static void CheckHierarchy(GameObject root, string assetPath, List<string> errors)
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                GameObject go = transform.gameObject;
                string location = $"{assetPath} | {HierarchyPath(transform)}";
                int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
                if (missing > 0)
                    errors.Add($"{location}: {missing} missing MonoBehaviour script(s)");

                foreach (Component component in go.GetComponents<Component>())
                {
                    if (component == null) continue;
                    CheckReferences(component, location, errors);
                    if (component is Renderer renderer)
                        CheckRenderer(renderer, location, errors);
                }
            }
        }

        private static void CheckReferences(Component component, string location, List<string> errors)
        {
            var serialized = new SerializedObject(component);
            SerializedProperty property = serialized.GetIterator();
            while (property.NextVisible(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                if (property.objectReferenceValue == null &&
                    property.objectReferenceInstanceIDValue != 0)
                {
                    errors.Add($"{location} | {component.GetType().Name}.{property.propertyPath}: " +
                               "missing object reference");
                }
            }
        }

        private static void CheckRenderer(Renderer renderer, string location, List<string> errors)
        {
            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                if (material == null)
                {
                    errors.Add($"{location} | {renderer.GetType().Name} material slot {i}: missing material");
                    continue;
                }
                if (material.shader == null || material.shader.name == "Hidden/InternalErrorShader")
                    errors.Add($"{location} | material {AssetDatabase.GetAssetPath(material)}: missing/error shader");
            }
        }

        private static string HierarchyPath(Transform transform)
        {
            string result = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                result = transform.name + "/" + result;
            }
            return result;
        }

        private static void CheckSettings(List<string> errors, BuildReport report, bool isBuild)
        {
            if (PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android) != PackageId)
                errors.Add($"Android package ID must be {PackageId}");

            if (!isBuild)
            {
                string[] enabledScenes = EditorBuildSettings.scenes
                    .Where(s => s.enabled).Select(s => s.path).ToArray();
                if (enabledScenes.Length != 1 || enabledScenes[0] != ScenePath)
                    errors.Add("Editor Build Settings must enable Bedroom.unity as the only scene");
            }

            if (isBuild && report != null)
            {
                if ((report.summary.options & BuildOptions.Development) != 0)
                    errors.Add("Development Build must be disabled");
            }
            else if (EditorUserBuildSettings.development)
            {
                errors.Add("Development Build must be disabled");
            }
        }

        private static void CheckSounds(List<string> errors)
        {
            ScriptableObject library = AssetDatabase.LoadAssetAtPath<ScriptableObject>(SoundLibraryPath);
            if (library == null)
            {
                errors.Add($"Sound library missing/unreadable: {SoundLibraryPath}");
                return;
            }
            SoundValidationAdapter.Check(library, SoundLibraryPath, errors);
        }
    }
}
