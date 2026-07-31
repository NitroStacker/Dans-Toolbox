using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;

namespace DansToolbox.Editor
{
    internal static class DansToolboxToolbarButton
    {
        private const string Tooltip = "Open Dans Toolbox Setup Wizard";

        [MainToolbarElement(
            "Dans Toolbox/Setup Wizard",
            defaultDockPosition = MainToolbarDockPosition.Left,
            defaultDockIndex = 2)]
        internal static MainToolbarElement Create()
        {
            return new MainToolbarButton(
                new MainToolbarContent(LoadIcon(), Tooltip),
                DansToolboxSetupWizard.Open);
        }

        private static Texture2D LoadIcon()
        {
            UnityEditor.PackageManager.PackageInfo package =
                UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(DansToolboxToolbarButton).Assembly);
            if (package != null)
            {
                string iconPath = $"Packages/{package.name}/Editor/Icons/toolbox.png";
                Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);
                if (icon != null)
                {
                    return icon;
                }
            }

            return EditorGUIUtility.IconContent("Settings").image as Texture2D;
        }
    }
}
