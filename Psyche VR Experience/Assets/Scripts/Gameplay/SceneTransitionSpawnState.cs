using UnityEngine;

namespace PsycheVR.Gameplay
{
    public static class SceneTransitionSpawnState
    {
        private static bool _hasPendingSpawn;
        private static string _targetSceneName;
        private static Vector3 _targetRigPosition;
        private static float _targetYawDegrees;

        public static void SetPending(string sceneName, Vector3 rigPosition, float yawDegrees)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
                return;

            _hasPendingSpawn = true;
            _targetSceneName = sceneName;
            _targetRigPosition = rigPosition;
            _targetYawDegrees = yawDegrees;
        }

        public static bool TryConsume(string activeSceneName, out Vector3 rigPosition, out float yawDegrees)
        {
            if (_hasPendingSpawn && activeSceneName == _targetSceneName)
            {
                rigPosition = _targetRigPosition;
                yawDegrees = _targetYawDegrees;
                Clear();
                return true;
            }

            rigPosition = default;
            yawDegrees = 0f;
            return false;
        }

        public static void Clear()
        {
            _hasPendingSpawn = false;
            _targetSceneName = string.Empty;
            _targetRigPosition = default;
            _targetYawDegrees = 0f;
        }
    }
}
