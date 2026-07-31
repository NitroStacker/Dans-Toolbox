using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace DansToolbox.EditorTools.NativeWindowDock
{
    [Serializable]
    internal struct NativeWindowCrop
    {
        internal NativeWindowCrop(int left, int top, int right, int bottom)
        {
            Left = Math.Max(0, left);
            Top = Math.Max(0, top);
            Right = Math.Max(0, right);
            Bottom = Math.Max(0, bottom);
        }

        internal int Left { get; }
        internal int Top { get; }
        internal int Right { get; }
        internal int Bottom { get; }
        internal bool IsEmpty => Left == 0 && Top == 0 && Right == 0 && Bottom == 0;

        internal RectInt CalculateTargetBounds(
            int hostWidth,
            int hostHeight,
            float pixelsPerPoint)
        {
            float scale = Mathf.Max(1f, pixelsPerPoint);
            int leftPixels = Mathf.RoundToInt(Left * scale);
            int topPixels = Mathf.RoundToInt(Top * scale);
            int rightPixels = Mathf.RoundToInt(Right * scale);
            int bottomPixels = Mathf.RoundToInt(Bottom * scale);
            return new RectInt(
                -leftPixels,
                -topPixels,
                Math.Max(1, hostWidth + leftPixels + rightPixels),
                Math.Max(1, hostHeight + topPixels + bottomPixels));
        }
    }

    internal sealed class NativeWindowCandidate
    {
        internal NativeWindowCandidate(
            IntPtr handle,
            uint processId,
            string title,
            string processName,
            string className)
        {
            Handle = handle;
            ProcessId = processId;
            Title = title;
            ProcessName = processName;
            ClassName = className;
        }

        internal IntPtr Handle { get; }
        internal uint ProcessId { get; }
        internal string Title { get; }
        internal string ProcessName { get; }
        internal string ClassName { get; }
        internal string DisplayLabel => ComposeDisplayLabel(ProcessName, Title);

        internal static string ComposeDisplayLabel(string processName, string title)
        {
            string cleanProcess = NormalizeDisplayText(processName, 24);
            string cleanTitle = NormalizeDisplayText(title, 72);
            if (string.IsNullOrEmpty(cleanProcess))
            {
                cleanProcess = "Application";
            }

            if (string.IsNullOrEmpty(cleanTitle))
            {
                cleanTitle = "Untitled window";
            }

            return cleanProcess + "  /  " + cleanTitle;
        }

        internal static string NormalizeDisplayText(string value, int maximumLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string normalized = string.Join(
                " ",
                value.Split((char[])null, StringSplitOptions.RemoveEmptyEntries));
            if (normalized.Length <= maximumLength)
            {
                return normalized;
            }

            return normalized.Substring(0, Math.Max(1, maximumLength - 1)) + "…";
        }
    }

    internal sealed class NativeWindowSession : IDisposable
    {
        private readonly IntPtr target;
        private readonly IntPtr originalParent;
        private readonly long originalStyle;
        private readonly long originalExtendedStyle;
        private readonly NativeWindowInterop.RECT originalRect;
        private readonly NativeWindowInterop.WINDOWPLACEMENT originalPlacement;
        private IntPtr host;
        private IntPtr hostOwner;
        private NativeWindowCrop crop;
        private RectInt lastTargetBounds = new RectInt(int.MinValue, int.MinValue, 0, 0);
        private bool embedded;
        private bool disposed;
        private bool visible;

        private NativeWindowSession(
            IntPtr target,
            IntPtr originalParent,
            long originalStyle,
            long originalExtendedStyle,
            NativeWindowInterop.RECT originalRect,
            NativeWindowInterop.WINDOWPLACEMENT originalPlacement)
        {
            this.target = target;
            this.originalParent = originalParent;
            this.originalStyle = originalStyle;
            this.originalExtendedStyle = originalExtendedStyle;
            this.originalRect = originalRect;
            this.originalPlacement = originalPlacement;
        }

        internal IntPtr Target => target;
        internal bool IsEmbedded => embedded && !disposed;
        internal bool IsTargetAlive => !disposed && NativeWindowInterop.IsWindow(target);

        internal void SetCrop(NativeWindowCrop value)
        {
            crop = value;
            lastTargetBounds = new RectInt(int.MinValue, int.MinValue, 0, 0);
        }

        internal static NativeWindowSession Attach(IntPtr target)
        {
            if (target == IntPtr.Zero || !NativeWindowInterop.IsWindow(target))
            {
                throw new InvalidOperationException("The selected application window is no longer available.");
            }

            string blockReason = NativeWindowInterop.GetEmbeddingBlockReason(target);
            if (!string.IsNullOrEmpty(blockReason))
            {
                throw new InvalidOperationException(blockReason);
            }

            NativeWindowInterop.RECT originalRect;
            if (!NativeWindowInterop.GetWindowRect(target, out originalRect))
            {
                throw NativeWindowInterop.CreateWin32Exception("Could not read the selected window's placement.");
            }

            NativeWindowInterop.WINDOWPLACEMENT placement =
                NativeWindowInterop.WINDOWPLACEMENT.Create();
            NativeWindowInterop.GetWindowPlacement(target, ref placement);

            IntPtr originalParent = NativeWindowInterop.GetParent(target);
            long originalStyle = NativeWindowInterop.GetWindowStyle(target);
            long originalExtendedStyle = NativeWindowInterop.GetWindowExtendedStyle(target);
            return new NativeWindowSession(
                target,
                originalParent,
                originalStyle,
                originalExtendedStyle,
                originalRect,
                placement);
        }

        internal void Position(Rect screenRectInPoints, float pixelsPerPoint)
        {
            if (disposed)
            {
                return;
            }

            float scale = Mathf.Max(1f, pixelsPerPoint);
            IntPtr unityWindow = NativeWindowInterop.ResolveUnityContainerWindow(
                screenRectInPoints,
                scale);
            if (unityWindow == IntPtr.Zero)
            {
                throw new InvalidOperationException(
                    "The Unity window containing this tab could not be located.");
            }

            if (!embedded)
            {
                Embed(unityWindow);
            }
            else if (hostOwner != unityWindow)
            {
                NativeWindowInterop.SetParent(host, unityWindow);
                if (NativeWindowInterop.GetParent(host) != unityWindow)
                {
                    throw NativeWindowInterop.CreateWin32Exception(
                        "The native host could not follow this tab to its new Unity window.");
                }

                hostOwner = unityWindow;
            }

            NativeWindowInterop.POINT clientOrigin = new NativeWindowInterop.POINT(
                Mathf.RoundToInt(screenRectInPoints.x * scale),
                Mathf.RoundToInt(screenRectInPoints.y * scale));
            if (!NativeWindowInterop.ScreenToClient(unityWindow, ref clientOrigin))
            {
                return;
            }

            int width = Mathf.Max(1, Mathf.RoundToInt(screenRectInPoints.width * scale));
            int height = Mathf.Max(1, Mathf.RoundToInt(screenRectInPoints.height * scale));
            NativeWindowInterop.SetWindowPos(
                host,
                NativeWindowInterop.HWND_TOP,
                clientOrigin.X,
                clientOrigin.Y,
                width,
                height,
                NativeWindowInterop.SWP_NOACTIVATE | NativeWindowInterop.SWP_SHOWWINDOW);

            RectInt targetBounds = crop.CalculateTargetBounds(width, height, scale);
            if (targetBounds != lastTargetBounds)
            {
                lastTargetBounds = targetBounds;
                NativeWindowInterop.SetWindowPos(
                    target,
                    IntPtr.Zero,
                    targetBounds.x,
                    targetBounds.y,
                    targetBounds.width,
                    targetBounds.height,
                    NativeWindowInterop.SWP_NOACTIVATE
                    | NativeWindowInterop.SWP_NOZORDER
                    | NativeWindowInterop.SWP_SHOWWINDOW);
                NativeWindowInterop.RedrawEmbeddedWindow(target);
            }
        }

        internal void SetVisible(bool shouldBeVisible)
        {
            if (disposed
                || !embedded
                || visible == shouldBeVisible
                || !NativeWindowInterop.IsWindow(host))
            {
                return;
            }

            visible = shouldBeVisible;
            NativeWindowInterop.ShowWindow(
                host,
                shouldBeVisible ? NativeWindowInterop.SW_SHOWNA : NativeWindowInterop.SW_HIDE);
        }

        internal void Focus()
        {
            if (!disposed && NativeWindowInterop.IsWindow(target))
            {
                NativeWindowInterop.SetForegroundWindow(target);
                NativeWindowInterop.SetFocus(target);
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            try
            {
                if (embedded || NativeWindowInterop.IsWindow(host))
                {
                    NativeWindowInterop.RestoreWindow(
                        target,
                        host,
                        originalParent,
                        originalStyle,
                        originalExtendedStyle,
                        originalRect,
                        originalPlacement);
                }
            }
            finally
            {
                NativeWindowSafetyNet.Unregister(target);
            }
        }

        internal string SerializeRecoveryState()
        {
            return string.Join(
                "|",
                target.ToInt64().ToString(CultureInfo.InvariantCulture),
                host.ToInt64().ToString(CultureInfo.InvariantCulture),
                originalParent.ToInt64().ToString(CultureInfo.InvariantCulture),
                originalStyle.ToString(CultureInfo.InvariantCulture),
                originalExtendedStyle.ToString(CultureInfo.InvariantCulture),
                originalRect.Left.ToString(CultureInfo.InvariantCulture),
                originalRect.Top.ToString(CultureInfo.InvariantCulture),
                originalRect.Right.ToString(CultureInfo.InvariantCulture),
                originalRect.Bottom.ToString(CultureInfo.InvariantCulture),
                originalPlacement.ShowCmd.ToString(CultureInfo.InvariantCulture));
        }

        private void Embed(IntPtr unityWindow)
        {
            host = NativeWindowInterop.CreateHostWindow(unityWindow);
            hostOwner = unityWindow;
            if (host == IntPtr.Zero)
            {
                throw NativeWindowInterop.CreateWin32Exception(
                    "Could not create the native host surface.");
            }

            try
            {
                NativeWindowInterop.ShowWindow(target, NativeWindowInterop.SW_HIDE);

                long childStyle = originalStyle;
                childStyle &= ~(NativeWindowInterop.WS_CAPTION
                                | NativeWindowInterop.WS_THICKFRAME
                                | NativeWindowInterop.WS_SYSMENU
                                | NativeWindowInterop.WS_MINIMIZEBOX
                                | NativeWindowInterop.WS_MAXIMIZEBOX
                                | NativeWindowInterop.WS_POPUP);
                childStyle |= NativeWindowInterop.WS_CHILD
                              | NativeWindowInterop.WS_VISIBLE
                              | NativeWindowInterop.WS_CLIPSIBLINGS
                              | NativeWindowInterop.WS_CLIPCHILDREN;
                NativeWindowInterop.SetWindowStyle(target, childStyle);

                long childExtendedStyle = originalExtendedStyle
                                          & ~(NativeWindowInterop.WS_EX_APPWINDOW
                                              | NativeWindowInterop.WS_EX_TOPMOST);
                NativeWindowInterop.SetWindowExtendedStyle(target, childExtendedStyle);

                NativeWindowInterop.SetParent(target, host);
                int setParentError = Marshal.GetLastWin32Error();
                if (NativeWindowInterop.GetParent(target) != host)
                {
                    string message =
                        "Windows refused to embed this application. Close and relaunch it normally, or run Unity at the same elevation level.";
                    throw NativeWindowInterop.CreateWin32Exception(message, setParentError);
                }

                NativeWindowInterop.SetWindowPos(
                    target,
                    IntPtr.Zero,
                    0,
                    0,
                    1,
                    1,
                    NativeWindowInterop.SWP_NOACTIVATE
                    | NativeWindowInterop.SWP_NOZORDER
                    | NativeWindowInterop.SWP_FRAMECHANGED);
                NativeWindowInterop.ShowWindow(target, NativeWindowInterop.SW_SHOW);
                NativeWindowInterop.RedrawEmbeddedWindow(target);
                embedded = true;
                visible = true;
                NativeWindowSafetyNet.Register(this);
            }
            catch
            {
                NativeWindowInterop.RestoreWindow(
                    target,
                    host,
                    originalParent,
                    originalStyle,
                    originalExtendedStyle,
                    originalRect,
                    originalPlacement);
                host = IntPtr.Zero;
                hostOwner = IntPtr.Zero;
                embedded = false;
                throw;
            }
        }
    }

    [InitializeOnLoad]
    internal static class NativeWindowSafetyNet
    {
        private const string SessionKey = "BattleSoccer.NativeWindowDock.ActiveEmbeddings";
        private const string LegacySessionKey = "BattleSoccer.NativeWindowDock.ActiveEmbedding";
        private static readonly Dictionary<long, string> RecoveryStates =
            new Dictionary<long, string>();

        static NativeWindowSafetyNet()
        {
            LoadRecoveryStates();
            EditorApplication.delayCall += RecoverOrphanedWindows;
        }

        internal static void Register(NativeWindowSession session)
        {
            RecoveryStates[session.Target.ToInt64()] = session.SerializeRecoveryState();
            PersistRecoveryStates();
        }

        internal static void Unregister(IntPtr target)
        {
            if (target != IntPtr.Zero)
            {
                RecoveryStates.Remove(target.ToInt64());
            }

            PersistRecoveryStates();
        }

        private static void RecoverOrphanedWindows()
        {
            List<string> states = RecoveryStates.Values.ToList();
            string legacyState = SessionState.GetString(LegacySessionKey, string.Empty);
            if (!string.IsNullOrEmpty(legacyState) && !states.Contains(legacyState))
            {
                states.Add(legacyState);
            }

            if (states.Count == 0)
            {
                return;
            }

            foreach (string data in states)
            {
                try
                {
                    RestoreRecoveryState(data);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        "Native Window Dock recovery could not restore an app window: "
                        + exception.Message);
                }
            }

            RecoveryStates.Clear();
            SessionState.EraseString(SessionKey);
            SessionState.EraseString(LegacySessionKey);
        }

        private static void LoadRecoveryStates()
        {
            string data = SessionState.GetString(SessionKey, string.Empty);
            if (string.IsNullOrEmpty(data))
            {
                return;
            }

            try
            {
                RecoveryStateCollection collection =
                    JsonUtility.FromJson<RecoveryStateCollection>(data);
                if (collection?.states == null)
                {
                    return;
                }

                foreach (string state in collection.states)
                {
                    long target;
                    if (TryReadRecoveryTarget(state, out target))
                    {
                        RecoveryStates[target] = state;
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "Native Window Dock could not read saved recovery state: "
                    + exception.Message);
            }
        }

        private static void PersistRecoveryStates()
        {
            if (RecoveryStates.Count == 0)
            {
                SessionState.EraseString(SessionKey);
                return;
            }

            RecoveryStateCollection collection = new RecoveryStateCollection
            {
                states = RecoveryStates.Values.ToList()
            };
            SessionState.SetString(SessionKey, JsonUtility.ToJson(collection));
        }

        private static bool TryReadRecoveryTarget(string state, out long target)
        {
            target = 0;
            if (string.IsNullOrEmpty(state))
            {
                return false;
            }

            string[] parts = state.Split('|');
            return parts.Length == 10
                   && long.TryParse(
                       parts[0],
                       NumberStyles.Integer,
                       CultureInfo.InvariantCulture,
                       out target);
        }

        private static void RestoreRecoveryState(string data)
        {
            string[] parts = data.Split('|');
            if (parts.Length != 10)
            {
                return;
            }

            IntPtr target = new IntPtr(ParseLong(parts[0]));
            IntPtr host = new IntPtr(ParseLong(parts[1]));
            IntPtr parent = new IntPtr(ParseLong(parts[2]));
            long style = ParseLong(parts[3]);
            long extendedStyle = ParseLong(parts[4]);
            NativeWindowInterop.RECT rect = new NativeWindowInterop.RECT
            {
                Left = (int)ParseLong(parts[5]),
                Top = (int)ParseLong(parts[6]),
                Right = (int)ParseLong(parts[7]),
                Bottom = (int)ParseLong(parts[8])
            };
            NativeWindowInterop.WINDOWPLACEMENT placement =
                NativeWindowInterop.WINDOWPLACEMENT.Create();
            placement.ShowCmd = (int)ParseLong(parts[9]);
            placement.NormalPosition = rect;
            NativeWindowInterop.RestoreWindow(
                target,
                host,
                parent,
                style,
                extendedStyle,
                rect,
                placement);
        }

        private static long ParseLong(string value)
        {
            return long.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
        }

        [Serializable]
        private sealed class RecoveryStateCollection
        {
            public List<string> states = new List<string>();
        }
    }

    internal static class NativeWindowInterop
    {
        internal const long WS_CHILD = 0x40000000L;
        internal const long WS_POPUP = 0x80000000L;
        internal const long WS_VISIBLE = 0x10000000L;
        internal const long WS_CAPTION = 0x00C00000L;
        internal const long WS_THICKFRAME = 0x00040000L;
        internal const long WS_SYSMENU = 0x00080000L;
        internal const long WS_MINIMIZEBOX = 0x00020000L;
        internal const long WS_MAXIMIZEBOX = 0x00010000L;
        internal const long WS_CLIPSIBLINGS = 0x04000000L;
        internal const long WS_CLIPCHILDREN = 0x02000000L;
        internal const long WS_EX_APPWINDOW = 0x00040000L;
        internal const long WS_EX_TOPMOST = 0x00000008L;

        internal const int SW_HIDE = 0;
        internal const int SW_SHOWNORMAL = 1;
        internal const int SW_SHOW = 5;
        internal const int SW_RESTORE = 9;
        internal const int SW_SHOWNA = 8;

        internal const uint SWP_NOZORDER = 0x0004;
        internal const uint SWP_NOACTIVATE = 0x0010;
        internal const uint SWP_FRAMECHANGED = 0x0020;
        internal const uint SWP_SHOWWINDOW = 0x0040;

        internal static readonly IntPtr HWND_TOP = IntPtr.Zero;

        private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
        private const uint TOKEN_QUERY = 0x0008;
        private const int TOKEN_INTEGRITY_LEVEL = 25;
        private const int SECURITY_MANDATORY_HIGH_RID = 0x00003000;
        private const int GWL_STYLE = -16;
        private const int GWL_EXSTYLE = -20;
        private const uint GA_ROOT = 2;
        private const int DWMWA_CLOAKED = 14;
        private const uint RDW_INVALIDATE = 0x0001;
        private const uint RDW_UPDATENOW = 0x0100;
        private const uint RDW_ALLCHILDREN = 0x0080;
        private const uint RDW_FRAME = 0x0400;

        internal static IReadOnlyList<NativeWindowCandidate> EnumerateCandidates()
        {
            uint unityProcessId = (uint)Process.GetCurrentProcess().Id;
            List<NativeWindowCandidate> candidates = new List<NativeWindowCandidate>();
            EnumWindows(
                (window, _) =>
                {
                    if (!IsWindowVisible(window))
                    {
                        return true;
                    }

                    uint processId;
                    GetWindowThreadProcessId(window, out processId);
                    if (processId == 0 || processId == unityProcessId)
                    {
                        return true;
                    }

                    int cloaked;
                    if (DwmGetWindowAttribute(
                            window,
                            DWMWA_CLOAKED,
                            out cloaked,
                            Marshal.SizeOf<int>()) == 0
                        && cloaked != 0)
                    {
                        return true;
                    }

                    RECT rect;
                    if (!GetWindowRect(window, out rect)
                        || rect.Right - rect.Left < 80
                        || rect.Bottom - rect.Top < 60)
                    {
                        return true;
                    }

                    string title = GetWindowTextValue(window);
                    if (string.IsNullOrWhiteSpace(title))
                    {
                        return true;
                    }

                    string processName = GetProcessName(processId);
                    string className = GetClassNameValue(window);
                    candidates.Add(new NativeWindowCandidate(
                        window,
                        processId,
                        title,
                        processName,
                        className));
                    return true;
                },
                IntPtr.Zero);

            return candidates
                .OrderBy(candidate => candidate.ProcessName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(candidate => candidate.Title, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        internal static IntPtr ResolveUnityMainWindow()
        {
            Process current = Process.GetCurrentProcess();
            current.Refresh();
            IntPtr mainWindow = current.MainWindowHandle;
            if (mainWindow != IntPtr.Zero)
            {
                return GetAncestor(mainWindow, GA_ROOT);
            }

            uint currentProcessId = (uint)current.Id;
            IntPtr bestWindow = IntPtr.Zero;
            long bestArea = 0;
            EnumWindows(
                (window, _) =>
                {
                    uint processId;
                    GetWindowThreadProcessId(window, out processId);
                    if (processId != currentProcessId || !IsWindowVisible(window))
                    {
                        return true;
                    }

                    RECT rect;
                    if (!GetWindowRect(window, out rect))
                    {
                        return true;
                    }

                    long area = (long)(rect.Right - rect.Left) * (rect.Bottom - rect.Top);
                    if (area > bestArea)
                    {
                        bestArea = area;
                        bestWindow = window;
                    }

                    return true;
                },
                IntPtr.Zero);
            return bestWindow;
        }

        internal static IntPtr ResolveUnityContainerWindow(
            Rect screenRectInPoints,
            float pixelsPerPoint)
        {
            POINT screenPoint = new POINT(
                Mathf.RoundToInt(screenRectInPoints.center.x * pixelsPerPoint),
                Mathf.RoundToInt(screenRectInPoints.center.y * pixelsPerPoint));
            IntPtr windowAtPoint = WindowFromPoint(screenPoint);
            IntPtr root = windowAtPoint == IntPtr.Zero
                ? IntPtr.Zero
                : GetAncestor(windowAtPoint, GA_ROOT);
            uint currentProcessId = (uint)Process.GetCurrentProcess().Id;
            if (IsWindowOwnedByProcess(root, currentProcessId))
            {
                return root;
            }

            IntPtr bestWindow = IntPtr.Zero;
            long bestArea = long.MaxValue;
            EnumWindows(
                (window, _) =>
                {
                    if (!IsWindowVisible(window)
                        || !IsWindowOwnedByProcess(window, currentProcessId))
                    {
                        return true;
                    }

                    RECT rect;
                    if (!GetWindowRect(window, out rect)
                        || screenPoint.X < rect.Left
                        || screenPoint.X >= rect.Right
                        || screenPoint.Y < rect.Top
                        || screenPoint.Y >= rect.Bottom)
                    {
                        return true;
                    }

                    long area = (long)(rect.Right - rect.Left) * (rect.Bottom - rect.Top);
                    if (area < bestArea)
                    {
                        bestArea = area;
                        bestWindow = window;
                    }

                    return true;
                },
                IntPtr.Zero);
            return bestWindow != IntPtr.Zero ? bestWindow : ResolveUnityMainWindow();
        }

        internal static string GetEmbeddingBlockReason(IntPtr target)
        {
            uint targetProcessId;
            GetWindowThreadProcessId(target, out targetProcessId);
            int currentIntegrity;
            int currentError;
            int targetIntegrity;
            int targetError;
            bool hasCurrentIntegrity = TryGetProcessIntegrityLevel(
                (uint)Process.GetCurrentProcess().Id,
                out currentIntegrity,
                out currentError);
            bool hasTargetIntegrity = TryGetProcessIntegrityLevel(
                targetProcessId,
                out targetIntegrity,
                out targetError);
            string targetName = GetProcessName(targetProcessId);

            if (hasCurrentIntegrity
                && hasTargetIntegrity
                && targetIntegrity > currentIntegrity)
            {
                return ComposeIntegrityMismatchMessage(
                    targetName,
                    currentIntegrity,
                    targetIntegrity);
            }

            if (hasCurrentIntegrity
                && currentIntegrity < SECURITY_MANDATORY_HIGH_RID
                && !hasTargetIntegrity
                && targetError == 5)
            {
                return targetName
                       + " is protected by a higher Windows integrity level than Unity. "
                       + "Close and relaunch it normally, or run Unity as administrator.";
            }

            return string.Empty;
        }

        internal static string ComposeIntegrityMismatchMessage(
            string targetName,
            int unityIntegrity,
            int targetIntegrity)
        {
            return targetName
                   + " is running at "
                   + DescribeIntegrity(targetIntegrity)
                   + " integrity while Unity is "
                   + DescribeIntegrity(unityIntegrity)
                   + ". Close and relaunch "
                   + targetName
                   + " normally, or run Unity at the same elevation level.";
        }

        internal static void RedrawEmbeddedWindow(IntPtr window)
        {
            RedrawWindow(
                window,
                IntPtr.Zero,
                IntPtr.Zero,
                RDW_INVALIDATE | RDW_UPDATENOW | RDW_ALLCHILDREN | RDW_FRAME);
            UpdateWindow(window);
        }

        internal static IntPtr CreateHostWindow(IntPtr unityWindow)
        {
            return CreateWindowEx(
                0,
                "STATIC",
                string.Empty,
                (uint)(WS_CHILD | WS_CLIPCHILDREN | WS_CLIPSIBLINGS),
                0,
                0,
                1,
                1,
                unityWindow,
                IntPtr.Zero,
                IntPtr.Zero,
                IntPtr.Zero);
        }

        internal static long GetWindowStyle(IntPtr window)
        {
            return GetWindowLongPtr(window, GWL_STYLE).ToInt64();
        }

        internal static long GetWindowExtendedStyle(IntPtr window)
        {
            return GetWindowLongPtr(window, GWL_EXSTYLE).ToInt64();
        }

        internal static void SetWindowStyle(IntPtr window, long style)
        {
            SetWindowLongPtr(window, GWL_STYLE, new IntPtr(style));
        }

        internal static void SetWindowExtendedStyle(IntPtr window, long style)
        {
            SetWindowLongPtr(window, GWL_EXSTYLE, new IntPtr(style));
        }

        internal static void RestoreWindow(
            IntPtr target,
            IntPtr host,
            IntPtr originalParent,
            long originalStyle,
            long originalExtendedStyle,
            RECT originalRect,
            WINDOWPLACEMENT originalPlacement)
        {
            if (IsWindow(target))
            {
                ShowWindow(target, SW_HIDE);
                SetParent(target, originalParent);
                SetWindowStyle(target, originalStyle);
                SetWindowExtendedStyle(target, originalExtendedStyle);
                SetWindowPos(
                    target,
                    IntPtr.Zero,
                    originalRect.Left,
                    originalRect.Top,
                    Math.Max(1, originalRect.Right - originalRect.Left),
                    Math.Max(1, originalRect.Bottom - originalRect.Top),
                    SWP_NOACTIVATE | SWP_NOZORDER | SWP_FRAMECHANGED);

                originalPlacement.Length = Marshal.SizeOf<WINDOWPLACEMENT>();
                if (originalPlacement.ShowCmd == 0)
                {
                    originalPlacement.ShowCmd = SW_SHOWNORMAL;
                }

                SetWindowPlacement(target, ref originalPlacement);
                ShowWindow(target, originalPlacement.ShowCmd);
            }

            if (IsWindow(host))
            {
                DestroyWindow(host);
            }
        }

        internal static Win32Exception CreateWin32Exception(string message)
        {
            int error = Marshal.GetLastWin32Error();
            return new Win32Exception(error, message + " Win32 error " + error + ".");
        }

        internal static Win32Exception CreateWin32Exception(string message, int error)
        {
            return new Win32Exception(error, message + " Win32 error " + error + ".");
        }

        private static string GetWindowTextValue(IntPtr window)
        {
            int length = GetWindowTextLength(window);
            StringBuilder builder = new StringBuilder(Math.Max(2, length + 1));
            GetWindowText(window, builder, builder.Capacity);
            return builder.ToString();
        }

        private static string GetClassNameValue(IntPtr window)
        {
            StringBuilder builder = new StringBuilder(256);
            GetClassName(window, builder, builder.Capacity);
            return builder.ToString();
        }

        private static string GetProcessName(uint processId)
        {
            try
            {
                using (Process process = Process.GetProcessById((int)processId))
                {
                    return process.ProcessName;
                }
            }
            catch
            {
                return "Application";
            }
        }

        private static bool IsWindowOwnedByProcess(IntPtr window, uint processId)
        {
            if (window == IntPtr.Zero)
            {
                return false;
            }

            uint ownerProcessId;
            GetWindowThreadProcessId(window, out ownerProcessId);
            return ownerProcessId == processId;
        }

        private static bool TryGetProcessIntegrityLevel(
            uint processId,
            out int integrityLevel,
            out int error)
        {
            integrityLevel = 0;
            error = 0;
            IntPtr process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
            if (process == IntPtr.Zero)
            {
                error = Marshal.GetLastWin32Error();
                return false;
            }

            IntPtr token = IntPtr.Zero;
            IntPtr buffer = IntPtr.Zero;
            try
            {
                if (!OpenProcessToken(process, TOKEN_QUERY, out token))
                {
                    error = Marshal.GetLastWin32Error();
                    return false;
                }

                int requiredLength;
                GetTokenInformation(
                    token,
                    TOKEN_INTEGRITY_LEVEL,
                    IntPtr.Zero,
                    0,
                    out requiredLength);
                if (requiredLength <= 0)
                {
                    error = Marshal.GetLastWin32Error();
                    return false;
                }

                buffer = Marshal.AllocHGlobal(requiredLength);
                if (!GetTokenInformation(
                        token,
                        TOKEN_INTEGRITY_LEVEL,
                        buffer,
                        requiredLength,
                        out requiredLength))
                {
                    error = Marshal.GetLastWin32Error();
                    return false;
                }

                TOKEN_MANDATORY_LABEL label =
                    Marshal.PtrToStructure<TOKEN_MANDATORY_LABEL>(buffer);
                IntPtr countPointer = GetSidSubAuthorityCount(label.Label.Sid);
                if (countPointer == IntPtr.Zero)
                {
                    error = Marshal.GetLastWin32Error();
                    return false;
                }

                byte count = Marshal.ReadByte(countPointer);
                if (count == 0)
                {
                    return false;
                }

                IntPtr ridPointer = GetSidSubAuthority(label.Label.Sid, (uint)(count - 1));
                if (ridPointer == IntPtr.Zero)
                {
                    error = Marshal.GetLastWin32Error();
                    return false;
                }

                integrityLevel = Marshal.ReadInt32(ridPointer);
                return true;
            }
            finally
            {
                if (buffer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(buffer);
                }

                if (token != IntPtr.Zero)
                {
                    CloseHandle(token);
                }

                CloseHandle(process);
            }
        }

        private static string DescribeIntegrity(int integrityLevel)
        {
            if (integrityLevel >= 0x00004000)
            {
                return "system";
            }

            if (integrityLevel >= SECURITY_MANDATORY_HIGH_RID)
            {
                return "administrator";
            }

            if (integrityLevel >= 0x00002000)
            {
                return "standard";
            }

            return "low";
        }

        private static IntPtr GetWindowLongPtr(IntPtr window, int index)
        {
            return IntPtr.Size == 8
                ? GetWindowLongPtr64(window, index)
                : new IntPtr(GetWindowLong32(window, index));
        }

        private static IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr value)
        {
            return IntPtr.Size == 8
                ? SetWindowLongPtr64(window, index, value)
                : new IntPtr(SetWindowLong32(window, index, value.ToInt32()));
        }

        internal delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);

        [StructLayout(LayoutKind.Sequential)]
        internal struct POINT
        {
            internal int X;
            internal int Y;

            internal POINT(int x, int y)
            {
                X = x;
                Y = y;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct RECT
        {
            internal int Left;
            internal int Top;
            internal int Right;
            internal int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct WINDOWPLACEMENT
        {
            internal int Length;
            internal int Flags;
            internal int ShowCmd;
            internal POINT MinPosition;
            internal POINT MaxPosition;
            internal RECT NormalPosition;

            internal static WINDOWPLACEMENT Create()
            {
                return new WINDOWPLACEMENT
                {
                    Length = Marshal.SizeOf<WINDOWPLACEMENT>()
                };
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SID_AND_ATTRIBUTES
        {
            internal IntPtr Sid;
            internal uint Attributes;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct TOKEN_MANDATORY_LABEL
        {
            internal SID_AND_ATTRIBUTES Label;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsWindow(IntPtr window);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr window);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr window, StringBuilder value, int maximumCount);

        [DllImport("user32.dll")]
        private static extern int GetWindowTextLength(IntPtr window);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr window, StringBuilder value, int maximumCount);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

        [DllImport("user32.dll")]
        private static extern IntPtr GetAncestor(IntPtr window, uint flags);

        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(POINT point);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern IntPtr GetParent(IntPtr child);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern IntPtr SetParent(IntPtr child, IntPtr newParent);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetWindowRect(IntPtr window, out RECT rect);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ScreenToClient(IntPtr window, ref POINT point);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetWindowPos(
            IntPtr window,
            IntPtr insertAfter,
            int x,
            int y,
            int width,
            int height,
            uint flags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ShowWindow(IntPtr window, int command);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyWindow(IntPtr window);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateWindowEx(
            uint extendedStyle,
            string className,
            string windowName,
            uint style,
            int x,
            int y,
            int width,
            int height,
            IntPtr parent,
            IntPtr menu,
            IntPtr instance,
            IntPtr parameter);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetWindowPlacement(IntPtr window, ref WINDOWPLACEMENT placement);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPlacement(IntPtr window, ref WINDOWPLACEMENT placement);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetForegroundWindow(IntPtr window);

        [DllImport("user32.dll")]
        internal static extern IntPtr SetFocus(IntPtr window);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool RedrawWindow(
            IntPtr window,
            IntPtr updateRect,
            IntPtr updateRegion,
            uint flags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UpdateWindow(IntPtr window);

        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        private static extern int GetWindowLong32(IntPtr window, int index);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        private static extern IntPtr GetWindowLongPtr64(IntPtr window, int index);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
        private static extern int SetWindowLong32(IntPtr window, int index, int value);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr64(IntPtr window, int index, IntPtr value);

        [DllImport("dwmapi.dll")]
        private static extern int DwmGetWindowAttribute(
            IntPtr window,
            int attribute,
            out int value,
            int valueSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(
            uint desiredAccess,
            [MarshalAs(UnmanagedType.Bool)] bool inheritHandle,
            uint processId);

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr handle);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool OpenProcessToken(
            IntPtr processHandle,
            uint desiredAccess,
            out IntPtr tokenHandle);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetTokenInformation(
            IntPtr tokenHandle,
            int tokenInformationClass,
            IntPtr tokenInformation,
            int tokenInformationLength,
            out int returnLength);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern IntPtr GetSidSubAuthorityCount(IntPtr sid);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern IntPtr GetSidSubAuthority(IntPtr sid, uint subAuthorityIndex);
    }
}
