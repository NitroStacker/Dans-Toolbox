using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DansToolbox.EditorTools.BetterScene
{
    [InitializeOnLoad]
    internal static class BetterSceneSelectionHistory
    {
        private const int Capacity = 80;
        private static readonly List<string> ids = new List<string>();
        private static int index = -1;
        private static bool navigating;

        static BetterSceneSelectionHistory()
        {
            Selection.selectionChanged += RecordCurrent;
            EditorApplication.delayCall += RecordCurrent;
        }

        internal static event Action Changed;
        internal static bool CanBack => FindValidIndex(index - 1, -1) >= 0;
        internal static bool CanForward => FindValidIndex(index + 1, 1) >= 0;
        internal static int Count => ids.Count;

        internal static void Back() => Navigate(-1);
        internal static void Forward() => Navigate(1);

        internal static void Clear()
        {
            ids.Clear();
            index = -1;
            Changed?.Invoke();
        }

        internal static string GetId(UnityEngine.Object target)
        {
            if (target == null) return string.Empty;
            GlobalObjectId id = GlobalObjectId.GetGlobalObjectIdSlow(target);
            return id.identifierType == 0 ? string.Empty : id.ToString();
        }

        internal static UnityEngine.Object Resolve(string value)
        {
            if (string.IsNullOrEmpty(value) || !GlobalObjectId.TryParse(value, out GlobalObjectId id)) return null;
            return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id);
        }

        private static void RecordCurrent()
        {
            if (navigating) return;
            UnityEngine.Object target = Selection.activeObject;
            string id = GetId(target);
            if (string.IsNullOrEmpty(id)) return;
            if (index >= 0 && index < ids.Count && string.Equals(ids[index], id, StringComparison.Ordinal)) return;
            if (index < ids.Count - 1) ids.RemoveRange(index + 1, ids.Count - index - 1);
            ids.Add(id);
            if (ids.Count > Capacity) ids.RemoveAt(0);
            index = ids.Count - 1;
            Changed?.Invoke();
        }

        private static void Navigate(int direction)
        {
            int targetIndex = FindValidIndex(index + direction, direction);
            if (targetIndex < 0) return;
            UnityEngine.Object target = Resolve(ids[targetIndex]);
            if (target == null) return;
            navigating = true;
            try
            {
                index = targetIndex;
                Selection.activeObject = target;
                EditorGUIUtility.PingObject(target);
            }
            finally
            {
                EditorApplication.delayCall += () => navigating = false;
            }
            Changed?.Invoke();
        }

        private static int FindValidIndex(int start, int direction)
        {
            for (int candidate = start; candidate >= 0 && candidate < ids.Count; candidate += direction)
            {
                if (Resolve(ids[candidate]) != null) return candidate;
            }
            return -1;
        }
    }
}
