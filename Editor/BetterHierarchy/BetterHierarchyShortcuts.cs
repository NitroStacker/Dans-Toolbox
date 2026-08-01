using UnityEngine;

namespace DansToolbox.EditorTools.BetterHierarchy
{
    internal enum BetterHierarchyShortcutAction
    {
        None,
        Delete,
        Rename,
        Duplicate,
        Copy,
        Cut,
        Paste,
        SelectAll,
        FocusSearch,
        Frame,
        CreateEmpty,
        CreateEmptyChild,
        CreateEmptyParent
    }

    internal static class BetterHierarchyShortcuts
    {
        internal static BetterHierarchyShortcutAction Resolve(
            KeyCode keyCode,
            bool actionKey,
            bool shift,
            bool alt)
        {
            if (keyCode == KeyCode.Delete || keyCode == KeyCode.Backspace)
            {
                return BetterHierarchyShortcutAction.Delete;
            }

            if (keyCode == KeyCode.F2 ||
                (keyCode == KeyCode.Return && !actionKey && !shift && !alt))
            {
                return BetterHierarchyShortcutAction.Rename;
            }

            if (actionKey)
            {
                if (shift && !alt && keyCode == KeyCode.N)
                {
                    return BetterHierarchyShortcutAction.CreateEmpty;
                }
                if (shift && !alt && keyCode == KeyCode.G)
                {
                    return BetterHierarchyShortcutAction.CreateEmptyParent;
                }
                if (!shift && !alt)
                {
                    switch (keyCode)
                    {
                        case KeyCode.D: return BetterHierarchyShortcutAction.Duplicate;
                        case KeyCode.C: return BetterHierarchyShortcutAction.Copy;
                        case KeyCode.X: return BetterHierarchyShortcutAction.Cut;
                        case KeyCode.V: return BetterHierarchyShortcutAction.Paste;
                        case KeyCode.A: return BetterHierarchyShortcutAction.SelectAll;
                        case KeyCode.F: return BetterHierarchyShortcutAction.FocusSearch;
                    }
                }
            }

            if (!actionKey && shift && alt && keyCode == KeyCode.N)
            {
                return BetterHierarchyShortcutAction.CreateEmptyChild;
            }

            return !actionKey && !shift && !alt && keyCode == KeyCode.F
                ? BetterHierarchyShortcutAction.Frame
                : BetterHierarchyShortcutAction.None;
        }
    }
}
