using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace DansToolbox.EditorTools.BetterScene
{
    internal static class BetterSceneShortcuts
    {
        [Shortcut("Dans Toolbox/Better Scene/Select", KeyCode.Alpha1, ShortcutModifiers.Alt)]
        private static void Select() => BetterSceneController.SetMode(BetterSceneMode.Select);

        [Shortcut("Dans Toolbox/Better Scene/Place", KeyCode.Alpha2, ShortcutModifiers.Alt)]
        private static void Place() => BetterSceneController.SetMode(BetterSceneMode.Place);

        [Shortcut("Dans Toolbox/Better Scene/Measure", KeyCode.Alpha3, ShortcutModifiers.Alt)]
        private static void Measure() => BetterSceneController.SetMode(BetterSceneMode.Measure);

        [Shortcut("Dans Toolbox/Better Scene/Review", KeyCode.Alpha4, ShortcutModifiers.Alt)]
        private static void Review() => BetterSceneController.SetMode(BetterSceneMode.Review);
    }
}
