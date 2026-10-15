using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace PsycheVR.Data
{
    /// <summary>
    /// Gameplay events for the session log (TG-263): what the visitor touched, opened, pressed and
    /// placed. Every call counts as visitor activity for the idle tracker in
    /// <see cref="SessionDataLogger"/>. Details are <c>key=value</c> pairs joined by <c>;</c>, and
    /// objects are named by <see cref="ObjectId"/> so a grab, its release and the session's
    /// <c>interactables_present</c> inventory can be matched by analysis.
    /// </summary>
    public static class SessionEvents
    {
        private static readonly Regex CopySuffix = new Regex(@"(\s*\(\d+\)|\(Clone\))+$");

        /// <summary>Time (unscaled seconds) of the last visitor activity.</summary>
        public static float LastActivityTime { get; private set; }

        /// <summary>Name of the last activity event, for the idle record.</summary>
        public static string LastActivity { get; private set; } = "session_start";

        /// <summary>
        /// Logs a visitor action on <paramref name="target"/>, with optional extra
        /// <c>key=value</c> pairs, and marks the visitor active.
        /// </summary>
        public static void Interaction(string eventName, Component target, string extra = null)
        {
            Interaction(eventName, ObjectId(target), extra);
        }

        /// <summary>
        /// Logs a visitor action on the object named <paramref name="objectId"/> (from
        /// <see cref="ObjectId"/>), for a caller that read the name earlier, such as before a grab
        /// moved the object out of its room.
        /// </summary>
        public static void Interaction(string eventName, string objectId, string extra = null)
        {
            string details = "object=" + objectId;
            if (!string.IsNullOrEmpty(extra))
                details += ";" + extra;
            MarkActive(eventName);
            SessionDataLogger.LogEvent(eventName, details);
        }

        /// <summary>Marks the visitor active without logging anything.</summary>
        public static void MarkActive(string activity)
        {
            LastActivityTime = Time.unscaledTime;
            LastActivity = activity;
        }

        /// <summary>
        /// The object's name without Unity's copy suffixes (" (2)", "(Clone)"), prefixed with its
        /// room (top-level parent) when it has one: "Event_Room/BasketBall". A pivot child (a
        /// book's "SpinePivot") is named after its parent, the object the visitor sees.
        /// </summary>
        public static string ObjectId(Component target)
        {
            if (target == null)
                return "none";
            Transform named = target.transform;
            if (named.name.EndsWith("Pivot") && named.parent != null)
                named = named.parent;
            string name = CopySuffix.Replace(named.name, "");
            Transform root = target.transform.root;
            return root == target.transform ? name : CopySuffix.Replace(root.name, "") + "/" + name;
        }

        /// <summary>Formats seconds for a details value.</summary>
        public static string Seconds(float seconds)
        {
            return seconds.ToString("0.###", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// The distinct interactable objects in the loaded scenes, sorted, joined by "|". Analysis
        /// compares this list with the grabs to find what visitors never discovered.
        /// </summary>
        public static string Inventory(out int count)
        {
            var ids = new SortedSet<string>();
            foreach (var behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (behaviour is ISessionInteractable)
                    ids.Add(ObjectId(behaviour));
            count = ids.Count;
            var text = new StringBuilder();
            foreach (var id in ids)
            {
                if (text.Length > 0) text.Append('|');
                text.Append(id);
            }
            return text.ToString();
        }
    }

    /// <summary>
    /// Marks a component as something a visitor can interact with, so it is listed in the
    /// session's <c>interactables_present</c> inventory.
    /// </summary>
    public interface ISessionInteractable { }
}
