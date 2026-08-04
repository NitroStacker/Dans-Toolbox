using System;
using System.Linq;
using DansToolbox.EditorTools.Artboard;
using NUnit.Framework;
using UnityEngine;

namespace DansToolbox.Editor.Tests
{
    public sealed class ArtboardTests
    {
        [Test]
        public void Document_MaintainsACelForEveryLayerAndFrame()
        {
            ArtboardAsset asset = ArtboardAsset.CreateDocument(32, 24, ArtboardMode.Animation);
            try
            {
                int layer = asset.AddLayer(0);
                int frame = asset.AddFrame(0, true);

                Assert.That(asset.Width, Is.EqualTo(32));
                Assert.That(asset.Height, Is.EqualTo(24));
                Assert.That(asset.Layers.Count, Is.EqualTo(2));
                Assert.That(asset.Frames.Count, Is.EqualTo(2));
                Assert.That(asset.GetCel(frame, layer), Is.Not.Null);
                Assert.That(asset.Frames.All(item => item.Cels.Count == asset.Layers.Count), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void PixelEngine_FloodFillStopsAtOpaqueBoundary()
        {
            const int width = 5;
            const int height = 5;
            Color32[] pixels = ArtboardPixelEngine.Blank(width, height);
            Color32 wall = new Color32(255, 255, 255, 255);
            for (int y = 0; y < height; y++) pixels[y * width + 2] = wall;

            int changed = ArtboardPixelEngine.FloodFill(
                pixels, width, height, 0, 0, new Color32(255, 0, 0, 255));

            Assert.That(changed, Is.EqualTo(10));
            Assert.That(pixels[4], Is.EqualTo(default(Color32)));
            Assert.That(pixels[2], Is.EqualTo(wall));
        }

        [Test]
        public void PixelEngine_NearestScaleReplicatesEverySourcePixel()
        {
            Color32 red = new Color32(255, 0, 0, 255);
            Color32 blue = new Color32(0, 0, 255, 255);
            Color32[] scaled = ArtboardPixelEngine.ScaleNearest(new[] { red, blue }, 2, 1, 3);

            Assert.That(scaled.Length, Is.EqualTo(18));
            for (int y = 0; y < 3; y++)
            {
                Assert.That(scaled.Skip(y * 6).Take(3), Is.All.EqualTo(red));
                Assert.That(scaled.Skip(y * 6 + 3).Take(3), Is.All.EqualTo(blue));
            }
        }

        [Test]
        public void PixelEngine_MirroredStrokeTouchesBothSides()
        {
            Color32[] pixels = ArtboardPixelEngine.Blank(8, 4);
            Color32 color = new Color32(10, 20, 30, 255);

            ArtboardPixelEngine.DrawStroke(
                pixels, 8, 4, new Vector2Int(1, 1), new Vector2Int(1, 1),
                color, 1, false, false, true, false);

            Assert.That(pixels[1 * 8 + 1], Is.EqualTo(color));
            Assert.That(pixels[1 * 8 + 6], Is.EqualTo(color));
        }

        [Test]
        public void BrushIndicator_MatchesTheStampedPixelFootprintAndStaysReadable()
        {
            Rect artboard = new Rect(20f, 30f, 80f, 80f);

            Rect exact = ArtboardWindow.CalculateBrushIndicatorRect(
                artboard, 8, new Vector2Int(3, 4), 3, 10f);
            Rect distant = ArtboardWindow.CalculateBrushIndicatorRect(
                artboard, 8, new Vector2Int(3, 4), 1, 0.5f);

            Assert.That(exact, Is.EqualTo(new Rect(40f, 50f, 30f, 30f)));
            Assert.That(distant.width, Is.EqualTo(10f));
            Assert.That(distant.height, Is.EqualTo(10f));
            Assert.That(distant.center.x, Is.EqualTo(21.75f).Within(0.001f));
            Assert.That(distant.center.y, Is.EqualTo(31.75f).Within(0.001f));
        }

        [Test]
        public void SpriteSheet_PacksFramesInStableVisualOrder()
        {
            Color32[] frames =
            {
                new Color32(1, 0, 0, 255),
                new Color32(2, 0, 0, 255),
                new Color32(3, 0, 0, 255),
                new Color32(4, 0, 0, 255)
            };
            ArtboardSheet sheet = ArtboardExportService.BuildSheet(
                frames.Select(pixel => new[] { pixel }).ToArray(), 1, 1);

            Assert.That(sheet.Width, Is.EqualTo(2));
            Assert.That(sheet.Height, Is.EqualTo(2));
            Assert.That(sheet.Pixels[2].r, Is.EqualTo(1));
            Assert.That(sheet.Pixels[3].r, Is.EqualTo(2));
            Assert.That(sheet.Pixels[0].r, Is.EqualTo(3));
            Assert.That(sheet.Pixels[1].r, Is.EqualTo(4));
        }

        [Test]
        public void ExportGuard_BlocksTexturesBeyondUnitySafeLimit()
        {
            ArtboardAsset asset = ArtboardAsset.CreateDocument(4096, 4096, ArtboardMode.PixelArt);
            try
            {
                Assert.That(ArtboardExportService.CanExport(asset, 4, false, out _), Is.True);
                Assert.That(ArtboardExportService.CanExport(asset, 5, false, out string reason), Is.False);
                Assert.That(reason, Does.Contain("16384"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void ToolboxCatalog_RegistersArtboardAsACreateTool()
        {
            Assert.That(DansToolboxTools.Find(DansToolboxTools.ArtboardId).Name, Is.EqualTo("Artboard"));
            DansToolboxLaunchDescriptor launcher = DansToolboxToolLauncher.Find(DansToolboxTools.ArtboardId);
            Assert.That(launcher.Group, Is.EqualTo(DansToolboxToolGroup.Create));
            Assert.That(launcher.TypeName, Does.Contain("DansToolbox.Artboard.Editor"));
        }
    }
}
