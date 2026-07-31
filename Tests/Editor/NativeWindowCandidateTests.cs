using System;
using NUnit.Framework;

namespace DansToolbox.EditorTools.NativeWindowDock.Tests
{
    internal sealed class NativeWindowCandidateTests
    {
        [Test]
        public void ComposeDisplayLabel_NormalizesWhitespace()
        {
            string result = NativeWindowCandidate.ComposeDisplayLabel(
                "chrome",
                "  Battle\tSoccer\nReference  ");

            Assert.AreEqual("chrome  /  Battle Soccer Reference", result);
        }

        [Test]
        public void ComposeDisplayLabel_ProvidesUsefulFallbacks()
        {
            string result = NativeWindowCandidate.ComposeDisplayLabel("", "");

            Assert.AreEqual("Application  /  Untitled window", result);
        }

        [Test]
        public void NormalizeDisplayText_TruncatesWithEllipsis()
        {
            string result = NativeWindowCandidate.NormalizeDisplayText("1234567890", 6);

            Assert.AreEqual("12345…", result);
        }

        [Test]
        public void ComposeIntegrityMismatchMessage_NamesBothIntegrityLevels()
        {
            string result = NativeWindowInterop.ComposeIntegrityMismatchMessage(
                "Bezi",
                0x00002000,
                0x00003000);

            StringAssert.Contains("Bezi is running at administrator integrity", result);
            StringAssert.Contains("Unity is standard", result);
        }

        [Test]
        public void CropBounds_ExpandAndOffsetTheEmbeddedWindow()
        {
            NativeWindowCrop crop = new NativeWindowCrop(300, 20, 200, 10);

            UnityEngine.RectInt bounds = crop.CalculateTargetBounds(1000, 600, 1.5f);

            Assert.AreEqual(new UnityEngine.RectInt(-450, -30, 1750, 645), bounds);
        }

        [Test]
        public void Crop_ClampsNegativeMargins()
        {
            NativeWindowCrop crop = new NativeWindowCrop(-10, -20, -30, -40);

            Assert.IsTrue(crop.IsEmpty);
        }

        [Test]
        public void CropBorderDrag_AdjustsTheSelectedEdges()
        {
            NativeWindowCrop start = new NativeWindowCrop(100, 50, 80, 30);

            NativeWindowCrop left = NativeWindowDockWindow.AdjustCropFromDrag(
                start,
                NativeWindowCropEdge.Left,
                new UnityEngine.Vector2(25, 0));
            NativeWindowCrop right = NativeWindowDockWindow.AdjustCropFromDrag(
                start,
                NativeWindowCropEdge.Right,
                new UnityEngine.Vector2(-15, 0));
            NativeWindowCrop top = NativeWindowDockWindow.AdjustCropFromDrag(
                start,
                NativeWindowCropEdge.Top,
                new UnityEngine.Vector2(0, 20));
            NativeWindowCrop bottom = NativeWindowDockWindow.AdjustCropFromDrag(
                start,
                NativeWindowCropEdge.Bottom,
                new UnityEngine.Vector2(0, -10));

            Assert.AreEqual(125, left.Left);
            Assert.AreEqual(95, right.Right);
            Assert.AreEqual(70, top.Top);
            Assert.AreEqual(40, bottom.Bottom);
        }

        [Test]
        public void WindowPickerGrid_RespondsFromOneToFourColumns()
        {
            Assert.AreEqual(1, NativeWindowDockWindow.CalculateWindowPickerColumnCount(160f));
            Assert.AreEqual(2, NativeWindowDockWindow.CalculateWindowPickerColumnCount(383f));
            Assert.AreEqual(4, NativeWindowDockWindow.CalculateWindowPickerColumnCount(760f));
            Assert.AreEqual(4, NativeWindowDockWindow.CalculateWindowPickerColumnCount(2000f));
        }

        [Test]
        public void ThumbnailCapture_RejectsAnInvalidWindowWithoutAllocatingPixels()
        {
            NativeWindowThumbnailData result = NativeWindowThumbnailCapture.Capture(
                IntPtr.Zero,
                320,
                180);

            Assert.IsFalse(result.Succeeded);
            Assert.IsNull(result.RgbaPixels);
        }

        [Test]
        public void PanelTitle_IdentifiesSlotAndAttachedApplication()
        {
            string result = NativeWindowDockWindow.ComposePanelTitle(2, "Discord");

            Assert.AreEqual("Native Dock 2  ·  Discord", result);
        }

        [Test]
        public void ClaimRegistry_PreventsTwoPanelsFromOwningTheSameWindow()
        {
            IntPtr target = new IntPtr(0x7F1234);
            const int firstPanel = 101;
            const int secondPanel = 202;

            try
            {
                Assert.IsTrue(NativeWindowClaimRegistry.TryClaim(target, firstPanel));
                Assert.IsFalse(NativeWindowClaimRegistry.TryClaim(target, secondPanel));
                Assert.IsTrue(
                    NativeWindowClaimRegistry.IsClaimedByOther(target, secondPanel));
                NativeWindowClaimRegistry.Release(target, secondPanel);
                Assert.IsFalse(NativeWindowClaimRegistry.TryClaim(target, secondPanel));
            }
            finally
            {
                NativeWindowClaimRegistry.Release(target, firstPanel);
                NativeWindowClaimRegistry.Release(target, secondPanel);
            }

            Assert.IsTrue(NativeWindowClaimRegistry.TryClaim(target, secondPanel));
            NativeWindowClaimRegistry.Release(target, secondPanel);
        }
    }
}
