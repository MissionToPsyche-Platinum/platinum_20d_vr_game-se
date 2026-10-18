using PsycheVR.Modes;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using static System.Net.Mime.MediaTypeNames;

public class EventReloadStressTest : MonoBehaviour
{

    [SerializeField] private int totalResets = 5;
    [SerializeField] private float delayBetweenResets = 2f;
    [SerializeField] private float stabilizationDelay = 1f;

    private StressTestMetrics metrics;
    private StressTestLogger logger;
    private int completedResets;
    private bool waitingForScene;
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
        IsStressTestRunning = true;

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

        StartCoroutine(RunStressTest());
    }

    private IEnumerator RunStressTest()
    {
        while (completedResets < totalResets)
        {
            yield return new WaitForSecondsRealtime(delayBetweenResets);

            waitingForScene = true;

            Debug.Log(
                $"[StressTest] Starting reset {completedResets + 1}/{totalResets}"
            );

            GameModeManager.SwitchTo(GameMode.Event);

            yield return new WaitUntil(() => !waitingForScene);
            // Allow the newly loaded scene to finish initializing.
            yield return new WaitForSecondsRealtime(stabilizationDelay);

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
                    snapshot
                );

                Debug.Log("[StressTest] Baseline established.");
            }
            else
            {
                int objectDelta = snapshot.TotalObjects -
                                  previousSnapshot.TotalObjects;

                int objectGrowth = snapshot.TotalObjects -
                                   baselineSnapshot.TotalObjects;

                int persistentDelta = snapshot.PersistentObjects -
                                      previousSnapshot.PersistentObjects;

                int persistentGrowth = snapshot.PersistentObjects -
                                       baselineSnapshot.PersistentObjects;

                float memoryDelta =
                    (snapshot.AllocatedMemoryBytes -
                     previousSnapshot.AllocatedMemoryBytes) / 1048576f;

                float memoryGrowth =
                    (snapshot.AllocatedMemoryBytes -
                     baselineSnapshot.AllocatedMemoryBytes) / 1048576f;

                logger.Record(
                    completedResets,
                    "Measured",
                    snapshot,
                    previousSnapshot,
                     baselineSnapshot
                );

                Debug.Log(
                    $"[StressTest] Reset {completedResets}\n" +
                    $"Objects: {snapshot.TotalObjects} " +
                    $"(Previous: {objectDelta:+#;-#;0}, Baseline: {objectGrowth:+#;-#;0})\n" +
                    $"Persistent: {snapshot.PersistentObjects} " +
                    $"(Previous: {persistentDelta:+#;-#;0}, Baseline: {persistentGrowth:+#;-#;0})\n" +
                    $"Allocated Memory: {snapshot.AllocatedMemoryBytes / 1048576f:F2} MiB " +
                    $"(Previous: {memoryDelta:+0.00;-0.00;0.00}, " +
                    $"Baseline: {memoryGrowth:+0.00;-0.00;0.00})"
                );
            }

            previousSnapshot = snapshot;

            Debug.Log(
                $"[StressTest] Reset {completedResets} | " +
                $"Total Objects: {snapshot.TotalObjects} | " +
                $"Scene Objects: {snapshot.SceneObjects} | " +
                $"Persistent Objects: {snapshot.PersistentObjects} | " +
                $"Managed Memory: {snapshot.ManagedMemoryBytes / 1048576f:F2} MB | " +
                $"Allocated Memory: {snapshot.AllocatedMemoryBytes / 1048576f:F2} MB | " +
                $"Reserved Memory: {snapshot.ReservedMemoryBytes / 1048576f:F2} MB"
            );
        }

        Debug.Log(
            $"[StressTest] Completed {completedResets} resets."
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
