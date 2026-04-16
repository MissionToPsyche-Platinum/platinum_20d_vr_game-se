using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using PsycheVR.Gameplay;

namespace PsycheVR.Editor
{
public static class BookSetupEditor
{
    private const string PrefabPath =
        "Assets/Prefabs/Props/Book/InstructionManualBook.prefab";

    // Flip order: front cover opened first, back cover last.
    private static readonly string[] PageOrder =
    {
        "Cover_Front", "Page_001", "Page_002",
        "Page_003", "Page_004", "Cover_Back"
    };

    [MenuItem("Tools/Setup Book (Lever Pattern)")]
    public static void SetupBook()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[BookSetup] Prefab not found at {PrefabPath}");
            return;
        }

        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            ZeroRootRotation(root.transform);
            var spinePivot = FindDescendant(root.transform, "SpinePivot");
            if (spinePivot == null)
            {
                Debug.LogError("[BookSetup] SpinePivot not found.");
                return;
            }

            StripOldComponents(root.transform);
            ReparentPagesUnderSpine(root.transform, spinePivot);
            var bookCollider = ConfigureSpine(spinePivot);
            var pages = ConfigurePages(spinePivot);
            ConfigurePageManager(spinePivot, pages, bookCollider);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log("[BookSetup] Lever pattern setup complete!");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ZeroRootRotation(Transform root)
    {
        if (root.localRotation == Quaternion.identity) return;

        var children = new List<(Transform t, Vector3 pos, Quaternion rot)>();
        foreach (Transform child in root)
            children.Add((child, child.position, child.rotation));

        root.localRotation = Quaternion.identity;
        root.localScale = Vector3.one;

        foreach (var (t, pos, rot) in children)
            t.SetPositionAndRotation(pos, rot);
    }

    private static void StripOldComponents(Transform root)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
            foreach (var j in t.GetComponents<Joint>())
                Object.DestroyImmediate(j);
            foreach (var ab in t.GetComponents<ArticulationBody>())
                Object.DestroyImmediate(ab);
        }

        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name.Contains("Spine")) continue;
            if (!t.name.StartsWith("Cover_") && !t.name.StartsWith("Page_")) continue;
            var rb = t.GetComponent<Rigidbody>();
            if (rb != null) Object.DestroyImmediate(rb);
        }
    }

    private static void ReparentPagesUnderSpine(Transform root, Transform spinePivot)
    {
        var toReparent = new List<Transform>();
        foreach (Transform child in root)
        {
            if (child == spinePivot) continue;
            if (child.name.StartsWith("Cover_") || child.name.StartsWith("Page_"))
                toReparent.Add(child);
        }

        foreach (var t in toReparent)
            t.SetParent(spinePivot, worldPositionStays: true);
    }

    private static BoxCollider ConfigureSpine(Transform spinePivot)
    {
        var rb = spinePivot.GetComponent<Rigidbody>();
        if (rb == null) rb = spinePivot.gameObject.AddComponent<Rigidbody>();
        rb.mass = 0.5f;
        rb.useGravity = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        var grabbable = spinePivot.GetComponent<PsycheGrabbable>();
        if (grabbable == null)
            grabbable = spinePivot.gameObject.AddComponent<PsycheGrabbable>();

        var guids = AssetDatabase.FindAssets("t:GrabSettings");
        if (guids.Length > 0)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            var settings = AssetDatabase.LoadAssetAtPath<GrabSettings>(path);
            var so = new SerializedObject(grabbable);
            so.FindProperty("grabSettings").objectReferenceValue = settings;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        else
        {
            Debug.LogWarning("[BookSetup] No GrabSettings asset found.");
        }

        foreach (var old in spinePivot.GetComponents<BoxCollider>())
            Object.DestroyImmediate(old);
        var bookCollider = spinePivot.gameObject.AddComponent<BoxCollider>();
        SizeColliderToChildren(spinePivot, bookCollider);
        SetExplicitColliders(grabbable, bookCollider);

        if (spinePivot.GetComponent<BookGravityToggle>() == null)
            spinePivot.gameObject.AddComponent<BookGravityToggle>();

        return bookCollider;
    }

    private static void SizeColliderToChildren(Transform parent, BoxCollider col)
    {
        Vector3 min = Vector3.positiveInfinity;
        Vector3 max = Vector3.negativeInfinity;

        foreach (var mf in parent.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            var bounds = mf.sharedMesh.bounds;

            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3(
                        (i & 1) == 0 ? -1f : 1f,
                        (i & 2) == 0 ? -1f : 1f,
                        (i & 4) == 0 ? -1f : 1f));
                Vector3 world = mf.transform.TransformPoint(corner);
                Vector3 local = parent.InverseTransformPoint(world);
                min = Vector3.Min(min, local);
                max = Vector3.Max(max, local);
            }
        }

        if (min.x < max.x)
        {
            col.center = (min + max) / 2f;
            col.size = max - min;
        }
    }

    private static void SetExplicitColliders(Component interactable, Collider collider)
    {
        var so = new SerializedObject(interactable);
        var prop = so.FindProperty("m_Colliders");
        prop.ClearArray();
        prop.InsertArrayElementAtIndex(0);
        prop.GetArrayElementAtIndex(0).objectReferenceValue = collider;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static List<BookPage> ConfigurePages(Transform spinePivot)
    {
        var pages = new List<BookPage>();

        foreach (var pageName in PageOrder)
        {
            var pageTransform = FindDescendant(spinePivot, pageName);
            if (pageTransform == null)
            {
                Debug.LogWarning($"[BookSetup] '{pageName}' not found under SpinePivot.");
                continue;
            }

            var box = pageTransform.GetComponent<BoxCollider>();
            if (box == null)
            {
                box = pageTransform.gameObject.AddComponent<BoxCollider>();
                var mf = pageTransform.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    box.center = mf.sharedMesh.bounds.center;
                    box.size = mf.sharedMesh.bounds.size;
                }
            }
            box.isTrigger = false;

            var bookPage = pageTransform.GetComponent<BookPage>();
            if (bookPage == null)
                bookPage = pageTransform.gameObject.AddComponent<BookPage>();

            SetZeroAngleDirection(pageTransform, bookPage);
            SetPivotOffset(pageTransform, bookPage);
            pages.Add(bookPage);
        }

        return pages;
    }

    private static void SetPivotOffset(Transform page, BookPage bookPage)
    {
        Vector3 hingeAxis = Vector3.right;
        Vector3 spineEdge = Vector3.Dot(page.localPosition, hingeAxis) * hingeAxis;
        Vector3 offsetParent = spineEdge - page.localPosition;
        Vector3 offsetLocal = Quaternion.Inverse(page.localRotation) * offsetParent;

        var so = new SerializedObject(bookPage);
        so.FindProperty("pivotOffset").vector3Value = offsetLocal;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetZeroAngleDirection(Transform page, BookPage bookPage)
    {
        var mf = page.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return;

        Vector3 localCenter = mf.sharedMesh.bounds.center;
        Vector3 dirInParent = page.localRotation * localCenter;
        float h = Vector3.Dot(dirInParent, Vector3.right);
        Vector3 projected = dirInParent - h * Vector3.right;

        if (projected.sqrMagnitude < 0.001f) return;

        var so = new SerializedObject(bookPage);
        so.FindProperty("zeroAngleDirection").vector3Value = projected.normalized;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigurePageManager(
        Transform spinePivot, List<BookPage> pages, Collider bookGrabCollider)
    {
        var manager = spinePivot.GetComponent<PageManager>();
        if (manager == null)
            manager = spinePivot.gameObject.AddComponent<PageManager>();

        var so = new SerializedObject(manager);

        var prop = so.FindProperty("pages");
        prop.ClearArray();
        for (int i = 0; i < pages.Count; i++)
        {
            prop.InsertArrayElementAtIndex(i);
            prop.GetArrayElementAtIndex(i).objectReferenceValue = pages[i];
        }

        so.FindProperty("bookGrabCollider").objectReferenceValue = bookGrabCollider;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Transform FindDescendant(Transform parent, string exactName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == exactName) return child;
            var found = FindDescendant(child, exactName);
            if (found != null) return found;
        }
        return null;
    }
}
}
