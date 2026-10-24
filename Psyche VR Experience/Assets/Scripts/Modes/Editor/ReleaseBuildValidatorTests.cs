using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using PsycheVR.Modes.Editor;
using UnityEngine;

public class ReleaseBuildValidatorTests
{
    private static List<string> Check(GameObject root)
    {
        var errors = new List<string>();
        var method = typeof(ReleaseBuildValidator).GetMethod(
            "CheckHierarchy", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(method);
        method.Invoke(null, new object[] { root, "Assets/Prefabs/ValidationTest.prefab", errors });
        return errors;
    }

    [Test]
    public void BrokenMaterialOnChild_IsReportedWithObjectPath()
    {
        var root = new GameObject("Root");
        try
        {
            var child = new GameObject("Child");
            child.transform.SetParent(root.transform);
            var renderer = child.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new Material[] { null };
            var errors = Check(root);
            Assert.That(errors, Has.Some.Contains("Root/Child"));
            Assert.That(errors, Has.Some.Contains("missing material"));
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void EmptyGameObject_DoesNotReportErrors()
    {
        var root = new GameObject("Empty");
        try { Assert.IsEmpty(Check(root)); }
        finally { Object.DestroyImmediate(root); }
    }
}
