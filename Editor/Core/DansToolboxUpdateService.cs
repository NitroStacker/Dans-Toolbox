using System;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using UnityEngine.Networking;
using PackageManagerInfo = UnityEditor.PackageManager.PackageInfo;

namespace DansToolbox.Editor
{
    [InitializeOnLoad]
    internal static class DansToolboxUpdateService
    {
        internal const string RepositoryUrl =
            "https://github.com/NitroStacker/Dans-Toolbox.git";
        internal const string ReleaseNotesUrl =
            "https://github.com/NitroStacker/Dans-Toolbox/blob/main/CHANGELOG.md";

        private const string LatestManifestUrl =
            "https://raw.githubusercontent.com/NitroStacker/Dans-Toolbox/main/package.json";
        private const string LatestVersionKey = "DansToolbox.Updates.LatestVersion";
        private const string LastCheckTicksKey = "DansToolbox.Updates.LastCheckUtcTicks";
        private const double CheckIntervalHours = 12d;
        private const int RequestTimeoutSeconds = 6;

        private static UnityWebRequest versionRequest;
        private static AddRequest updateRequest;
        private static bool manualCheck;

        static DansToolboxUpdateService()
        {
            RefreshInstalledPackage();
            LatestVersion = EditorPrefs.GetString(LatestVersionKey, string.Empty);
            AssemblyReloadEvents.beforeAssemblyReload += BeforeAssemblyReload;
            EditorApplication.delayCall += CheckWhenStale;
        }

        internal static event Action Changed;

        internal static string CurrentVersion { get; private set; } = string.Empty;
        internal static string LatestVersion { get; private set; } = string.Empty;
        internal static string LastError { get; private set; } = string.Empty;
        internal static bool IsChecking => versionRequest != null;
        internal static bool IsUpdating => updateRequest != null;
        internal static bool CanAutoUpdate { get; private set; }
        internal static string InstalledPackageId { get; private set; } = string.Empty;

        internal static bool UpdateAvailable =>
            IsNewerVersion(LatestVersion, CurrentVersion);

        internal static string ToolbarTooltip => UpdateAvailable
            ? $"Dans Toolbox v{LatestVersion} is available - open the Hub to update"
            : "Open Dans Toolbox Hub";

        [MenuItem("Tools/Dans Toolbox/Check for Updates", false, -89)]
        private static void CheckFromMenu()
        {
            CheckNow(true);
        }

        internal static void CheckNow(bool userInitiated = false)
        {
            if (IsChecking || IsUpdating)
            {
                return;
            }

            manualCheck = userInitiated;
            LastError = string.Empty;
            versionRequest = UnityWebRequest.Get(LatestManifestUrl);
            versionRequest.timeout = RequestTimeoutSeconds;
            versionRequest.SetRequestHeader("User-Agent", "Dans-Toolbox-Unity-Update-Checker");
            versionRequest.SendWebRequest();
            EditorApplication.update -= PollVersionRequest;
            EditorApplication.update += PollVersionRequest;
            NotifyChanged();
        }

        internal static bool BeginUpdate()
        {
            if (!UpdateAvailable || IsUpdating || IsChecking)
            {
                return false;
            }

            if (!CanAutoUpdate)
            {
                LastError =
                    "Automatic update is available for Git installs. This copy is local or embedded.";
                NotifyChanged();
                return false;
            }

            string identifier = BuildUpdateIdentifier(InstalledPackageId, LatestVersion);
            if (string.IsNullOrEmpty(identifier))
            {
                LastError = "The installed package channel could not be resolved.";
                NotifyChanged();
                return false;
            }

            try
            {
                LastError = string.Empty;
                updateRequest = Client.Add(identifier);
                EditorUtility.DisplayProgressBar(
                    "Updating Dans Toolbox",
                    $"Installing v{LatestVersion} through Unity Package Manager...",
                    0.55f);
                EditorApplication.update -= PollUpdateRequest;
                EditorApplication.update += PollUpdateRequest;
                NotifyChanged();
                return true;
            }
            catch (Exception exception)
            {
                updateRequest = null;
                EditorUtility.ClearProgressBar();
                LastError = exception.Message;
                NotifyChanged();
                return false;
            }
        }

        internal static void OpenPackageManager()
        {
            EditorApplication.ExecuteMenuItem("Window/Package Management/Package Manager");
        }

        internal static void OpenReleasePage()
        {
            Application.OpenURL(ReleaseNotesUrl);
        }

        internal static bool IsNewerVersion(string candidate, string current)
        {
            if (!TryParseVersion(candidate, out ParsedVersion left) ||
                !TryParseVersion(current, out ParsedVersion right))
            {
                return false;
            }

            int comparison = left.Major.CompareTo(right.Major);
            if (comparison == 0) comparison = left.Minor.CompareTo(right.Minor);
            if (comparison == 0) comparison = left.Patch.CompareTo(right.Patch);
            if (comparison != 0) return comparison > 0;

            if (left.Prerelease.Length == 0 && right.Prerelease.Length > 0) return true;
            if (left.Prerelease.Length > 0 && right.Prerelease.Length == 0) return false;

            int length = Math.Max(left.Prerelease.Length, right.Prerelease.Length);
            for (int index = 0; index < length; index++)
            {
                if (index >= left.Prerelease.Length) return false;
                if (index >= right.Prerelease.Length) return true;

                string leftPart = left.Prerelease[index];
                string rightPart = right.Prerelease[index];
                bool leftNumber = int.TryParse(leftPart, out int leftValue);
                bool rightNumber = int.TryParse(rightPart, out int rightValue);
                if (leftNumber && rightNumber)
                {
                    comparison = leftValue.CompareTo(rightValue);
                }
                else if (leftNumber != rightNumber)
                {
                    comparison = leftNumber ? -1 : 1;
                }
                else
                {
                    comparison = string.Compare(
                        leftPart,
                        rightPart,
                        StringComparison.OrdinalIgnoreCase);
                }

                if (comparison != 0) return comparison > 0;
            }

            return false;
        }

        internal static string BuildUpdateIdentifier(string packageId, string latestVersion)
        {
            if (!TryParseVersion(latestVersion, out _))
            {
                return string.Empty;
            }

            bool tracksMain = !string.IsNullOrEmpty(packageId) &&
                              packageId.IndexOf("#main", StringComparison.OrdinalIgnoreCase) >= 0;
            return tracksMain
                ? RepositoryUrl + "#main"
                : RepositoryUrl + "#v" + latestVersion.Trim().TrimStart('v', 'V');
        }

        internal static bool TryReadRemoteVersion(string json, out string version)
        {
            version = string.Empty;
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            try
            {
                RemotePackageManifest manifest = JsonUtility.FromJson<RemotePackageManifest>(json);
                version = manifest?.version?.Trim() ?? string.Empty;
                return TryParseVersion(version, out _);
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private static void CheckWhenStale()
        {
            if (Application.isBatchMode)
            {
                NotifyChanged();
                return;
            }

            if (!long.TryParse(
                    EditorPrefs.GetString(LastCheckTicksKey, string.Empty),
                    out long lastTicks) ||
                lastTicks < DateTime.MinValue.Ticks ||
                lastTicks > DateTime.MaxValue.Ticks ||
                DateTime.UtcNow - new DateTime(lastTicks, DateTimeKind.Utc) >=
                TimeSpan.FromHours(CheckIntervalHours))
            {
                CheckNow();
            }
            else
            {
                NotifyChanged();
            }
        }

        private static void PollVersionRequest()
        {
            if (versionRequest == null || !versionRequest.isDone)
            {
                return;
            }

            EditorApplication.update -= PollVersionRequest;
            string remoteVersion = string.Empty;
            bool succeeded = versionRequest.result == UnityWebRequest.Result.Success &&
                             TryReadRemoteVersion(
                                 versionRequest.downloadHandler.text,
                                 out remoteVersion);
            string requestError = versionRequest.error;
            versionRequest.Dispose();
            versionRequest = null;

            if (succeeded)
            {
                LatestVersion = remoteVersion;
                EditorPrefs.SetString(LatestVersionKey, LatestVersion);
                EditorPrefs.SetString(
                    LastCheckTicksKey,
                    DateTime.UtcNow.Ticks.ToString());
                LastError = string.Empty;
                if (manualCheck)
                {
                    Debug.Log(UpdateAvailable
                        ? $"[Dans Toolbox] Update v{LatestVersion} is available."
                        : $"[Dans Toolbox] v{CurrentVersion} is up to date.");
                }
            }
            else
            {
                LastError = "Could not check for updates" +
                            (string.IsNullOrEmpty(requestError) ? "." : $": {requestError}");
                if (manualCheck)
                {
                    Debug.LogWarning("[Dans Toolbox] " + LastError);
                }
            }

            manualCheck = false;
            NotifyChanged();
        }

        private static void PollUpdateRequest()
        {
            if (updateRequest == null || !updateRequest.IsCompleted)
            {
                return;
            }

            EditorApplication.update -= PollUpdateRequest;
            EditorUtility.ClearProgressBar();
            AddRequest completed = updateRequest;
            updateRequest = null;
            if (completed.Status == StatusCode.Success)
            {
                CurrentVersion = completed.Result?.version ?? LatestVersion;
                LastError = string.Empty;
                Debug.Log($"[Dans Toolbox] Updated to v{CurrentVersion}.");
            }
            else
            {
                LastError = completed.Error?.message ?? "Unity Package Manager could not install the update.";
                Debug.LogError("[Dans Toolbox] Update failed: " + LastError);
            }

            NotifyChanged();
        }

        private static void RefreshInstalledPackage()
        {
            PackageManagerInfo package = PackageManagerInfo.FindForAssembly(
                typeof(DansToolboxUpdateService).Assembly);
            CurrentVersion = package?.version ?? string.Empty;
            InstalledPackageId = package?.packageId ?? string.Empty;
            CanAutoUpdate = package != null && package.source == PackageSource.Git;
        }

        private static bool TryParseVersion(string value, out ParsedVersion parsed)
        {
            parsed = default;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string normalized = value.Trim().TrimStart('v', 'V');
            int buildIndex = normalized.IndexOf('+');
            if (buildIndex >= 0)
            {
                normalized = normalized.Substring(0, buildIndex);
            }

            string[] releaseAndPrerelease = normalized.Split(new[] { '-' }, 2);
            string[] core = releaseAndPrerelease[0].Split('.');
            if (core.Length != 3 ||
                !int.TryParse(core[0], out int major) ||
                !int.TryParse(core[1], out int minor) ||
                !int.TryParse(core[2], out int patch))
            {
                return false;
            }

            parsed = new ParsedVersion(
                major,
                minor,
                patch,
                releaseAndPrerelease.Length == 2
                    ? releaseAndPrerelease[1].Split('.')
                    : Array.Empty<string>());
            return true;
        }

        private static void BeforeAssemblyReload()
        {
            EditorUtility.ClearProgressBar();
            EditorApplication.update -= PollVersionRequest;
            EditorApplication.update -= PollUpdateRequest;
            versionRequest?.Dispose();
            versionRequest = null;
            updateRequest = null;
        }

        private static void NotifyChanged()
        {
            Changed?.Invoke();
        }

        [Serializable]
        private sealed class RemotePackageManifest
        {
            public string version;
        }

        private readonly struct ParsedVersion
        {
            internal ParsedVersion(
                int major,
                int minor,
                int patch,
                string[] prerelease)
            {
                Major = major;
                Minor = minor;
                Patch = patch;
                Prerelease = prerelease;
            }

            internal int Major { get; }
            internal int Minor { get; }
            internal int Patch { get; }
            internal string[] Prerelease { get; }
        }
    }
}
