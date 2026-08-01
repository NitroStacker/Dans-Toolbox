using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DansToolbox.EditorTools.BetterInspector
{
    internal static class BetterInspectorContextMenu
    {
        private static readonly MethodInfo ObjectContextDropDownMethod = typeof(GenericMenu).GetMethod(
            "ObjectContextDropDown",
            BindingFlags.Instance | BindingFlags.NonPublic,
            null,
            new[] { typeof(Rect), typeof(Object[]), typeof(int) },
            null);

        internal static bool NativeMenuAvailable => ObjectContextDropDownMethod != null;

        internal static bool ShouldOpenComponentMenu(
            EventType eventType,
            Rect cardRect,
            Vector2 mousePosition)
        {
            return eventType == EventType.ContextClick && cardRect.Contains(mousePosition);
        }

        internal static bool ShouldToggleFoldout(
            EventType eventType,
            int mouseButton,
            Rect headerRect,
            Vector2 mousePosition)
        {
            return eventType == EventType.MouseUp &&
                   mouseButton == 0 &&
                   headerRect.Contains(mousePosition);
        }

        internal static bool ShowNativeWithExtras(
            GenericMenu extras,
            Rect guiAnchor,
            Object[] context)
        {
            if (extras == null || context == null || context.Length == 0 || ObjectContextDropDownMethod == null)
            {
                return false;
            }

            try
            {
                Vector2 screenPosition = GUIUtility.GUIToScreenPoint(guiAnchor.position);
                Rect screenAnchor = new Rect(screenPosition, guiAnchor.size);
                ObjectContextDropDownMethod.Invoke(extras, new object[] { screenAnchor, context, 0 });
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Better Inspector could not extend Unity's native context menu: " +
                                 exception.GetBaseException().Message);
                return false;
            }
        }
    }
}
