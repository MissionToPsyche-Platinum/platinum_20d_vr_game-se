
using System.Diagnostics;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PsycheVR.Modes.Editor
{
    public class ReleaseBuildValidator : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.Android)
                return;

            Debug.Log("[ReleaseBuildValidator] Checking Android Build...");

        }


        private void CheckPrefabs()
        {
            string[] preFabs = AssetDatabase.FindAssets(
                "t:Prefab", new[] { "Assets" });

            foreach (string f in preFabs) {
                string path = AssetDatabase.GUIDToAssetPath(f);
        }
    }
}
