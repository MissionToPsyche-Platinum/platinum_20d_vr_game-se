using UnityEngine;

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

        private void OnEnable() => Apply();

        // OnValidate fires when fields change in the inspector — gives live update.
        private void OnValidate() => Apply();

        [ContextMenu("Apply Content")]
        public void Apply()
        {
            if (content == null) return;

            // Cover materials
            SetMaterials("Cover_Front", content.cover);
            SetMaterials("Cover_Back",  content.cover, content.coverPage);

            // Spine materials
            SetMaterials("SpineMesh", content.spineFace, content.spineText);

            // Page materials — sub-mesh order is per-page (FBX export quirk):
            //   Page_001 mesh: [Front, PageEdges, Back]
            //   Page_002 mesh: [PageEdges, Front, Back]
            //   Page_003 mesh: [PageEdges, Front, Back]
            //   Page_004 mesh: [Back, PageEdges, Front]
            if (content.pages != null && content.pages.Length >= 4)
            {
                if (content.pages[0] != null)
                    SetMaterials("Page_001",
                        content.pages[0].front, content.pageEdges, content.pages[0].back);
                if (content.pages[1] != null)
                    SetMaterials("Page_002",
                        content.pageEdges, content.pages[1].front, content.pages[1].back);
                if (content.pages[2] != null)
                    SetMaterials("Page_003",
                        content.pageEdges, content.pages[2].front, content.pages[2].back);
                if (content.pages[3] != null)
                    SetMaterials("Page_004",
                        content.pages[3].back, content.pageEdges, content.pages[3].front);
            }
        }

        private void SetMaterials(string childName, params Material[] mats)
        {
            // Find the child by name anywhere under this transform.
            var t = FindChildRecursive(transform, childName);
            if (t == null) return;

            var rend = t.GetComponent<Renderer>();
            if (rend == null) return;

            rend.sharedMaterials = mats;
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
