using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace PsycheVR.Story.Tests
{
    public class StoryTextTests
    {
        private static Type TextType => Type.GetType("PsycheVR.Story.StoryText, Assembly-CSharp", true);

        [Test]
        public void ResourceAssetContainsIntroCopy()
        {
            var text = Resources.Load("StoryText", TextType);
            Assert.That(text, Is.Not.Null, "Assets/Resources/StoryText.asset must load.");
            Assert.That(TextType.GetProperty("IntroDateText").GetValue(text), Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void IntroCopyCanBeEditedThroughSerializedField()
        {
            var text = ScriptableObject.CreateInstance(TextType);
            try
            {
                var serialized = new SerializedObject(text);
                serialized.FindProperty("introDateText").stringValue = "Fixture date";
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(TextType.GetProperty("IntroDateText").GetValue(text), Is.EqualTo("Fixture date"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(text);
            }
        }
    }
}
