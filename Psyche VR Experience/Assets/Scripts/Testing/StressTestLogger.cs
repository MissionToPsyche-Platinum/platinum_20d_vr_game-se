using System;
using System.Globalization;
using System.IO;
using UnityEngine;
using static System.Net.Mime.MediaTypeNames;
using Application = UnityEngine.Application;

public class StressTestLogger : MonoBehaviour
{
    private string logPath;
    private bool initialized;

    private const double BytesToMiB = 1024.0 * 1024.0;

    public void Initialize()
    {
        if (initialized)
            return;

        string logDirectory = Path.Combine(
            Application.persistentDataPath,
            "StressTestLogs"
        );

        Directory.CreateDirectory(logDirectory);

        string fileName =
            $"StressTest_{DateTime.Now:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid():N}.csv";

        logPath = Path.Combine(logDirectory, fileName);

        string header =
            "Timestamp,Reset,Phase," +
            "TotalObjects,SceneObjects,PersistentObjects," +
            "ManagedMemoryMiB,AllocatedMemoryMiB,ReservedMemoryMiB," +
            "ObjectDelta,ObjectGrowth," +
            "PersistentDelta,PersistentGrowth," +
            "AllocatedDeltaMiB,AllocatedGrowthMiB";

        File.WriteAllText(logPath, header + Environment.NewLine);

        initialized = true;

        Debug.Log($"[StressTestLogger] Logging to: {logPath}");
    }

    public void Record(
        int reset,
        string phase,
        StressTestMetrics.Snapshot current,
        StressTestMetrics.Snapshot previous,
        StressTestMetrics.Snapshot baseline)
    {
        if (!initialized)
        {
            Debug.LogError("[StressTestLogger] Logger not initialized.");
            return;
        }

        string row = string.Join(",",
            DateTime.Now.ToString("O"),
            reset.ToString(),
            phase,
            current.TotalObjects.ToString(),
            current.SceneObjects.ToString(),
            current.PersistentObjects.ToString(),

            ToMiB(current.ManagedMemoryBytes),
            ToMiB(current.AllocatedMemoryBytes),
            ToMiB(current.ReservedMemoryBytes),

            (current.TotalObjects - previous.TotalObjects).ToString(),
            (current.TotalObjects - baseline.TotalObjects).ToString(),

            (current.PersistentObjects - previous.PersistentObjects).ToString(),
            (current.PersistentObjects - baseline.PersistentObjects).ToString(),

            ToMiB(current.AllocatedMemoryBytes -
                  previous.AllocatedMemoryBytes),

            ToMiB(current.AllocatedMemoryBytes -
                  baseline.AllocatedMemoryBytes)
        );

        File.AppendAllText(logPath, row + Environment.NewLine);
    }

    private static string ToMiB(double bytes)
    {
        return (bytes / BytesToMiB).ToString(
            "F3",
            CultureInfo.InvariantCulture
        );
    }

    public string GetLogPath()
    {
        return logPath;
    }
}
