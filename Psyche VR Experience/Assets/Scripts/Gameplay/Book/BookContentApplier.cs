using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PsycheVR.Gameplay
{
    /// <summary>
    /// Applies a BookContent ScriptableObject to all renderers under the book.
    /// Handles the per-page sub-mesh ordering quirk so consumers only need to
    /// supply Front/Back per page.
    ///
    /// Drop this on the InstructionManualBook prefab root, assign a BookContent,
    /// and materials will be applied automatically (also updates live when the
    /// content asset changes in the editor).
    /// </summary>
    [ExecuteAlways]
    public class BookContentApplier : MonoBehaviour
    {
        [Tooltip("ScriptableObject defining all materials for this book instance.")]
        [SerializeField] private BookContent content;

        public BookContent Content
        {
            get => content;
            set { content = value; Apply(); }
        }

        private void OnEnable()
        {
            // Defer in editor so the hierarchy is fully ready.
#if UNITY_EDITOR
            EditorApplication.delayCall += DelayedApply;
#else
            Apply();
#endif
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // OnValidate can run before children are accessible / before the
            // serialized object change is committed. Defer to the next editor tick.
            EditorApplication.delayCall += DelayedApply;
        }

        private void DelayedApply()
        {
            // Object may have been destroyed between the delay and the call.
            if (this == null) return;
            Apply();
        }
#endif

        [ContextMenu("Apply Content")]
        public void Apply()
        {
            if (content == null)
            {
                Debug.LogWarning($"[BookContentApplier] No content assigned on '{name}'.", this);
                return;
            }

            int applied = 0;

            // Cover materials
            applied += SetMaterials("Cover_Front", content.cover);
            applied += SetMaterials("Cover_Back",  content.cover, content.coverPage);

            // Spine materials
            applied += SetMaterials("SpineMesh", content.spineFace, content.spineText);

            // Page materials — sub-mesh order is per-page (FBX export quirk):
            //   Page_001 mesh: [Front, PageEdges, Back]
            //   Page_002 mesh: [PageEdges, Front, Back]
            //   Page_003 mesh: [PageEdges, Front, Back]
            //   Page_004 mesh: [Back, PageEdges, Front]
            if (content.pages != null && content.pages.Length >= 4)
            {
                if (content.pages[0] != null)
                    applied += SetMaterials("Page_001",
                        content.pages[0].front, content.pageEdges, content.pages[0].back);
                if (content.pages[1] != null)
                    applied += SetMaterials("Page_002",
                        content.pageEdges, content.pages[1].front, content.pages[1].back);
                if (content.pages[2] != null)
                    applied += SetMaterials("Page_003",
                        content.pageEdges, content.pages[2].front, content.pages[2].back);
                if (content.pages[3] != null)
                    applied += SetMaterials("Page_004",
                        content.pages[3].back, content.pageEdges, content.pages[3].front);
            }

            // Verbose summary so it's easy to verify which materials were assigned.
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[BookContentApplier] Applied '{content.name}' to '{name}' ({applied} renderers):");
            LogRendererMats(sb, "Cover_Front");
            LogRendererMats(sb, "Cover_Back");
            LogRendererMats(sb, "SpineMesh");
            LogRendererMats(sb, "Page_001");
            LogRendererMats(sb, "Page_002");
            LogRendererMats(sb, "Page_003");
            LogRendererMats(sb, "Page_004");
            Debug.Log(sb.ToString(), this);
        }

        private void LogRendererMats(System.Text.StringBuilder sb, string childName)
        {
            var t = FindChildRecursive(transform, childName);
            var rend = t != null ? t.GetComponent<Renderer>() : null;
            if (rend == null) { sb.AppendLine($"  {childName}: <missing>"); return; }
            var mats = rend.sharedMaterials;
            var names = new string[mats.Length];
            for (int i = 0; i < mats.Length; i++)
                names[i] = mats[i] != null ? mats[i].name : "null";
            sb.AppendLine($"  {childName}: [{string.Join(", ", names)}]");
        }

        private int SetMaterials(string childName, params Material[] mats)
        {
            var t = FindChildRecursive(transform, childName);
            if (t == null)
            {
                Debug.LogWarning($"[BookContentApplier] Child '{childName}' not found under '{name}'.", this);
                return 0;
            }

            var rend = t.GetComponent<Renderer>();
            if (rend == null)
            {
                Debug.LogWarning($"[BookContentApplier] No Renderer on '{childName}'.", this);
                return 0;
            }

#if UNITY_EDITOR
            // Record an undo step + mark scene dirty so the change persists.
            Undo.RecordObject(rend, "Apply Book Content");
#endif
            rend.sharedMaterials = mats;
#if UNITY_EDITOR
            EditorUtility.SetDirty(rend);
            if (!Application.isPlaying && rend.gameObject.scene.IsValid())
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                    rend.gameObject.scene);
            }
#endif
            return 1;
        }

        private static Transform FindChildRecursive(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var c = parent.GetChild(i);
                if (c.name == name) return c;
                var nested = FindChildRecursive(c, name);
                if (nested != null) return nested;
            }
            return null;
        }
    }
}
