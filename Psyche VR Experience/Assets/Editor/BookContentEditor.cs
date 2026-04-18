using UnityEditor;
using UnityEngine;
using PsycheVR.Gameplay;

namespace PsycheVR.EditorTools
{
    /// <summary>
    /// Custom inspector for BookContent that adds a "Load From Folder" button.
    /// Scans a folder for materials by naming convention and assigns them.
    ///
    /// Expected folder layout (case-insensitive, partial match):
    ///   Cover.mat            -> cover
    ///   CoverPage.mat        -> coverPage
    ///   SpineFace.mat        -> spineFace
    ///   SpineText.mat        -> spineText
    ///   PageEdges.mat        -> pageEdges
    ///   Page001Front.mat     -> pages[0].front
    ///   Page001Back.mat      -> pages[0].back
    ///   Page002Front.mat     -> pages[1].front
    ///   ... (up to Page004)
    /// </summary>
    [CustomEditor(typeof(BookContent))]
    public class BookContentEditor : UnityEditor.Editor
    {
        private DefaultAsset folderAsset;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Bulk Load", EditorStyles.boldLabel);

            folderAsset = (DefaultAsset)EditorGUILayout.ObjectField(
                "Materials Folder", folderAsset, typeof(DefaultAsset), false);

            using (new EditorGUI.DisabledScope(folderAsset == null))
            {
                if (GUILayout.Button("Load Materials From Folder"))
                    LoadFromFolder();
            }
        }

        private void LoadFromFolder()
        {
            string folderPath = AssetDatabase.GetAssetPath(folderAsset);
            if (string.IsNullOrEmpty(folderPath) ||
                !AssetDatabase.IsValidFolder(folderPath))
            {
                Debug.LogError("[BookContent] Selected asset is not a folder.");
                return;
            }

            var content = (BookContent)target;
            Undo.RecordObject(content, "Load Book Content From Folder");

            // Find all materials in the folder (recursive).
            var guids = AssetDatabase.FindAssets("t:Material", new[] { folderPath });
            var byName = new System.Collections.Generic.Dictionary<string, Material>(
                System.StringComparer.OrdinalIgnoreCase);
            foreach (var g in guids)
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                var m = AssetDatabase.LoadAssetAtPath<Material>(p);
                if (m != null) byName[m.name] = m;
            }

            // Helper: try multiple name variations.
            Material FindMat(params string[] candidates)
            {
                foreach (var c in candidates)
                    if (byName.TryGetValue(c, out var m)) return m;
                return null;
            }

            content.cover     = FindMat("Cover");
            content.coverPage = FindMat("CoverPage", "Cover_Page");
            content.spineFace = FindMat("SpineFace", "Spine_Face");
            content.spineText = FindMat("SpineText", "Spine_Text");
            content.pageEdges = FindMat("PageEdges", "Page_Edges");

            if (content.pages == null || content.pages.Length != 4)
                content.pages = new BookContent.PageMats[4];

            for (int i = 0; i < 4; i++)
            {
                content.pages[i] ??= new BookContent.PageMats();
                int n = i + 1;
                content.pages[i].front = FindMat(
                    $"Page{n:000}Front", $"Page_{n:000}_Front", $"Page{n}Front");
                content.pages[i].back = FindMat(
                    $"Page{n:000}Back", $"Page_{n:000}_Back", $"Page{n}Back");
            }

            EditorUtility.SetDirty(content);
            AssetDatabase.SaveAssets();

            // Report what was found.
            int foundCount = 0;
            if (content.cover) foundCount++;
            if (content.coverPage) foundCount++;
            if (content.spineFace) foundCount++;
            if (content.spineText) foundCount++;
            if (content.pageEdges) foundCount++;
            for (int i = 0; i < 4; i++)
            {
                if (content.pages[i].front) foundCount++;
                if (content.pages[i].back) foundCount++;
            }
            Debug.Log($"[BookContent] Loaded {foundCount}/13 materials from {folderPath}");
        }
    }
}
