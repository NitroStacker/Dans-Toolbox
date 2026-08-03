using NUnit.Framework;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DansToolbox.EditorTools.BetterScene
{
    internal sealed class BetterSceneTests
    {
        [Test]
        public void SnapVector_UsesPerAxisIncrements()
        {
            Vector3 result = BetterSceneOperations.SnapVector(
                new Vector3(1.24f, -2.61f, 8.9f),
                new Vector3(0.5f, 1f, 2f));

            Assert.That(result.x, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(result.y, Is.EqualTo(-3f).Within(0.0001f));
            Assert.That(result.z, Is.EqualTo(8f).Within(0.0001f));
        }

        [Test]
        public void CalculateAlignedPosition_AlignsRequestedBoundsEdge()
        {
            Bounds anchor = new Bounds(new Vector3(10f, 3f, 0f), new Vector3(4f, 4f, 4f));
            Bounds moving = new Bounds(new Vector3(2f, 9f, 0f), new Vector3(2f, 2f, 2f));

            Vector3 result = BetterSceneOperations.CalculateAlignedPosition(
                moving,
                anchor,
                BetterSceneAxis.Y,
                BetterSceneAlignAnchor.Minimum,
                moving.center);

            Assert.That(result.y, Is.EqualTo(2f).Within(0.0001f));
        }

        [Test]
        public void AlignSelection_IsUndoable()
        {
            var anchor = new GameObject("Anchor");
            var moving = new GameObject("Moving");
            anchor.transform.position = Vector3.zero;
            moving.transform.position = new Vector3(5f, 0f, 0f);
            try
            {
                Selection.objects = new Object[] { anchor, moving };
                Selection.activeGameObject = anchor;

                BetterSceneOperations.AlignSelection(BetterSceneAxis.X, BetterSceneAlignAnchor.Center);
                Assert.That(moving.transform.position.x, Is.EqualTo(0f).Within(0.0001f));

                Undo.PerformUndo();
                Assert.That(moving.transform.position.x, Is.EqualTo(5f).Within(0.0001f));
            }
            finally
            {
                Selection.objects = System.Array.Empty<Object>();
                Object.DestroyImmediate(anchor);
                Object.DestroyImmediate(moving);
                Undo.ClearAll();
            }
        }

        [Test]
        public void UniqueNames_AreCompactAndDeterministic()
        {
            string name = BetterSceneSettings.MakeUniqueName(
                "view",
                new[] { "VIEW", "VIEW 2" },
                "VIEW");

            Assert.That(name, Is.EqualTo("VIEW 3"));
        }

        [Test]
        public void Placement_AcceptsAuthoringAssetsAndRejectsMaterials()
        {
            var gameObject = new GameObject("Prefab Source");
            var mesh = new Mesh();
            var material = new Material(Shader.Find("Hidden/InternalErrorShader"));
            try
            {
                Assert.That(BetterSceneController.CanPlaceAsset(gameObject), Is.True);
                Assert.That(BetterSceneController.CanPlaceAsset(mesh), Is.True);
                Assert.That(BetterSceneController.CanPlaceAsset(material), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
                Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void VisibilityBands_RecognizeLightingAndAudioObjects()
        {
            var lightObject = new GameObject("Key Light", typeof(Light));
            var audioObject = new GameObject("Ambience", typeof(AudioSource));
            try
            {
                Assert.That(BetterSceneVisibility.Matches(lightObject, BetterSceneVisibilityBand.Lighting), Is.True);
                Assert.That(BetterSceneVisibility.Matches(lightObject, BetterSceneVisibilityBand.Audio), Is.False);
                Assert.That(BetterSceneVisibility.Matches(audioObject, BetterSceneVisibilityBand.Audio), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(lightObject);
                Object.DestroyImmediate(audioObject);
            }
        }

        [Test]
        public void ChangingAwayFromMeasure_ClearsTransientGuide()
        {
            try
            {
                BetterSceneController.SetMode(BetterSceneMode.Measure);
                BetterSceneController.BeginMeasurement(new Vector3(1f, 2f, 3f));
                Assert.That(BetterSceneController.Measurement.HasStart, Is.True);

                BetterSceneController.SetMode(BetterSceneMode.Select);

                Assert.That(BetterSceneController.Measurement.HasStart, Is.False);
                Assert.That(BetterSceneController.Measurement.HasEnd, Is.False);
                Assert.That(BetterSceneController.ActivePanel, Is.EqualTo(BetterScenePanel.Transform));
            }
            finally
            {
                BetterSceneController.ClearMeasurement();
                BetterSceneController.CollapsePanel();
            }
        }

        [Test]
        public void SwitchingMegaPanels_ChangesModeAndCleansSpatialState()
        {
            try
            {
                BetterSceneController.SetMode(BetterSceneMode.Measure);
                BetterSceneController.BeginMeasurement(Vector3.zero);

                BetterSceneController.TogglePanel(BetterScenePanel.Transform);

                Assert.That(BetterSceneController.Mode, Is.EqualTo(BetterSceneMode.Select));
                Assert.That(BetterSceneController.ActivePanel, Is.EqualTo(BetterScenePanel.Transform));
                Assert.That(BetterSceneController.Measurement.HasStart, Is.False);
            }
            finally
            {
                BetterSceneController.ClearMeasurement();
                BetterSceneController.CollapsePanel();
            }
        }

        [Test]
        public void MirrorSelection_UsesActiveObjectAsPivot()
        {
            var anchor = new GameObject("Anchor");
            var moving = new GameObject("Moving");
            anchor.transform.position = new Vector3(2f, 0f, 0f);
            moving.transform.position = new Vector3(5f, 0f, 0f);
            try
            {
                Selection.objects = new Object[] { anchor, moving };
                Selection.activeGameObject = anchor;

                BetterSceneOperations.MirrorSelection(BetterSceneAxis.X);

                Assert.That(moving.transform.position.x, Is.EqualTo(-1f).Within(0.0001f));
            }
            finally
            {
                Selection.objects = System.Array.Empty<Object>();
                Object.DestroyImmediate(anchor);
                Object.DestroyImmediate(moving);
                Undo.ClearAll();
            }
        }

        [Test]
        public void GroupSelection_IsOneUndoableOperation()
        {
            var first = new GameObject("First");
            var second = new GameObject("Second");
            try
            {
                Selection.objects = new Object[] { first, second };
                Selection.activeGameObject = first;

                GameObject group = BetterSceneOperations.GroupSelection();

                Assert.That(group, Is.Not.Null);
                Assert.That(first.transform.parent, Is.EqualTo(group.transform));
                Assert.That(second.transform.parent, Is.EqualTo(group.transform));

                Undo.PerformUndo();
                Assert.That(first.transform.parent, Is.Null);
                Assert.That(second.transform.parent, Is.Null);
                Assert.That(group == null, Is.True);
            }
            finally
            {
                Selection.objects = System.Array.Empty<Object>();
                if (first != null) Object.DestroyImmediate(first);
                if (second != null) Object.DestroyImmediate(second);
                Undo.ClearAll();
            }
        }

        [Test]
        public void ToolbarContents_CanBeReorderedHiddenAndReset()
        {
            try
            {
                BetterSceneSettings.ResetToolbarLayout();
                Assert.That(BetterSceneSettings.ToolbarOrder.First(), Is.EqualTo(BetterScenePanel.Create));

                BetterSceneSettings.MoveToolbarPanel(BetterScenePanel.Create, 1);
                Assert.That(BetterSceneSettings.ToolbarOrder.ElementAt(1), Is.EqualTo(BetterScenePanel.Create));

                BetterSceneSettings.SetToolbarPanelVisible(BetterScenePanel.Measure, false);
                Assert.That(BetterSceneSettings.IsToolbarPanelVisible(BetterScenePanel.Measure), Is.False);
            }
            finally
            {
                BetterSceneSettings.ResetToolbarLayout();
            }

            Assert.That(BetterSceneSettings.ToolbarOrder.First(), Is.EqualTo(BetterScenePanel.Create));
            Assert.That(BetterSceneSettings.IsToolbarPanelVisible(BetterScenePanel.Measure), Is.True);
            Assert.That(BetterSceneSettings.ToolbarHistoryVisible, Is.True);
            Assert.That(BetterSceneSettings.ToolbarQuickActionsVisible, Is.True);
        }

        [Test]
        public void MegaPanel_OpensBesideVerticalToolbarOnEitherSide()
        {
            Vector2 viewport = new Vector2(1400f, 800f);
            Vector2 panel = new Vector2(480f, 335f);

            Vector2 fromLeft = BetterSceneNativeOverlayUtility.CalculatePanelPosition(
                new Rect(40f, 180f, 38f, 300f), viewport, panel, true);
            Vector2 fromRight = BetterSceneNativeOverlayUtility.CalculatePanelPosition(
                new Rect(1320f, 180f, 38f, 300f), viewport, panel, true);

            Assert.That(fromLeft.x, Is.GreaterThan(78f));
            Assert.That(fromRight.x, Is.LessThan(1320f - panel.x));
            Assert.That(fromLeft.y, Is.EqualTo(180f).Within(0.001f));
            Assert.That(fromRight.y, Is.EqualTo(180f).Within(0.001f));
        }

        [Test]
        public void MegaPanel_OpensAboveOrBelowHorizontalToolbar()
        {
            Vector2 viewport = new Vector2(1400f, 800f);
            Vector2 panel = new Vector2(480f, 335f);

            Vector2 fromTop = BetterSceneNativeOverlayUtility.CalculatePanelPosition(
                new Rect(400f, 40f, 440f, 38f), viewport, panel, false);
            Vector2 fromBottom = BetterSceneNativeOverlayUtility.CalculatePanelPosition(
                new Rect(400f, 740f, 440f, 38f), viewport, panel, false);

            Assert.That(fromTop.y, Is.GreaterThan(78f));
            Assert.That(fromBottom.y, Is.LessThan(740f - panel.y));
            Assert.That(fromTop.x, Is.EqualTo(400f).Within(0.001f));
            Assert.That(fromBottom.x, Is.EqualTo(400f).Within(0.001f));
        }

        [Test]
        public void MegaPanel_UsesFloatingCanvasCoordinatesInsteadOfWindowCoordinates()
        {
            Rect toolbarWorldBounds = new Rect(1362f, 74f, 42f, 293f);
            Rect canvasWorldBounds = new Rect(1f, 24f, 1408f, 765f);

            Rect converted = BetterSceneNativeOverlayUtility.ConvertWorldBoundsToCanvas(
                toolbarWorldBounds,
                canvasWorldBounds);

            Assert.That(converted.position, Is.EqualTo(new Vector2(1361f, 50f)));
            Assert.That(converted.size, Is.EqualTo(toolbarWorldBounds.size));

            Vector2 panelPosition = BetterSceneNativeOverlayUtility.CalculatePanelPosition(
                converted,
                canvasWorldBounds.size,
                new Vector2(480f, 335f),
                true);
            Assert.That(panelPosition.x, Is.EqualTo(873f).Within(0.001f));
            Assert.That(panelPosition.y, Is.EqualTo(50f).Within(0.001f));
        }
    }
}
