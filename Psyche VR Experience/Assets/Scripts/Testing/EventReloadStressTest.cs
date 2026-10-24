using PsycheVR.Modes;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

/*
 * STRESS TESTING MECHANISM
 *
 * Tests scene reload times, memory allocation, GameObject counts,
 * and other performance metrics across repeated Event mode resets.
 *
 * TO RUN:
 * Add the EventStressTestController prefab to the scene
 * and enter Play Mode normally.
 *
 * *** IMPORTANT ***
 * This test suppresses normal session logging.
 *
 * To restore normal session logging, REMOVE the
 * EventStressTestController prefab instance from the scene.
 *
 * ALWAYS REMOVE THE PREFAB INSTANCE BEFORE BUILDING THE APK.
 */

public class EventReloadStressTest : MonoBehaviour
{

    [SerializeField] private int totalResets = 5;
    [SerializeField] private float delayBetweenResets = 2f;
    [SerializeField] private float stabilizationDelay = 1f;

    private StressTestMetrics metrics;
    private StressTestLogger logger;
    private int completedResets;
    private bool waitingForScene;
    private DateTime testStart;
    private DateTime testEnd;
    private DateTime resetStart;
    private DateTime resetEnd;
    private Stopwatch timer = new Stopwatch();
    private Stopwatch resetTimer = new Stopwatch();
    public static bool IsStressTestRunning { get; private set;}
    private static EventReloadStressTest instance;

    private StressTestMetrics.Snapshot previousSnapshot;
    private StressTestMetrics.Snapshot baselineSnapshot;

    private bool hasBaseline = false;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        metrics = GetComponent<StressTestMetrics>();

        if (metrics == null)
        {
            Debug.LogError("[StressTest] StressTestMetrics component missing.");
            enabled = false;
            return;
        }

        logger = GetComponent<StressTestLogger>();

        if (logger == null)
        {
            Debug.LogError("[StressTest] StressTestLogger component missing.");
            enabled = false;
            return;
        }

        logger.Initialize();
    }

    private void OnEnable()
    {
        if (instance != this)
            return;

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        if (instance != this)
            return;
        IsStressTestRunning = true;
        StartCoroutine(RunStressTest());
    }

    private IEnumerator RunStressTest()
    {
        testStart = DateTime.Now;
        timer.Start();
        Debug.Log($"[StressTest] Test started at: {testStart:yyyy-MM-dd HH:mm:ss}");
        while (completedResets < totalResets)
        {
            yield return new WaitForSecondsRealtime(delayBetweenResets);

            waitingForScene = true;

            Debug.Log(
                $"[StressTest] Starting reset {completedResets + 1}/{totalResets}"
            );
            resetStart = DateTime.Now;
            resetTimer.Restart();
            GameModeManager.SwitchTo(GameMode.Event);

            yield return new WaitUntil(() => !waitingForScene);
            // Allow the newly loaded scene to finish initializing.
            yield return new WaitForSecondsRealtime(stabilizationDelay);


            resetEnd = DateTime.Now;
            resetTimer.Stop();

            // Capture measurements after the reset.
            StressTestMetrics.Snapshot snapshot = metrics.Capture();

            if (!hasBaseline)
            {
                baselineSnapshot = snapshot;
                previousSnapshot = snapshot;
                hasBaseline = true;

                logger.Record(
                    completedResets,
                    "Baseline",
                    snapshot,
                    snapshot,
                    snapshot,
                    resetStart,
                    resetEnd,
                    resetTimer.Elapsed.TotalSeconds,
                    timer.Elapsed.TotalSeconds
                );

                Debug.Log("[StressTest] Baseline established.");
            }
            else
            {
                logger.Record(
                    completedResets,
                    "Measured",
                    snapshot,
                    previousSnapshot,
                    baselineSnapshot,
                    resetStart,
                    resetEnd,
                    resetTimer.Elapsed.TotalSeconds,
                    timer.Elapsed.TotalSeconds
                );
            }

            Debug.Log(
                $"[StressTest] Reset {completedResets}/{totalResets}\n" +
                $"Start: {resetStart:yyyy-MM-dd HH:mm:ss}\n" +
                $"End: {resetEnd:yyyy-MM-dd HH:mm:ss}\n" +
                $"Duration: {resetTimer.Elapsed.TotalSeconds:F2} seconds\n" +
                $"Total Objects: {snapshot.TotalObjects} " +
                $"(Previous: {snapshot.TotalObjects - previousSnapshot.TotalObjects:+#;-#;0}, " +
                $"Baseline: {snapshot.TotalObjects - baselineSnapshot.TotalObjects:+#;-#;0})\n" +
                $"Scene Objects: {snapshot.SceneObjects}\n" +
                $"Persistent Objects: {snapshot.PersistentObjects} " +
                $"(Previous: {snapshot.PersistentObjects - previousSnapshot.PersistentObjects:+#;-#;0}, " +
                $"Baseline: {snapshot.PersistentObjects - baselineSnapshot.PersistentObjects:+#;-#;0})\n" +
                $"Managed Memory: {snapshot.ManagedMemoryBytes / 1048576f:F2} MiB\n" +
                $"Allocated Memory: {snapshot.AllocatedMemoryBytes / 1048576f:F2} MiB " +
                $"(Previous: {(snapshot.AllocatedMemoryBytes - previousSnapshot.AllocatedMemoryBytes) / 1048576f:+0.00;-0.00;0.00}, " +
                $"Baseline: {(snapshot.AllocatedMemoryBytes - baselineSnapshot.AllocatedMemoryBytes) / 1048576f:+0.00;-0.00;0.00})\n" +
                $"Reserved Memory: {snapshot.ReservedMemoryBytes / 1048576f:F2} MiB"
            );

            previousSnapshot = snapshot;
        }

        timer.Stop();
        testEnd = DateTime.Now;
        Debug.Log(
            $"[StressTest] Test completed.\n" +
            $"Start: {testStart:yyyy-MM-dd HH:mm:ss}\n" +
            $"End: {testEnd:yyyy-MM-dd HH:mm:ss}\n" +
            $"Total Resets: {completedResets}\n" +
            $"Total Duration: {timer.Elapsed.TotalSeconds:F2} seconds\n" +
            $"Average Time Per Reset: " +
            $"{(completedResets > 0 ? timer.Elapsed.TotalSeconds / completedResets : 0):F2} seconds"
);

        Debug.Log(UnityEngine.Application.persistentDataPath);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        //If scene is running, do nothing
        if (!waitingForScene)
            return;

        if (scene.name != GameModeManager.MasterSceneName)
            return;

        if (GameModeManager.ActiveMode != GameMode.Event)
            return;

        completedResets++;
        waitingForScene = false;

        Debug.Log(
            $"[StressTest] Reset {completedResets} completed."
        );
    }

    private void OnDestroy()
    {
        if (instance != this)
            return;

        IsStressTestRunning = false;
        instance = null;
    }
}
