using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PsycheVR.Modes.Editor
{
    internal static class SoundValidationAdapter
    {
        private const string EntriesField = "entries";
        private const string SoundField = "sound";
        private const string ClipsField = "clips";

        public static void Check(ScriptableObject library, string path, List<string> errors)
        {
            var serialized = new SerializedObject(library);
            var entries = serialized.FindProperty(EntriesField);
            if (entries == null || !entries.isArray)
            {
                errors.Add($"{path}: Cannot inspect sound entries; set EntriesField in SoundValidationAdapter.cs");
                return;
            }

            var seen = new HashSet<int>();
            var declaredValues = new HashSet<int>();
            for (int i = 0; i < entries.arraySize; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                var sound = entry.FindPropertyRelative(SoundField);
                var clips = entry.FindPropertyRelative(ClipsField);
                if (sound == null || sound.propertyType != SerializedPropertyType.Enum ||
                    clips == null || !clips.isArray)
                {
                    errors.Add($"{path}: Entry {i} cannot be inspected; check SoundField/ClipsField names");
                    continue;
                }

                int value = sound.enumValueIndex;
                string name = value >= 0 && value < sound.enumNames.Length
                    ? sound.enumNames[value] : $"#{value}";
                for (int j = 0; j < sound.enumNames.Length; j++) declaredValues.Add(j);
                if (!seen.Add(value))
                    errors.Add($"{path}: Duplicate InteractionSound entry '{name}'");

                bool hasClip = false;
                for (int j = 0; j < clips.arraySize; j++)
                {
                    var clip = clips.GetArrayElementAtIndex(j);
                    if (clip.propertyType == SerializedPropertyType.ObjectReference &&
                        clip.objectReferenceValue is AudioClip)
                        hasClip = true;
                }
                if (!hasClip)
                    errors.Add($"{path}: InteractionSound '{name}' has no assigned AudioClip");
            }
            foreach (int value in declaredValues.Except(seen))
                errors.Add($"{path}: Missing InteractionSound entry '{value}'");
        }
    }
}
