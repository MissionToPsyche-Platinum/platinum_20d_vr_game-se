using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections.Generic;

public static class BuildFistAnims
{
    static float PCurl = 55f;
    static float ICurl = 55f;
    static float DCurl = 40f;
    static float TMCurl = 15f;
    static float TPCurl = 25f;
    static float TDCurl = 20f;

    [MenuItem("Tools/Build Fist Anims")]
    public static void Run()
    {
        BuildHand(
            "Assets/Prefabs/VR/Hands/LeftHandModel.prefab", "L_",
            "Assets/Art/Animations/Hands/Left_Idle.anim",
            "Assets/Art/Animations/Hands/Left_Fist.anim",
            "Assets/Art/Animations/Hands/LeftHandAnimController.controller"
        );
        BuildHand(
            "Assets/Prefabs/VR/Hands/RightHandModel.prefab", "R_",
            "Assets/Art/Animations/Hands/Right_Idle.anim",
            "Assets/Art/Animations/Hands/Right_Fist.anim",
            "Assets/Art/Animations/Hands/RightHandAnimController.controller"
        );
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[BuildFistAnims] Done.");
    }

    static void BuildHand(string prefabPath, string p,
        string idlePath, string fistPath, string ctrlPath)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null) { Debug.LogError("[Build] No prefab: " + prefabPath); return; }
        var wrist = prefab.transform.Find(p + "Wrist");
        if (wrist == null) { Debug.LogError("[Build] No wrist"); return; }

        var bones = new Dictionary<string, Transform>();
        Collect(wrist, p + "Wrist", bones);

        var idle = new AnimationClip();
        idle.name = System.IO.Path.GetFileNameWithoutExtension(idlePath);
        idle.frameRate = 60;
        SetEuler(idle, p + "Wrist", wrist.localEulerAngles);
        SaveClip(idle, idlePath);

        var fist = new AnimationClip();
        fist.name = System.IO.Path.GetFileNameWithoutExtension(fistPath);
        fist.frameRate = 60;
        string[] fingers = { "Index", "Middle", "Ring", "Little" };
        foreach (var f in fingers) CurlFinger(fist, p, f, bones);
        CurlThumb(fist, p, bones);
        SaveClip(fist, fistPath);

        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath);
        if (ctrl == null)
        {
            ctrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
            ctrl.AddParameter("Fist", AnimatorControllerParameterType.Float);
            var layers = ctrl.layers;
            layers[0].name = "Fist";
            ctrl.layers = layers;

            var tree = new BlendTree();
            tree.name = "FistBlend";
            tree.blendParameter = "Fist";
            tree.blendType = BlendTreeType.Simple1D;
            tree.useAutomaticThresholds = false;
            AssetDatabase.AddObjectToAsset(tree, ctrl);

            var sm = ctrl.layers[0].stateMachine;
            var state = sm.AddState("FistBlend");
            state.motion = tree;
            sm.defaultState = state;
        }

        // Update blend tree clip references in-place
        var existingTree = ctrl.layers[0].stateMachine.defaultState.motion as BlendTree;
        if (existingTree != null)
        {
            var idleClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(idlePath);
            var fistClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(fistPath);
            var children = existingTree.children;
            children[0].motion = idleClip;
            children[0].threshold = 0f;
            children[1].motion = fistClip;
            children[1].threshold = 1f;
            existingTree.children = children;
        }
        EditorUtility.SetDirty(ctrl);
        Debug.Log("[Build] " + p + " done.");
    }

    static void CurlFinger(AnimationClip clip, string p, string finger, Dictionary<string, Transform> bones)
    {
        string meta = p + "Wrist/" + p + finger + "Metacarpal";
        string prox = meta + "/" + p + finger + "Proximal";
        string inter = prox + "/" + p + finger + "Intermediate";
        string dist = inter + "/" + p + finger + "Distal";
        Transform b;
        if (bones.TryGetValue(prox, out b))
            SetEuler(clip, prox, V3(b.localEulerAngles.x + PCurl, b.localEulerAngles.y, b.localEulerAngles.z));
        if (bones.TryGetValue(inter, out b))
            SetEuler(clip, inter, V3(b.localEulerAngles.x + ICurl, b.localEulerAngles.y, b.localEulerAngles.z));
        if (bones.TryGetValue(dist, out b))
            SetEuler(clip, dist, V3(b.localEulerAngles.x + DCurl, b.localEulerAngles.y, b.localEulerAngles.z));
    }

    static void CurlThumb(AnimationClip clip, string p, Dictionary<string, Transform> bones)
    {
        string meta = p + "Wrist/" + p + "ThumbMetacarpal";
        string prox = meta + "/" + p + "ThumbProximal";
        string dist = prox + "/" + p + "ThumbDistal";
        Transform b;
        if (bones.TryGetValue(meta, out b))
            SetEuler(clip, meta, V3(b.localEulerAngles.x + TMCurl, b.localEulerAngles.y, b.localEulerAngles.z));
        if (bones.TryGetValue(prox, out b))
            SetEuler(clip, prox, V3(b.localEulerAngles.x + TPCurl, b.localEulerAngles.y, b.localEulerAngles.z));
        if (bones.TryGetValue(dist, out b))
            SetEuler(clip, dist, V3(b.localEulerAngles.x + TDCurl, b.localEulerAngles.y, b.localEulerAngles.z));
    }

    static void SetEuler(AnimationClip clip, string path, Vector3 e)
    {
        clip.SetCurve(path, typeof(Transform), "localEulerAngles.x", AnimationCurve.Constant(0, 0, e.x));
        clip.SetCurve(path, typeof(Transform), "localEulerAngles.y", AnimationCurve.Constant(0, 0, e.y));
        clip.SetCurve(path, typeof(Transform), "localEulerAngles.z", AnimationCurve.Constant(0, 0, e.z));
    }

    static Vector3 V3(float x, float y, float z) { return new Vector3(x, y, z); }

    static void Collect(Transform t, string path, Dictionary<string, Transform> map)
    {
        map[path] = t;
        foreach (Transform child in t) Collect(child, path + "/" + child.name, map);
    }

    static void SaveClip(AnimationClip clip, string path)
    {
        var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (existing != null)
        {
            EditorUtility.CopySerialized(clip, existing);
            EditorUtility.SetDirty(existing);
        }
        else
            AssetDatabase.CreateAsset(clip, path);
    }
}
