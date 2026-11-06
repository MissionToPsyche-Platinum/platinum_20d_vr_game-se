using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace PsycheVR.EditorTools
{
    /// <summary>
    /// Renders a picture of each spacecraft puzzle piece for the puzzle info board (TG-295): the piece's own
    /// model only (no snap ghosts, letters or base plates), lit from the front-left, on a transparent
    /// background, saved as a sprite in <see cref="OutFolder"/>. Pieces are read from the Event room prefab,
    /// so the pictures always match the models visitors pick up.
    ///
    /// Menu: Tools > Puzzle > Render Piece Pictures. Batch: -executeMethod PsycheVR.EditorTools.PuzzlePiecePictures.RenderAll
    /// </summary>
    public static class PuzzlePiecePictures
    {
        private const string RoomPrefab = "Assets/Prefabs/Environment/Event_Room.prefab";
        private const string ModelsPath = "Snappable/PsycheCraft_Models/";
        private const string OutFolder = "Assets/Art/PuzzleBoard";
        private const int Size = 512;
        private const float FieldOfView = 25f;
        private const float Fill = 1.05f;   // the bounding sphere overstates a piece's outline, so let it run past the frame a little

        // piece object -> picture name, the child renderers that belong to the piece's look, and the
        // direction the camera looks (room space): a three-quarter view that shows the piece's best side
        private static readonly (string piece, string picture, string[] extras, Vector3 view)[] Pieces =
        {
            ("Psyche_Spacecraft_-_Bus", "Bus", new[] { "Antenna_Color" }, new Vector3(-0.55f, -0.45f, 1f)),
            ("Psyche_Spacecraft_-_Antenna", "Antenna", new string[0], new Vector3(-0.4f, -0.9f, 0.6f)),
            ("Psyche_Spacecraft_-_Left_Solar_Panels", "SolarPanel", new string[0], new Vector3(-1f, -0.35f, 0.45f)),
        };

        [MenuItem("Tools/Puzzle/Render Piece Pictures")]
        public static void RenderAll()
        {
            Directory.CreateDirectory(OutFolder);
            var room = PrefabUtility.LoadPrefabContents(RoomPrefab);
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                foreach (var (piece, picture, extras, view) in Pieces)
                {
                    var source = room.transform.Find(ModelsPath + piece);
                    if (source == null) { Debug.LogError($"[PuzzlePiecePictures] {piece} not found in {RoomPrefab}."); continue; }
                    Render(scene, source, extras, room.transform.TransformDirection(view), $"{OutFolder}/{picture}.png");
                }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
                PrefabUtility.UnloadPrefabContents(room);
            }
            AssetDatabase.Refresh();
            foreach (var (_, picture, _, _) in Pieces) MakeSprite($"{OutFolder}/{picture}.png");
            Debug.Log($"[PuzzlePiecePictures] Rendered {Pieces.Length} pictures into {OutFolder}.");
        }

        private static void Render(UnityEngine.SceneManagement.Scene scene, Transform source, string[] extras, Vector3 view, string path)
        {
            var holder = new GameObject("Piece");
            SceneManager_Move(holder, scene);
            Bounds bounds = default;
            bool first = true;
            foreach (var r in source.GetComponentsInChildren<MeshRenderer>(true))
            {
                bool own = r.transform == source || System.Array.IndexOf(extras, r.name) >= 0;
                if (!own || !r.enabled) continue;
                var filter = r.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                var copy = new GameObject(r.name, typeof(MeshFilter), typeof(MeshRenderer));
                copy.transform.SetParent(holder.transform, false);
                copy.transform.SetPositionAndRotation(r.transform.position, r.transform.rotation);
                copy.transform.localScale = r.transform.lossyScale;
                copy.GetComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                var mr = copy.GetComponent<MeshRenderer>();
                mr.sharedMaterials = r.sharedMaterials;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                if (first) { bounds = mr.bounds; first = false; } else bounds.Encapsulate(mr.bounds);
            }

            var key = Light(scene, new Vector3(35f, 30f, 0f), 1.3f);
            var rim = Light(scene, new Vector3(20f, 200f, 0f), 0.6f);
            var camGo = new GameObject("Camera");
            SceneManager_Move(camGo, scene);
            var cam = camGo.AddComponent<Camera>();
            cam.scene = scene;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.fieldOfView = FieldOfView;
            float radius = bounds.extents.magnitude;
            float distance = radius / Mathf.Sin(FieldOfView * 0.5f * Mathf.Deg2Rad) / Fill;
            Vector3 dir = view.normalized;
            camGo.transform.position = bounds.center - dir * distance;
            camGo.transform.rotation = Quaternion.LookRotation(dir);
            cam.nearClipPlane = Mathf.Max(0.001f, distance - radius * 2f);
            cam.farClipPlane = distance + radius * 2f;

            var rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
            cam.targetTexture = rt;
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(path, tex.EncodeToPNG());

            cam.targetTexture = null;
            rt.Release();
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(key);
            Object.DestroyImmediate(rim);
            Object.DestroyImmediate(holder);
        }

        private static GameObject Light(UnityEngine.SceneManagement.Scene scene, Vector3 euler, float intensity)
        {
            var go = new GameObject("Light");
            SceneManager_Move(go, scene);
            var l = go.AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = intensity;
            go.transform.rotation = Quaternion.Euler(euler);
            return go;
        }

        private static void SceneManager_Move(GameObject go, UnityEngine.SceneManagement.Scene scene) =>
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);

        private static void MakeSprite(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
        }
    }
}
