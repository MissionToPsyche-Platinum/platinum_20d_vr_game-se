using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Video;
using PsycheVR.Gameplay;

namespace PsycheVR.EditorTools
{
    /// <summary>
    /// Builds a bookcase book from a folder of page images (TG-180): sets the images' import settings,
    /// makes the 13 materials by copying Journey to a Metal World's (same shader and settings, new
    /// textures) and creates the BookContent asset next to the folder, named after it.
    ///
    /// The folder holds the files the page generator writes (docs/superpowers/scripts/books):
    /// cover_page.png, page_00N_front.png / page_00N_back.png for N = 1..4, and spine_text.png, plus an
    /// optional book.json naming a page video (see <see cref="BookJson"/>). The solid cover and spine faces
    /// take the colour of the cover image's top-left corner. Running it again on the same folder updates
    /// everything in place, so asset GUIDs stay stable.
    ///
    /// Menu: select the folder, then Tools > Books > Build Book From Selected Folder.
    /// Batch: -executeMethod PsycheVR.EditorTools.BookBuilder.BuildFromCommandLine -bookFolder Assets/Art/Books/X
    /// </summary>
    public static class BookBuilder
    {
        private const string TemplateFolder = "Assets/Art/Books/JourneyToAMetalWorld";
        private const string MainTexture = "_MainTexture";
        private const string BaseColor = "_BaseColor";
        private const int AnisoLevel = 16;   // pages are read at an angle; matches Journey's import
        private const int CornerInset = 8;   // px in from the corner when sampling the cover colour

        // material name -> image file stem; null = a solid material with no image
        private static readonly (string material, string image)[] Slots =
        {
            ("Cover", null), ("CoverPage", "cover_page"), ("SpineFace", null), ("SpineText", "spine_text"),
            ("PageEdges", null),
            ("Page001Front", "page_001_front"), ("Page001Back", "page_001_back"),
            ("Page002Front", "page_002_front"), ("Page002Back", "page_002_back"),
            ("Page003Front", "page_003_front"), ("Page003Back", "page_003_back"),
            ("Page004Front", "page_004_front"), ("Page004Back", "page_004_back"),
        };

        /// <summary>book.json, written by the page generator: the page video and where it plays.</summary>
        [System.Serializable]
        private class BookJson
        {
            public string video;      // file name in the folder
            public int page = 1;      // inner page 1-4
            public bool front = true; // that page's front face, or its back
            public float[] window;    // x, y, width, height in page UV, origin bottom left
        }

        [MenuItem("Tools/Books/Build Book From Selected Folder")]
        private static void BuildFromSelection()
        {
            string folder = Selection.activeObject != null ? AssetDatabase.GetAssetPath(Selection.activeObject) : null;
            if (string.IsNullOrEmpty(folder) || !AssetDatabase.IsValidFolder(folder))
            {
                EditorUtility.DisplayDialog("Build Book", "Select a book's image folder in the Project window first.", "OK");
                return;
            }
            var content = Build(folder);
            if (content != null) Selection.activeObject = content;
        }

        /// <summary>Batch entry point: builds the folder given after -bookFolder; exits 1 on failure.</summary>
        public static void BuildFromCommandLine()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-bookFolder");
            string folder = i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
            bool ok = folder != null && Build(folder) != null;
            if (!ok) Debug.LogError("[BookBuilder] FAIL: pass -bookFolder Assets/Art/Books/<Name> with the page images in it.");
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        /// <summary>Builds the book in <paramref name="folder"/>; returns its BookContent, or null when an image is missing.</summary>
        public static BookContent Build(string folder)
        {
            foreach (var (_, image) in Slots)
            {
                if (image != null && !File.Exists(ImagePath(folder, image)))
                {
                    Debug.LogError($"[BookBuilder] {folder}: missing {image}.png.");
                    return null;
                }
            }

            foreach (var (_, image) in Slots)
                if (image != null) SetImport(ImagePath(folder, image));

            Color coverColour = CornerColour(ImagePath(folder, "cover_page"));
            foreach (var (material, image) in Slots)
            {
                var mat = CopyTemplate(folder, material);
                if (mat == null) return null;
                if (image != null)
                    mat.SetTexture(MainTexture, AssetDatabase.LoadAssetAtPath<Texture2D>(ImagePath(folder, image)));
                else if (material != "PageEdges")
                    mat.SetColor(BaseColor, coverColour);
                EditorUtility.SetDirty(mat);
            }

            string assetPath = folder + ".asset";
            var content = AssetDatabase.LoadAssetAtPath<BookContent>(assetPath);
            if (content == null)
            {
                content = ScriptableObject.CreateInstance<BookContent>();
                AssetDatabase.CreateAsset(content, assetPath);
            }
            int found = BookContentEditor.AssignFromFolder(content, folder);
            if (!ApplyVideo(content, folder)) return null;
            AssetDatabase.SaveAssets();
            string video = content.pageVideo != null ? $", video on page {content.videoPage} ({content.pageVideo.length:F1} s)" : "";
            Debug.Log($"[BookBuilder] Built {assetPath}: {found}/13 materials, cover colour #{ColorUtility.ToHtmlStringRGB(coverColour)}{video}.");
            return found == Slots.Length ? content : null;
        }

        private static string ImagePath(string folder, string stem) => $"{folder}/{stem}.png";

        /// <summary>Sets or clears the page video from book.json; false when book.json names a video that is not there.</summary>
        private static bool ApplyVideo(BookContent content, string folder)
        {
            string jsonPath = $"{folder}/book.json";
            var book = File.Exists(jsonPath) ? JsonUtility.FromJson<BookJson>(File.ReadAllText(jsonPath)) : null;
            if (book == null || string.IsNullOrEmpty(book.video))
            {
                content.pageVideo = null;
                return true;
            }
            string clipPath = $"{folder}/{book.video}";
            if (AssetImporter.GetAtPath(clipPath) is VideoClipImporter importer && importer.importAudio)
            {
                importer.importAudio = false;   // page videos are silent
                importer.SaveAndReimport();
            }
            var clip = AssetDatabase.LoadAssetAtPath<VideoClip>(clipPath);
            if (clip == null || book.window == null || book.window.Length != 4)
            {
                Debug.LogError($"[BookBuilder] {jsonPath}: video '{book.video}' missing or window not 4 numbers.");
                return false;
            }
            content.pageVideo = clip;
            content.videoPage = book.page;
            content.videoOnFront = book.front;
            content.videoWindow = new Rect(book.window[0], book.window[1], book.window[2], book.window[3]);
            EditorUtility.SetDirty(content);
            return true;
        }

        /// <summary>Journey's import: mipmapped sRGB, trilinear, anisotropic, no wider than 2048.</summary>
        private static void SetImport(string path)
        {
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = true;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = AnisoLevel;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }

        /// <summary>Copies the template's material of the same name into the folder, or reuses an earlier copy.</summary>
        private static Material CopyTemplate(string folder, string material)
        {
            string target = $"{folder}/{material}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(target);
            if (existing != null) return existing;
            string source = $"{TemplateFolder}/{material}.mat";
            if (!AssetDatabase.CopyAsset(source, target))
            {
                Debug.LogError($"[BookBuilder] could not copy {source} to {target}.");
                return null;
            }
            return AssetDatabase.LoadAssetAtPath<Material>(target);
        }

        /// <summary>
        /// The cover image's top-left colour, read from the PNG file itself so the texture need not be readable.
        /// Left in sRGB: Material.SetColor takes sRGB colours, like the colour picker.
        /// </summary>
        private static Color CornerColour(string path)
        {
            var tex = new Texture2D(2, 2);
            tex.LoadImage(File.ReadAllBytes(path));
            Color c = tex.GetPixel(CornerInset, tex.height - 1 - CornerInset);
            Object.DestroyImmediate(tex);
            return c;
        }
    }
}
