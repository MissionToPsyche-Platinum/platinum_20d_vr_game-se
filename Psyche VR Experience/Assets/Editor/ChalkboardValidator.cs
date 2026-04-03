using UnityEngine;
using UnityEditor;
using PsycheVR.UI;

/// <summary>
/// Editor tool to validate chalkboard display system setup.
/// Run via Tools > Psyche VR > Validate Chalkboard Setup.
/// </summary>
public class ChalkboardValidator
{
    [MenuItem("Tools/Psyche VR/Validate Chalkboard Setup")]
    public static void Validate()
    {
        int issues = 0;

        // Check for Chalkboard
        var boards = Object.FindObjectsByType<Chalkboard>(FindObjectsSortMode.None);
        if (boards.Length == 0)
        {
            Debug.LogWarning("[Validator] No Chalkboard found in scene. Add one and tag it 'Chalkboard'.");
            issues++;
        }
        else if (boards.Length > 1)
        {
            Debug.LogWarning($"[Validator] Found {boards.Length} Chalkboards. There should be exactly 1.");
            issues++;
        }

        // Check Chalkboard tag
        var tagged = GameObject.FindGameObjectWithTag("Chalkboard");
        if (tagged == null && boards.Length > 0)
        {
            Debug.LogWarning("[Validator] Chalkboard exists but is not tagged 'Chalkboard'. ComponentInfo won't find it.");
            issues++;
        }

        // Check ComponentInfo pieces
        var pieces = Object.FindObjectsByType<ComponentInfo>(FindObjectsSortMode.None);
        if (pieces.Length == 0)
        {
            Debug.LogWarning("[Validator] No ComponentInfo pieces found in scene.");
            issues++;
        }

        foreach (var piece in pieces)
        {
            if (piece.Data == null)
            {
                Debug.LogError($"[Validator] {piece.gameObject.name} has no ComponentData assigned.", piece);
                issues++;
            }

            if (piece.GetComponent<Collider>() == null)
            {
                Debug.LogWarning($"[Validator] {piece.gameObject.name} has no Collider. Mouse click won't work.", piece);
                issues++;
            }
        }

        if (issues == 0)
            Debug.Log($"[Validator] Chalkboard setup OK. {boards.Length} board, {pieces.Length} pieces.");
        else
            Debug.LogWarning($"[Validator] Found {issues} issue(s). See above.");
    }
}
