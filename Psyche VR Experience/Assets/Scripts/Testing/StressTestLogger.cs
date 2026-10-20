using System;
using System.Globalization;
using System.IO;
using UnityEngine;
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
            "ResetStart,ResetEnd,ResetDurationSeconds,TotalElapsedSeconds," +
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
        StressTestMetrics.Snapshot baseline,
        DateTime resetStart,
        DateTime resetEnd,
        double resetDurationSeconds,
        double totalElapsedSeconds)

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

            // Timing
            resetStart.ToString("O"),
            resetEnd.ToString("O"),
            resetDurationSeconds.ToString("F3", CultureInfo.InvariantCulture),
            totalElapsedSeconds.ToString("F3", CultureInfo.InvariantCulture),

            // Object counts
            current.TotalObjects.ToString(),
            current.SceneObjects.ToString(),
            current.PersistentObjects.ToString(),

            // Memory measurements
            ToMiB(current.ManagedMemoryBytes),
            ToMiB(current.AllocatedMemoryBytes),
            ToMiB(current.ReservedMemoryBytes),

            // Object changes
            (current.TotalObjects - previous.TotalObjects).ToString(),
            (current.TotalObjects - baseline.TotalObjects).ToString(),

            // Persistent object changes
            (current.PersistentObjects - previous.PersistentObjects).ToString(),
            (current.PersistentObjects - baseline.PersistentObjects).ToString(),

            // Memory changes
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
