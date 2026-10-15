using System.Collections.Generic;
using UnityEngine;

namespace PsycheVR.Audio
{
    /// <summary>
    /// Plays <see cref="InteractionSound"/> one-shots at a world position through a small pool of 3D
    /// audio sources. The clips come from the <see cref="InteractionSoundLibrary"/> asset in Resources.
    /// A missing library or entry is silent, never an error.
    /// </summary>
    public static class InteractionAudio
    {
        /// <summary>Resources path of the library asset.</summary>
        public const string LibraryResourceName = "InteractionSoundLibrary";

        private const int PoolSize = 8;

        private static InteractionSoundLibrary s_Library;
        private static bool s_LibraryLoaded;
        private static readonly List<AudioSource> s_Pool = new List<AudioSource>();
        private static readonly Dictionary<InteractionSound, int> s_LastVariant = new Dictionary<InteractionSound, int>();
        private static int s_Next;

        /// <summary>Plays one variant of <paramref name="sound"/> at <paramref name="position"/>.</summary>
        public static void Play(InteractionSound sound, Vector3 position)
        {
            Play(sound, position, 1f);
        }

        /// <summary>
        /// Plays one variant of <paramref name="sound"/> at <paramref name="position"/>, its library
        /// volume scaled by <paramref name="volumeScale"/> (0 to 1), for impacts that hit harder or softer.
        /// </summary>
        public static void Play(InteractionSound sound, Vector3 position, float volumeScale)
        {
            if (volumeScale <= 0f)
                return;

            var library = Library;
            if (library == null)
                return;

            var entry = library.Get(sound);
            if (entry == null || entry.clips == null || entry.clips.Length == 0)
                return;

            var clip = entry.clips[PickVariant(sound, entry.clips.Length)];
            if (clip == null)
                return;

            var source = NextSource(library);
            source.transform.position = position;
            source.pitch = 1f + Random.Range(-entry.pitchJitter, entry.pitchJitter);
            source.PlayOneShot(clip, entry.volume * Mathf.Clamp01(volumeScale));
        }

        private static InteractionSoundLibrary Library
        {
            get
            {
                if (!s_LibraryLoaded)
                {
                    s_Library = Resources.Load<InteractionSoundLibrary>(LibraryResourceName);
                    s_LibraryLoaded = true;
                    if (s_Library == null)
                        Debug.LogWarning($"[InteractionAudio] No {LibraryResourceName} in Resources; interaction sounds are off.");
                }
                return s_Library;
            }
        }

        private static int PickVariant(InteractionSound sound, int count)
        {
            if (count == 1)
                return 0;

            s_LastVariant.TryGetValue(sound, out int last);
            int pick = Random.Range(0, count - 1);
            if (pick >= last)
                pick++;   // skips the previous variant
            s_LastVariant[sound] = pick;
            return pick;
        }

        private static AudioSource NextSource(InteractionSoundLibrary library)
        {
            s_Pool.RemoveAll(s => s == null);   // leaving play mode destroys the sources but not this static list
            if (s_Pool.Count < PoolSize)
            {
                var go = new GameObject("InteractionAudio " + s_Pool.Count);
                Object.DontDestroyOnLoad(go);
                var created = go.AddComponent<AudioSource>();
                created.playOnAwake = false;
                created.spatialBlend = 1f;
                created.rolloffMode = AudioRolloffMode.Logarithmic;
                created.minDistance = library.MinDistance;
                created.maxDistance = library.MaxDistance;
                s_Pool.Add(created);
                return created;
            }

            s_Next = (s_Next + 1) % s_Pool.Count;
            return s_Pool[s_Next];
        }
    }
}
