using System;
using System.Runtime.InteropServices;

namespace DansToolbox.EditorTools.NativeWindowDock
{
    internal sealed class NativeWindowThumbnailData
    {
        internal NativeWindowThumbnailData(IntPtr handle, int width, int height, byte[] rgbaPixels)
        {
            Handle = handle;
            Width = width;
            Height = height;
            RgbaPixels = rgbaPixels;
        }

        internal IntPtr Handle { get; }
        internal int Width { get; }
        internal int Height { get; }
        internal byte[] RgbaPixels { get; }
        internal bool Succeeded => RgbaPixels != null && RgbaPixels.Length == Width * Height * 4;
    }

    internal static class NativeWindowThumbnailCapture
    {
        private const uint PW_RENDERFULLCONTENT = 0x00000002;
        private const int DIB_RGB_COLORS = 0;
        private const int BI_RGB = 0;
        private const int HALFTONE = 4;
        private const uint SRCCOPY = 0x00CC0020;

        internal static NativeWindowThumbnailData Capture(
            IntPtr window,
            int thumbnailWidth,
            int thumbnailHeight)
        {
            thumbnailWidth = Math.Max(1, thumbnailWidth);
            thumbnailHeight = Math.Max(1, thumbnailHeight);
            if (window == IntPtr.Zero || !IsWindow(window))
            {
                return Failed(window, thumbnailWidth, thumbnailHeight);
            }

            RECT sourceRect;
            if (!GetWindowRect(window, out sourceRect))
            {
                return Failed(window, thumbnailWidth, thumbnailHeight);
            }

            int sourceWidth = sourceRect.Right - sourceRect.Left;
            int sourceHeight = sourceRect.Bottom - sourceRect.Top;
            if (sourceWidth < 2 || sourceHeight < 2 || sourceWidth > 8192 || sourceHeight > 8192)
            {
                return Failed(window, thumbnailWidth, thumbnailHeight);
            }

            IntPtr screenDc = IntPtr.Zero;
            IntPtr sourceDc = IntPtr.Zero;
            IntPtr sourceBitmap = IntPtr.Zero;
            IntPtr previousSourceBitmap = IntPtr.Zero;
            IntPtr thumbnailDc = IntPtr.Zero;
            IntPtr thumbnailBitmap = IntPtr.Zero;
            IntPtr previousThumbnailBitmap = IntPtr.Zero;
            try
            {
                screenDc = GetDC(IntPtr.Zero);
                sourceDc = CreateCompatibleDC(screenDc);
                sourceBitmap = CreateCompatibleBitmap(screenDc, sourceWidth, sourceHeight);
                if (screenDc == IntPtr.Zero || sourceDc == IntPtr.Zero || sourceBitmap == IntPtr.Zero)
                {
                    return Failed(window, thumbnailWidth, thumbnailHeight);
                }

                previousSourceBitmap = SelectObject(sourceDc, sourceBitmap);
                bool captured = PrintWindow(window, sourceDc, PW_RENDERFULLCONTENT)
                                || PrintWindow(window, sourceDc, 0);
                if (!captured)
                {
                    return Failed(window, thumbnailWidth, thumbnailHeight);
                }

                BITMAPINFO bitmapInfo = BITMAPINFO.Create(thumbnailWidth, thumbnailHeight);
                IntPtr pixelBuffer;
                thumbnailBitmap = CreateDIBSection(
                    screenDc,
                    ref bitmapInfo,
                    DIB_RGB_COLORS,
                    out pixelBuffer,
                    IntPtr.Zero,
                    0);
                thumbnailDc = CreateCompatibleDC(screenDc);
                if (thumbnailBitmap == IntPtr.Zero
                    || thumbnailDc == IntPtr.Zero
                    || pixelBuffer == IntPtr.Zero)
                {
                    return Failed(window, thumbnailWidth, thumbnailHeight);
                }

                previousThumbnailBitmap = SelectObject(thumbnailDc, thumbnailBitmap);
                SetStretchBltMode(thumbnailDc, HALFTONE);
                if (!StretchBlt(
                        thumbnailDc,
                        0,
                        0,
                        thumbnailWidth,
                        thumbnailHeight,
                        sourceDc,
                        0,
                        0,
                        sourceWidth,
                        sourceHeight,
                        SRCCOPY))
                {
                    return Failed(window, thumbnailWidth, thumbnailHeight);
                }

                byte[] pixels = new byte[thumbnailWidth * thumbnailHeight * 4];
                Marshal.Copy(pixelBuffer, pixels, 0, pixels.Length);
                bool hasVisiblePixel = false;
                for (int index = 0; index < pixels.Length; index += 4)
                {
                    byte blue = pixels[index];
                    byte red = pixels[index + 2];
                    pixels[index] = red;
                    pixels[index + 2] = blue;
                    pixels[index + 3] = 255;
                    hasVisiblePixel |= red > 3 || pixels[index + 1] > 3 || blue > 3;
                }

                return hasVisiblePixel
                    ? new NativeWindowThumbnailData(window, thumbnailWidth, thumbnailHeight, pixels)
                    : Failed(window, thumbnailWidth, thumbnailHeight);
            }
            finally
            {
                if (previousThumbnailBitmap != IntPtr.Zero && thumbnailDc != IntPtr.Zero)
                {
                    SelectObject(thumbnailDc, previousThumbnailBitmap);
                }

                if (thumbnailBitmap != IntPtr.Zero)
                {
                    DeleteObject(thumbnailBitmap);
                }

                if (thumbnailDc != IntPtr.Zero)
                {
                    DeleteDC(thumbnailDc);
                }

                if (previousSourceBitmap != IntPtr.Zero && sourceDc != IntPtr.Zero)
                {
                    SelectObject(sourceDc, previousSourceBitmap);
                }

                if (sourceBitmap != IntPtr.Zero)
                {
                    DeleteObject(sourceBitmap);
                }

                if (sourceDc != IntPtr.Zero)
                {
                    DeleteDC(sourceDc);
                }

                if (screenDc != IntPtr.Zero)
                {
                    ReleaseDC(IntPtr.Zero, screenDc);
                }
            }
        }

        private static NativeWindowThumbnailData Failed(IntPtr window, int width, int height)
        {
            return new NativeWindowThumbnailData(window, width, height, null);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            internal int Left;
            internal int Top;
            internal int Right;
            internal int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFOHEADER
        {
            internal uint Size;
            internal int Width;
            internal int Height;
            internal ushort Planes;
            internal ushort BitCount;
            internal int Compression;
            internal uint SizeImage;
            internal int XPelsPerMeter;
            internal int YPelsPerMeter;
            internal uint ClrUsed;
            internal uint ClrImportant;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RGBQUAD
        {
            internal byte Blue;
            internal byte Green;
            internal byte Red;
            internal byte Reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFO
        {
            internal BITMAPINFOHEADER Header;
            internal RGBQUAD Colors;

            internal static BITMAPINFO Create(int width, int height)
            {
                return new BITMAPINFO
                {
                    Header = new BITMAPINFOHEADER
                    {
                        Size = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
                        Width = width,
                        // Unity's raw texture data starts at the bottom scanline.
                        // A positive DIB height stores rows in that same order.
                        Height = height,
                        Planes = 1,
                        BitCount = 32,
                        Compression = BI_RGB,
                        SizeImage = (uint)(width * height * 4)
                    }
                };
            }
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindow(IntPtr window);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr window, out RECT rect);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr window);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr window, IntPtr deviceContext);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PrintWindow(IntPtr window, IntPtr deviceContext, uint flags);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr deviceContext);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleBitmap(
            IntPtr deviceContext,
            int width,
            int height);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateDIBSection(
            IntPtr deviceContext,
            ref BITMAPINFO bitmapInfo,
            uint usage,
            out IntPtr bits,
            IntPtr section,
            uint offset);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr deviceContext, IntPtr value);

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteObject(IntPtr value);

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteDC(IntPtr deviceContext);

        [DllImport("gdi32.dll")]
        private static extern int SetStretchBltMode(IntPtr deviceContext, int mode);

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool StretchBlt(
            IntPtr destination,
            int destinationX,
            int destinationY,
            int destinationWidth,
            int destinationHeight,
            IntPtr source,
            int sourceX,
            int sourceY,
            int sourceWidth,
            int sourceHeight,
            uint operation);
    }
}
