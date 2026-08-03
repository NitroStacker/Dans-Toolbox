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
        public void ShiftSnap_QuantizesFloorTangentsAndKeepsSurfaceHeight()
        {
            Vector3 point = new Vector3(1.24f, 2.3f, 3.76f);

            Vector3 result = BetterSceneOperations.SnapPointToSurface(
                point,
                Vector3.up,
                new Vector3(1f, 0.5f, 1f));

            Assert.That(result.x, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(result.y, Is.EqualTo(2.3f).Within(0.0001f));
            Assert.That(result.z, Is.EqualTo(4f).Within(0.0001f));
        }

        [Test]
        public void ShiftSnap_QuantizesWallTangentsAndKeepsSurfaceDepth()
        {
            Vector3 point = new Vector3(2.3f, 1.24f, 3.76f);

            Vector3 result = BetterSceneOperations.SnapPointToSurface(
                point,
                Vector3.right,
                Vector3.one);

            Assert.That(result.x, Is.EqualTo(2.3f).Within(0.0001f));
            Assert.That(result.y, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(result.z, Is.EqualTo(4f).Within(0.0001f));
        }

        [Test]
        public void ShiftSnap_RemainsOnSlopedSurfacePlane()
        {
            Vector3 point = new Vector3(1.24f, 2.61f, 3.76f);
            Vector3 normal = new Vector3(1f, 1f, 0f).normalized;

            Vector3 result = BetterSceneOperations.SnapPointToSurface(
                point,
                normal,
                Vector3.one);

            Assert.That(
                Vector3.Dot(result - point, normal),
                Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void SmartSnap_SameSizedTargetUsesExactBoundsCenter()
        {
            Vector3 point = new Vector3(0.31f, 1f, -0.22f);
            Bounds target = new Bounds(Vector3.zero, new Vector3(1f, 0.2f, 1f));
            Bounds placed = new Bounds(point + new Vector3(0.08f, 0.5f, -0.04f), Vector3.one);

            Vector3 result = BetterSceneOperations.SnapPlacementPointToTarget(
                point, Vector3.up, target, placed, Vector3.one);
            Vector3 movedCenter = placed.center + result - point;

            Assert.That(movedCenter.x, Is.EqualTo(target.center.x).Within(0.0001f));
            Assert.That(movedCenter.z, Is.EqualTo(target.center.z).Within(0.0001f));
        }

        [Test]
        public void SmartSnap_LargeTargetUsesCenteredFootprintIntervals()
        {
            Vector3 point = new Vector3(1.38f, 1f, -0.72f);
            Bounds target = new Bounds(Vector3.zero, new Vector3(4f, 0.2f, 4f));
            Bounds placed = new Bounds(point + Vector3.up * 0.5f, Vector3.one);

            Vector3 result = BetterSceneOperations.SnapPlacementPointToTarget(
                point, Vector3.up, target, placed, Vector3.one);
            Vector3 movedCenter = placed.center + result - point;

            Assert.That(movedCenter.x, Is.EqualTo(1.5f).Within(0.0001f));
            Assert.That(movedCenter.z, Is.EqualTo(-0.5f).Within(0.0001f));
        }

        [Test]
        public void SmartSnap_NormalizesNearIntegralMeshFootprints()
        {
            Vector3 point = new Vector3(0.18f, 1f, 0.12f);
            Bounds target = new Bounds(Vector3.zero, new Vector3(10f, 0.2f, 10f));
            Bounds placed = new Bounds(point + Vector3.up * 0.5f, new Vector3(1.0004f, 1f, 1.0004f));

            Vector3 result = BetterSceneOperations.SnapPlacementPointToTarget(
                point, Vector3.up, target, placed, Vector3.one);
            Vector3 movedCenter = placed.center + result - point;

            Assert.That(movedCenter.x, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(movedCenter.z, Is.EqualTo(0.5f).Within(0.0001f));
        }

        [Test]
        public void SmartSnap_FallbackUsesVisibleCenterInsteadOfRootPivot()
        {
            Vector3 point = new Vector3(1.24f, 2.3f, 3.76f);
            Bounds placed = new Bounds(point + new Vector3(0.08f, 0.5f, -0.04f), Vector3.one);

            Vector3 result = BetterSceneOperations.SnapPlacementPointToGridAnchor(
                point, Vector3.up, placed, Vector3.one);
            Vector3 movedCenter = placed.center + result - point;

            Assert.That(movedCenter.x, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(movedCenter.z, Is.EqualTo(4f).Within(0.0001f));
            Assert.That(result.y, Is.EqualTo(point.y).Within(0.0001f));
        }

        [Test]
        public void SmartSnap_RemainsOnSlopedSurfacePlane()
        {
            Vector3 point = new Vector3(1.24f, 2.61f, 3.76f);
            Vector3 normal = new Vector3(1f, 1f, 0f).normalized;
            Bounds target = new Bounds(Vector3.zero, Vector3.one * 6f);
            Bounds placed = new Bounds(point + Vector3.up * 0.5f, Vector3.one);

            Vector3 result = BetterSceneOperations.SnapPlacementPointToTarget(
                point, normal, target, placed, Vector3.one);

            Assert.That(Vector3.Dot(result - point, normal), Is.EqualTo(0f).Within(0.0001f));
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
        public void DirectionalViewZoom_TogglePreservesOrResetsFraming()
        {
            Assert.That(
                BetterSceneOperations.ResolveDirectionalViewZoom(true, 2.75f),
                Is.EqualTo(2.75f).Within(0.0001f));
            Assert.That(
                BetterSceneOperations.ResolveDirectionalViewZoom(false, 2.75f),
                Is.EqualTo(BetterSceneOperations.DefaultViewZoom).Within(0.0001f));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SavedView_StoresExactZoomState(bool orthographic)
        {
            BetterSceneBookmark bookmark = BetterSceneSettings.AddBookmark(
                "ZOOM TEST",
                string.Empty,
                new Vector3(1f, 2f, 3f),
                Quaternion.Euler(10f, 20f, 0f),
                2.875f,
                orthographic,
                false);
            try
            {
                Assert.That(bookmark.Size, Is.EqualTo(2.875f).Within(0.0001f));
                Assert.That(bookmark.Orthographic, Is.EqualTo(orthographic));
            }
            finally
            {
                BetterSceneSettings.RemoveBookmark(bookmark.Id);
            }
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
        public void Placement_PreservesExactBuiltInMeshReference()
        {
            UnityEngine.Object previous = BetterSceneSettings.PlacementAsset;
            GameObject primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Mesh mesh = primitive.GetComponent<MeshFilter>().sharedMesh;
            try
            {
                Assert.That(EditorUtility.IsPersistent(mesh), Is.True);

                BetterSceneSettings.PlacementAsset = mesh;

                Assert.That(BetterSceneSettings.PlacementAsset, Is.SameAs(mesh));
                Assert.That(BetterSceneSettings.GetRecentPlacementAssets().First(), Is.SameAs(mesh));
            }
            finally
            {
                BetterSceneSettings.PlacementAsset = previous;
                Object.DestroyImmediate(primitive);
            }
        }

        [Test]
        public void PlacementGhost_UsesRenderableMeshWithoutCreatingPreviewObjects()
        {
            var mesh = new Mesh
            {
                vertices = new[]
                {
                    new Vector3(-1f, -1f, -1f), new Vector3(1f, -1f, -1f),
                    new Vector3(-1f, 1f, -1f), new Vector3(1f, 1f, -1f),
                    new Vector3(-1f, -1f, 1f), new Vector3(1f, -1f, 1f),
                    new Vector3(-1f, 1f, 1f), new Vector3(1f, 1f, 1f)
                },
                triangles = new[] { 0, 2, 1, 1, 2, 3 }
            };
            mesh.RecalculateBounds();
            var source = new GameObject("Ghost Source", typeof(MeshFilter), typeof(MeshRenderer));
            source.GetComponent<MeshFilter>().sharedMesh = mesh;
            source.transform.localScale = Vector3.one * 2f;
            int rootsBeforePreview = source.scene.rootCount;
            try
            {
                Assert.That(BetterScenePlacementPreview.GetRenderableCount(source), Is.EqualTo(1));
                Assert.That(source.scene.rootCount, Is.EqualTo(rootsBeforePreview));
                Assert.That(BetterScenePlacementPreview.TryCalculateWorldBounds(
                    source,
                    new Vector3(0f, 5f, 0f),
                    Vector3.up,
                    true,
                    true,
                    out Bounds bounds), Is.True);
                Assert.That(bounds.min.y, Is.EqualTo(5f).Within(0.0001f));
                Assert.That(bounds.size.y, Is.EqualTo(4f).Within(0.0001f));
            }
            finally
            {
                BetterScenePlacementPreview.Invalidate();
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(mesh);
            }
        }

        [TestCase(BetterSceneSnapMode.Surface, 10f)]
        [TestCase(BetterSceneSnapMode.Free, 13f)]
        public void Placement_UsesBoundsContactOnlyInSurfaceMode(
            BetterSceneSnapMode snapMode,
            float expectedMinimumY)
        {
            var mesh = new Mesh
            {
                vertices = new[]
                {
                    new Vector3(-0.5f, 3f, -0.5f), new Vector3(0.5f, 3f, -0.5f),
                    new Vector3(-0.5f, 4f, -0.5f), new Vector3(0.5f, 4f, -0.5f),
                    new Vector3(-0.5f, 3f, 0.5f), new Vector3(0.5f, 3f, 0.5f),
                    new Vector3(-0.5f, 4f, 0.5f), new Vector3(0.5f, 4f, 0.5f)
                },
                triangles = new[] { 0, 2, 1, 1, 2, 3 }
            };
            mesh.RecalculateBounds();
            var placed = new GameObject("Offset Mesh", typeof(MeshFilter), typeof(MeshRenderer));
            placed.GetComponent<MeshFilter>().sharedMesh = mesh;
            placed.transform.position = new Vector3(0f, 10f, 0f);
            try
            {
                bool usesContact = BetterSceneOperations.TryCalculatePlacementContactOffset(
                    snapMode,
                    placed,
                    new Vector3(0f, 10f, 0f),
                    Vector3.up,
                    out Vector3 contactOffset);
                placed.transform.position += contactOffset;

                Assert.That(usesContact, Is.EqualTo(snapMode == BetterSceneSnapMode.Surface));
                Assert.That(BetterSceneOperations.TryGetBounds(placed, out Bounds bounds), Is.True);
                Assert.That(bounds.min.y, Is.EqualTo(expectedMinimumY).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(placed);
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void SurfaceContactOffset_UsesExactNormalOnSlopedSurfaces()
        {
            Vector3 normal = new Vector3(1f, 1f, 0f).normalized;
            Bounds localBounds = new Bounds(new Vector3(0f, 3.5f, 0f), Vector3.one);
            Matrix4x4 matrix = Matrix4x4.TRS(
                new Vector3(2f, 4f, -1f),
                Quaternion.FromToRotation(Vector3.up, normal),
                Vector3.one);
            Vector3 surfacePoint = new Vector3(5f, -2f, 3f);
            float minimumProjection = BetterSceneOperations.CalculateMinimumProjection(localBounds, matrix, normal);

            Vector3 offset = BetterSceneOperations.CalculateSurfaceContactOffset(
                minimumProjection,
                surfacePoint,
                normal);
            float movedMinimum = minimumProjection + Vector3.Dot(offset, normal);

            Assert.That(movedMinimum, Is.EqualTo(Vector3.Dot(surfacePoint, normal)).Within(0.0001f));
            Assert.That(Vector3.Cross(offset, normal).sqrMagnitude, Is.LessThan(0.0001f));
        }

        [Test]
        public void EraseTarget_MatchesExactMeshSpriteAndAudioReferences()
        {
            var mesh = new Mesh();
            var otherMesh = new Mesh();
            var texture = new Texture2D(4, 4);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), Vector2.one * 0.5f);
            AudioClip clip = AudioClip.Create("Erase Clip", 32, 1, 8000, false);
            var meshObject = new GameObject("Mesh Target", typeof(MeshFilter), typeof(MeshRenderer));
            var spriteObject = new GameObject("Sprite Target", typeof(SpriteRenderer));
            var audioObject = new GameObject("Audio Target", typeof(AudioSource));
            meshObject.GetComponent<MeshFilter>().sharedMesh = mesh;
            spriteObject.GetComponent<SpriteRenderer>().sprite = sprite;
            audioObject.GetComponent<AudioSource>().clip = clip;
            try
            {
                Assert.That(BetterSceneOperations.FindPlacementAssetTarget(meshObject, mesh), Is.SameAs(meshObject));
                Assert.That(BetterSceneOperations.FindPlacementAssetTarget(meshObject, otherMesh), Is.Null);
                Assert.That(BetterSceneOperations.FindPlacementAssetTarget(spriteObject, sprite), Is.SameAs(spriteObject));
                Assert.That(BetterSceneOperations.FindPlacementAssetTarget(audioObject, clip), Is.SameAs(audioObject));
                Assert.That(BetterSceneOperations.FindPlacementAssetTarget(audioObject, sprite), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(meshObject);
                Object.DestroyImmediate(spriteObject);
                Object.DestroyImmediate(audioObject);
                Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(otherMesh);
                Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(clip);
            }
        }

        [Test]
        public void EraseTarget_IsUndoableAndIgnoresNonMatchingObject()
        {
            var targetMesh = new Mesh();
            var otherMesh = new Mesh();
            var target = new GameObject("Erase Me", typeof(MeshFilter), typeof(MeshRenderer));
            var other = new GameObject("Keep Me", typeof(MeshFilter), typeof(MeshRenderer));
            target.GetComponent<MeshFilter>().sharedMesh = targetMesh;
            other.GetComponent<MeshFilter>().sharedMesh = otherMesh;
            try
            {
                Assert.That(BetterSceneOperations.ErasePlacementAssetTarget(other, targetMesh), Is.False);
                Assert.That(other, Is.Not.Null);

                Assert.That(BetterSceneOperations.ErasePlacementAssetTarget(target, targetMesh), Is.True);
                Assert.That(target == null, Is.True);

                Undo.PerformUndo();
                Assert.That(target == null, Is.False);
            }
            finally
            {
                if (target != null) Object.DestroyImmediate(target);
                if (other != null) Object.DestroyImmediate(other);
                Object.DestroyImmediate(targetMesh);
                Object.DestroyImmediate(otherMesh);
                Undo.ClearAll();
            }
        }

        [Test]
        public void EraseStroke_RemovesMultipleMatchesAsOneUndoOperation()
        {
            var targetMesh = new Mesh { name = "Stroke Target" };
            var first = new GameObject("First", typeof(MeshFilter), typeof(MeshRenderer));
            var second = new GameObject("Second", typeof(MeshFilter), typeof(MeshRenderer));
            first.GetComponent<MeshFilter>().sharedMesh = targetMesh;
            second.GetComponent<MeshFilter>().sharedMesh = targetMesh;
            try
            {
                int group = BetterSceneOperations.BeginEraseUndoGroup(targetMesh);
                Assert.That(BetterSceneOperations.ErasePlacementAssetTarget(first, targetMesh), Is.True);
                Assert.That(BetterSceneOperations.ErasePlacementAssetTarget(second, targetMesh), Is.True);
                BetterSceneOperations.EndEraseUndoGroup(group);

                Assert.That(first == null, Is.True);
                Assert.That(second == null, Is.True);

                Undo.PerformUndo();
                Assert.That(first == null, Is.False);
                Assert.That(second == null, Is.False);
            }
            finally
            {
                if (first != null) Object.DestroyImmediate(first);
                if (second != null) Object.DestroyImmediate(second);
                Object.DestroyImmediate(targetMesh);
                Undo.ClearAll();
            }
        }

        [Test]
        public void PlacementGhost_BuildsRenderableSpriteImage()
        {
            var texture = new Texture2D(4, 4);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);
            try
            {
                Assert.That(BetterScenePlacementPreview.GetRenderableCount(sprite), Is.EqualTo(1));
            }
            finally
            {
                BetterScenePlacementPreview.Invalidate();
                Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(texture);
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

        [Test]
        public void MegaPanel_SavedViewsHeightTracksExpandedRowCount()
        {
            float collapsed = BetterSceneOverlay.CalculateDesiredHeight(
                BetterScenePanel.View, false, false, 8, false, 0, false);
            float twoViews = BetterSceneOverlay.CalculateDesiredHeight(
                BetterScenePanel.View, false, true, 2, false, 0, false);
            float eightViews = BetterSceneOverlay.CalculateDesiredHeight(
                BetterScenePanel.View, false, true, 8, false, 0, false);

            Assert.That(collapsed, Is.EqualTo(400f));
            Assert.That(twoViews, Is.EqualTo(476f));
            Assert.That(eightViews, Is.EqualTo(620f));
        }

        [Test]
        public void MegaPanel_ExpandedListsCapAtVisibleRows()
        {
            float eightPresets = BetterSceneOverlay.CalculateDesiredHeight(
                BetterScenePanel.Visibility, false, false, 0, true, 8, false);
            float manyPresets = BetterSceneOverlay.CalculateDesiredHeight(
                BetterScenePanel.Visibility, false, false, 0, true, 40, false);

            Assert.That(eightPresets, Is.EqualTo(575f));
            Assert.That(manyPresets, Is.EqualTo(eightPresets));
        }

        [Test]
        public void MegaPanel_ResponsiveSizeUsesScrollFallbackForShortViewport()
        {
            Vector2 size = BetterSceneNativeOverlayUtility.CalculateResponsivePanelSize(
                480f,
                620f,
                new Vector2(1000f, 400f));

            Assert.That(size.x, Is.EqualTo(480f));
            Assert.That(size.y, Is.EqualTo(384f));
        }

        [Test]
        public void MegaPanel_ResponsiveSizeFitsNarrowViewport()
        {
            Vector2 size = BetterSceneNativeOverlayUtility.CalculateResponsivePanelSize(
                480f,
                400f,
                new Vector2(300f, 800f));

            Assert.That(size.x, Is.EqualTo(284f));
            Assert.That(size.y, Is.EqualTo(400f));
        }

        [Test]
        public void MegaPanel_ExpansionReclampsPanelInsideViewport()
        {
            Vector2 position = BetterSceneNativeOverlayUtility.ClampPanelPosition(
                new Vector2(700f, 500f),
                new Vector2(1000f, 600f),
                new Vector2(480f, 500f));

            Assert.That(position.x, Is.EqualTo(512f));
            Assert.That(position.y, Is.EqualTo(92f));
        }
    }
}
