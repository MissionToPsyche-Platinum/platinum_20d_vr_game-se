using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;

public class StressTestMetrics : MonoBehaviour
{
    public struct Snapshot
    {
        public int TotalObjects;
        public int SceneObjects;
        public int PersistentObjects;
        public long ManagedMemoryBytes;
        public long AllocatedMemoryBytes;
        public long ReservedMemoryBytes;
    }

    public Snapshot Capture()
    {
        Snapshot snapshot = new Snapshot();

        GameObject[] objects =
            Resources.FindObjectsOfTypeAll<GameObject>();

        Scene activeScene = SceneManager.GetActiveScene();

        foreach (GameObject obj in objects)
        {
            if (!obj.scene.IsValid())
                continue;

            // Exclude prefab assets and objects outside loaded scenes.
            if (!obj.scene.isLoaded)
                continue;

            snapshot.TotalObjects++;

            if (obj.scene == activeScene)
            {
                snapshot.SceneObjects++;
            }
            else if (obj.scene.name == "DontDestroyOnLoad")
            {
                snapshot.PersistentObjects++;
            }
        }

        snapshot.ManagedMemoryBytes =
            System.GC.GetTotalMemory(false);

        snapshot.AllocatedMemoryBytes =
            Profiler.GetTotalAllocatedMemoryLong();

        snapshot.ReservedMemoryBytes =
            Profiler.GetTotalReservedMemoryLong();

        return snapshot;
    }
}
