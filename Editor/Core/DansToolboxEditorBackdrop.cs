using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UnityEngine;

namespace DansToolbox.Editor
{
    internal static class DansToolboxEditorBackdrop
    {
        private const int BlurScale = 12;

        internal static Texture2D CaptureBlurred()
        {
#if UNITY_EDITOR_WIN
            Texture2D capture = CaptureMainWindow();
            if (capture == null)
            {
                return null;
            }

            int width = Mathf.Max(96, capture.width / BlurScale);
            int height = Mathf.Max(54, capture.height / BlurScale);
            RenderTexture previous = RenderTexture.active;
            RenderTexture reduced = RenderTexture.GetTemporary(
                width,
                height,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Linear);
            reduced.filterMode = FilterMode.Bilinear;

            try
            {
                Graphics.Blit(capture, reduced);
                RenderTexture.active = reduced;
                Texture2D blurred = new Texture2D(
                    width,
                    height,
                    TextureFormat.RGBA32,
                    false,
                    true)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };
                blurred.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
                blurred.Apply(false, true);
                return blurred;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(reduced);
                UnityEngine.Object.DestroyImmediate(capture);
            }
#else
            return null;
#endif
        }

#if UNITY_EDITOR_WIN
        private const int Srccopy = 0x00CC0020;
        private const int CaptureBlt = 0x40000000;
        private const uint DibRgbColors = 0;
        private const uint PwRenderFullContent = 2;

        private static Texture2D CaptureMainWindow()
        {
            IntPtr window = FindMainProcessWindow();
            if (window == IntPtr.Zero || !GetWindowRect(window, out NativeRect rect))
            {
                return null;
            }

            int width = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;
            if (width < 2 || height < 2)
            {
                return null;
            }

            IntPtr sourceDc = GetWindowDC(window);
            if (sourceDc == IntPtr.Zero)
            {
                return null;
            }

            IntPtr memoryDc = CreateCompatibleDC(sourceDc);
            IntPtr bitmap = CreateCompatibleBitmap(sourceDc, width, height);
            if (memoryDc == IntPtr.Zero || bitmap == IntPtr.Zero)
            {
                if (bitmap != IntPtr.Zero)
                {
                    DeleteObject(bitmap);
                }

                if (memoryDc != IntPtr.Zero)
                {
                    DeleteDC(memoryDc);
                }

                ReleaseDC(window, sourceDc);
                return null;
            }

            IntPtr previousBitmap = SelectObject(memoryDc, bitmap);
            try
            {
                bool captured = PrintWindow(window, memoryDc, PwRenderFullContent);
                if (!captured)
                {
                    captured = BitBlt(
                        memoryDc,
                        0,
                        0,
                        width,
                        height,
                        sourceDc,
                        0,
                        0,
                        Srccopy | CaptureBlt);
                }

                if (!captured)
                {
                    return null;
                }

                BitmapInfo info = new BitmapInfo
                {
                    Header = new BitmapInfoHeader
                    {
                        Size = (uint)Marshal.SizeOf<BitmapInfoHeader>(),
                        Width = width,
                        Height = height,
                        Planes = 1,
                        BitCount = 32,
                        Compression = 0,
                        SizeImage = (uint)(width * height * 4)
                    }
                };
                byte[] pixels = new byte[width * height * 4];
                GCHandle pinned = GCHandle.Alloc(pixels, GCHandleType.Pinned);
                try
                {
                    int scanLines = GetDIBits(
                        memoryDc,
                        bitmap,
                        0,
                        (uint)height,
                        pinned.AddrOfPinnedObject(),
                        ref info,
                        DibRgbColors);
                    if (scanLines == 0)
                    {
                        return null;
                    }
                }
                finally
                {
                    pinned.Free();
                }

                Texture2D texture = new Texture2D(
                    width,
                    height,
                    TextureFormat.BGRA32,
                    false,
                    true)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };
                texture.LoadRawTextureData(pixels);
                texture.Apply(false, true);
                return texture;
            }
            finally
            {
                SelectObject(memoryDc, previousBitmap);
                DeleteObject(bitmap);
                DeleteDC(memoryDc);
                ReleaseDC(window, sourceDc);
            }
        }

        private static IntPtr FindMainProcessWindow()
        {
            uint processId = (uint)Process.GetCurrentProcess().Id;
            IntPtr bestWindow = IntPtr.Zero;
            long bestArea = 0;
            EnumWindows(
                (window, _) =>
                {
                    GetWindowThreadProcessId(window, out uint windowProcessId);
                    if (windowProcessId != processId || !IsWindowVisible(window) ||
                        !GetWindowRect(window, out NativeRect rect))
                    {
                        return true;
                    }

                    long width = Math.Max(0, rect.Right - rect.Left);
                    long height = Math.Max(0, rect.Bottom - rect.Top);
                    long area = width * height;
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

        private delegate bool EnumWindowsCallback(IntPtr window, IntPtr parameter);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            internal int Left;
            internal int Top;
            internal int Right;
            internal int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BitmapInfoHeader
        {
            internal uint Size;
            internal int Width;
            internal int Height;
            internal ushort Planes;
            internal ushort BitCount;
            internal uint Compression;
            internal uint SizeImage;
            internal int XPixelsPerMeter;
            internal int YPixelsPerMeter;
            internal uint ColorsUsed;
            internal uint ColorsImportant;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BitmapInfo
        {
            internal BitmapInfoHeader Header;
            internal uint Colors;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumWindows(EnumWindowsCallback callback, IntPtr parameter);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr window);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr window, out NativeRect rect);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowDC(IntPtr window);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr window, IntPtr dc);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PrintWindow(IntPtr window, IntPtr dc, uint flags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool BitBlt(
            IntPtr destination,
            int x,
            int y,
            int width,
            int height,
            IntPtr source,
            int sourceX,
            int sourceY,
            int operation);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr dc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr dc, int width, int height);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr dc, IntPtr value);

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteObject(IntPtr value);

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteDC(IntPtr dc);

        [DllImport("gdi32.dll")]
        private static extern int GetDIBits(
            IntPtr dc,
            IntPtr bitmap,
            uint start,
            uint lines,
            IntPtr bits,
            ref BitmapInfo info,
            uint usage);
#endif
    }
}
