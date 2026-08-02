using NUnit.Framework;
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
    }
}
