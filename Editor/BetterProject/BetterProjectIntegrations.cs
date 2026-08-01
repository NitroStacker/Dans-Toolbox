using System;
using System.Reflection;

namespace DansToolbox.EditorTools.BetterProject
{
    internal static class BetterProjectIntegrations
    {
        internal static string GetAddressableGroup(string guid)
        {
            try
            {
                Type defaultObject = Type.GetType(
                    "UnityEditor.AddressableAssets.Settings.AddressableAssetSettingsDefaultObject, Unity.Addressables.Editor");
                object settings = defaultObject?.GetProperty("Settings", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                if (settings == null) return string.Empty;
                MethodInfo finder = settings.GetType().GetMethod("FindAssetEntry", new[] { typeof(string) });
                object entry = finder?.Invoke(settings, new object[] { guid });
                object group = entry?.GetType().GetProperty("parentGroup")?.GetValue(entry);
                return group?.GetType().GetProperty("Name")?.GetValue(group) as string ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        internal static string GetVersionControlState(string path)
        {
            try
            {
                Type provider = Type.GetType("UnityEditor.VersionControl.Provider, UnityEditor.CoreModule");
                if (provider == null) return string.Empty;
                object active = provider.GetProperty("isActive", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                if (!(active is bool enabled) || !enabled) return string.Empty;
                object asset = provider.GetMethod("GetAssetByPath", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, new object[] { path });
                object state = asset?.GetType().GetProperty("state")?.GetValue(asset);
                return state?.ToString() ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
