using System.IO;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace PsycheVR.Core
{
    /// <summary>
    /// Removes the one 300 ms freeze in the first minute of every session (TG-389). On Vulkan the Quest builds
    /// each shader's pipeline the first time a material is drawn, on the render thread, and a burst of new
    /// materials coming into view at once stalls a frame. This builds those pipelines before the first scene
    /// shows, from a list recorded on the headset.
    ///
    /// Warm-up: the recorded list is <c>Resources/PsycheGraphicsStates.graphicsstate</c>. It is loaded and
    /// warmed before the first scene loads, while the headset still shows the Unity splash, so the cost
    /// lands in the load time instead of in play. No list, or a list recorded for another graphics API: nothing
    /// happens.
    ///
    /// Recording: on the headset, create the empty file <see cref="TraceFlagName"/> in the app's files folder
    /// (<c>/sdcard/Android/data/&lt;package&gt;/files/</c>) and start the app. Every pipeline the session builds is
    /// added to <see cref="TraceFileName"/> in the same folder, which is saved whenever the headset is taken
    /// off or the app pauses or quits. Sessions add to the same file, so play both modes once. Pull the file
    /// into Assets/Resources as PsycheGraphicsStates.graphicsstate and delete the flag.
    /// </summary>
    public static class GraphicsWarmUp
    {
        private const string LogPrefix = "[GraphicsWarmUp]";

        /// <summary>Name of the recorded list under a Resources folder (file extension .graphicsstate).</summary>
        public const string ResourceName = "PsycheGraphicsStates";

        /// <summary>Empty file in persistentDataPath that turns recording on.</summary>
        public const string TraceFlagName = "trace-graphics-states";

        /// <summary>File in persistentDataPath that recording writes.</summary>
        public const string TraceFileName = "PsycheGraphicsStates.graphicsstate";

        private static GraphicsStateCollection s_Trace;
        private static string s_TracePath;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            if (Application.isEditor) return;   // the editor compiles shaders its own way; nothing to gain there

            WarmUp();

            string flag = Path.Combine(Application.persistentDataPath, TraceFlagName);
            if (File.Exists(flag)) StartTrace();
        }

        /// <summary>Builds every pipeline in the recorded list now. Blocks until done.</summary>
        private static void WarmUp()
        {
            var states = Resources.Load<GraphicsStateCollection>(ResourceName);
            if (states == null) return;
            if (states.graphicsDeviceType != SystemInfo.graphicsDeviceType)
            {
                Debug.Log($"{LogPrefix} List is for {states.graphicsDeviceType}, running {SystemInfo.graphicsDeviceType}: skipped.");
                return;
            }

            float started = Time.realtimeSinceStartup;
            states.WarmUp().Complete();
            Debug.Log($"{LogPrefix} Warmed {states.totalGraphicsStateCount} pipeline states in {(Time.realtimeSinceStartup - started) * 1000f:F0} ms.");
        }

        private static void StartTrace()
        {
            s_TracePath = Path.Combine(Application.persistentDataPath, TraceFileName);
            s_Trace = new GraphicsStateCollection();
            if (File.Exists(s_TracePath) && !s_Trace.LoadFromFile(s_TracePath))
                Debug.LogWarning($"{LogPrefix} Could not read {s_TracePath}; starting a new list.");

            if (!s_Trace.BeginTrace())
            {
                Debug.LogWarning($"{LogPrefix} Recording did not start.");
                s_Trace = null;
                return;
            }

            Application.focusChanged += focused => { if (!focused) Save(); };
            Application.quitting += Save;
            Debug.Log($"{LogPrefix} Recording pipeline states to {s_TracePath} (starting from {s_Trace.totalGraphicsStateCount}).");
        }

        /// <summary>Writes what has been recorded so far. Recording carries on afterwards.</summary>
        private static void Save()
        {
            if (s_Trace == null) return;
            s_Trace.EndTrace();
            bool saved = s_Trace.SaveToFile(s_TracePath);
            Debug.Log($"{LogPrefix} {(saved ? "Saved" : "FAILED to save")} {s_Trace.totalGraphicsStateCount} pipeline states to {s_TracePath}.");
            s_Trace.BeginTrace();
        }
    }
}
