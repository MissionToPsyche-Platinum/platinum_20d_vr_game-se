using System;
using System.Collections.Generic;
using UnityEngine;

namespace PsycheVR.Audio
{
    /// <summary>
    /// Clip variants and levels for every <see cref="InteractionSound"/>. One asset, loaded from
    /// Resources by <see cref="InteractionAudio"/>, so interaction scripts need no audio wiring in
    /// their prefabs.
    /// </summary>
    [CreateAssetMenu(fileName = InteractionAudio.LibraryResourceName, menuName = "Psyche VR/Interaction Sound Library")]
    public class InteractionSoundLibrary : ScriptableObject
    {
        /// <summary>The clips and levels for one sound.</summary>
        [Serializable]
        public class Entry
        {
            public InteractionSound sound;

            [Tooltip("Variants; one is picked at random each time, never the same one twice in a row.")]
            public AudioClip[] clips = Array.Empty<AudioClip>();

            [Range(0f, 1f)] public float volume = 0.8f;

            [Tooltip("Random pitch spread around 1, so repeats do not sound mechanical.")]
            [Range(0f, 0.2f)] public float pitchJitter = 0.05f;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();

        [Header("3D falloff")]
        [Tooltip("Full volume within this distance (m).")]
        [SerializeField] private float minDistance = 0.5f;

        [Tooltip("Silent beyond this distance (m).")]
        [SerializeField] private float maxDistance = 6f;

        /// <summary>Full-volume radius in metres.</summary>
        public float MinDistance => minDistance;

        /// <summary>Silent radius in metres.</summary>
        public float MaxDistance => maxDistance;

        /// <summary>The entry for <paramref name="sound"/>, or null if the library has none.</summary>
        public Entry Get(InteractionSound sound)
        {
            foreach (var entry in entries)
                if (entry.sound == sound)
                    return entry;
            return null;
        }
    }
}
