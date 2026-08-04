using System;
using System.Collections.Generic;
using DansToolbox.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace DansToolbox.EditorTools.BetterScene
{
    internal static class BetterScenePlacementPreview
    {
        private sealed class RenderPart
        {
            internal Mesh Mesh;
            internal Matrix4x4 LocalMatrix;
            internal Texture Texture;
        }

        private static readonly List<RenderPart> parts = new List<RenderPart>();
        private static readonly Dictionary<Sprite, Mesh> spriteMeshes = new Dictionary<Sprite, Mesh>();
        private static UnityEngine.Object cachedAsset;
        private static string cachedAssetPath = string.Empty;
        private static Hash128 cachedDependencyHash;
        private static bool dependenciesMayHaveChanged;
        private static Material ghostMaterial;
        private static Material spriteMaterial;

        static BetterScenePlacementPreview()
        {
            EditorApplication.projectChanged += MarkDependenciesChanged;
            AssemblyReloadEvents.beforeAssemblyReload += Cleanup;
            EditorApplication.quitting += Cleanup;
        }

        internal static int GetRenderableCount(UnityEngine.Object asset)
        {
            EnsureAsset(asset);
            return parts.Count;
        }

        internal static bool Draw(
            UnityEngine.Object asset,
            SceneView sceneView,
            Vector3 point,
            Vector3 normal,
            bool groundToSurface)
        {
            if (Event.current.type != EventType.Repaint || sceneView == null || sceneView.camera == null)
            {
                return false;
            }

            EnsureAsset(asset);
            if (parts.Count == 0) return false;

            Matrix4x4 rootMatrix = CalculatePlacementMatrix(
                asset,
                point,
                normal,
                BetterSceneSettings.AlignToSurface,
                groundToSurface,
                out Bounds worldBounds);
            DansToolboxPalette palette = DansToolboxTheme.Current;
            Color fill = new Color(palette.Signal.r, palette.Signal.g, palette.Signal.b, 0.28f);
            if (GraphicsSettings.currentRenderPipeline == null)
            {
                EnsureMaterials();
                if (ghostMaterial == null) return false;
                ghostMaterial.SetColor("_Color", fill);
                if (spriteMaterial != null)
                {
                    spriteMaterial.SetColor("_Color", new Color(fill.r, fill.g, fill.b, 0.48f));
                }
                DrawBuiltInPipelinePreview(sceneView.camera, rootMatrix);
            }

            CompareFunction previousZ = Handles.zTest;
            Color previousColor = Handles.color;
            Handles.zTest = CompareFunction.LessEqual;
            Handles.color = new Color(palette.Signal.r, palette.Signal.g, palette.Signal.b, 0.9f);
            Handles.DrawWireCube(worldBounds.center, worldBounds.size);
            Handles.color = previousColor;
            Handles.zTest = previousZ;
            return true;
        }

        internal static bool TryCalculateWorldBounds(
            UnityEngine.Object asset,
            Vector3 point,
            Vector3 normal,
            bool alignToSurface,
            bool groundToSurface,
            out Bounds worldBounds)
        {
            EnsureAsset(asset);
            if (parts.Count == 0)
            {
                worldBounds = new Bounds(point, Vector3.zero);
                return false;
            }
            CalculatePlacementMatrix(asset, point, normal, alignToSurface, groundToSurface, out worldBounds);
            return true;
        }

        internal static void Invalidate()
        {
            foreach (Mesh mesh in spriteMeshes.Values)
            {
                if (mesh != null) UnityEngine.Object.DestroyImmediate(mesh);
            }
            spriteMeshes.Clear();
            parts.Clear();
            cachedAsset = null;
            cachedAssetPath = string.Empty;
            cachedDependencyHash = default;
            dependenciesMayHaveChanged = false;
        }

        private static void DrawBuiltInPipelinePreview(Camera camera, Matrix4x4 rootMatrix)
        {
            foreach (RenderPart part in parts)
            {
                if (part.Mesh == null) continue;
                Matrix4x4 matrix = rootMatrix * part.LocalMatrix;
                Material material = part.Texture != null && spriteMaterial != null
                    ? spriteMaterial
                    : ghostMaterial;
                if (part.Texture != null) material.mainTexture = part.Texture;
                int subMeshCount = Mathf.Max(1, part.Mesh.subMeshCount);
                for (int subMesh = 0; subMesh < subMeshCount; subMesh++)
                {
                    Graphics.DrawMesh(
                        part.Mesh,
                        matrix,
                        material,
                        0,
                        camera,
                        subMesh,
                        null,
                        ShadowCastingMode.Off,
                        false,
                        null,
                        LightProbeUsage.Off,
                        null);
                }
            }
        }

        private static Matrix4x4 CalculatePlacementMatrix(
            UnityEngine.Object asset,
            Vector3 point,
            Vector3 normal,
            bool alignToSurface,
            bool groundToSurface,
            out Bounds worldBounds)
        {
            Quaternion rotation = Quaternion.identity;
            Vector3 scale = Vector3.one;
            if (asset is GameObject gameObject)
            {
                rotation = gameObject.transform.localRotation;
                scale = gameObject.transform.localScale;
            }
            if (alignToSurface && normal.sqrMagnitude > 0.001f && !(asset is Sprite))
            {
                rotation = Quaternion.FromToRotation(Vector3.up, normal.normalized);
            }

            Matrix4x4 rootMatrix = Matrix4x4.TRS(point, rotation, scale);
            worldBounds = CalculateBounds(rootMatrix, normal, out float minimumProjection);
            if (groundToSurface)
            {
                point += BetterSceneOperations.CalculateSurfaceContactOffset(
                    minimumProjection,
                    point,
                    normal);
                rootMatrix = Matrix4x4.TRS(point, rotation, scale);
                worldBounds = CalculateBounds(rootMatrix, normal, out _);
            }
            return rootMatrix;
        }

        private static Bounds CalculateBounds(
            Matrix4x4 rootMatrix,
            Vector3 surfaceNormal,
            out float minimumProjection)
        {
            bool found = false;
            Bounds result = default;
            Vector3 normal = surfaceNormal.sqrMagnitude > 0.001f
                ? surfaceNormal.normalized
                : Vector3.up;
            minimumProjection = float.PositiveInfinity;
            foreach (RenderPart part in parts)
            {
                if (part.Mesh == null) continue;
                Matrix4x4 matrix = rootMatrix * part.LocalMatrix;
                Bounds meshBounds = part.Mesh.bounds;
                Vector3 min = meshBounds.min;
                Vector3 max = meshBounds.max;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 local = new Vector3(
                        (corner & 1) == 0 ? min.x : max.x,
                        (corner & 2) == 0 ? min.y : max.y,
                        (corner & 4) == 0 ? min.z : max.z);
                    Vector3 world = matrix.MultiplyPoint3x4(local);
                    if (!found)
                    {
                        result = new Bounds(world, Vector3.zero);
                        found = true;
                    }
                    else result.Encapsulate(world);
                    minimumProjection = Mathf.Min(minimumProjection, Vector3.Dot(world, normal));
                }
            }
            if (found) return result;
            Vector3 position = rootMatrix.GetColumn(3);
            minimumProjection = Vector3.Dot(position, normal);
            return new Bounds(position, Vector3.zero);
        }

        private static void EnsureAsset(UnityEngine.Object asset)
        {
            if (asset == cachedAsset)
            {
                if (!dependenciesMayHaveChanged || string.IsNullOrEmpty(cachedAssetPath)) return;
                dependenciesMayHaveChanged = false;
                Hash128 currentHash = AssetDatabase.GetAssetDependencyHash(cachedAssetPath);
                if (currentHash == cachedDependencyHash) return;
            }
            Invalidate();
            cachedAsset = asset;
            cachedAssetPath = asset == null ? string.Empty : AssetDatabase.GetAssetPath(asset);
            cachedDependencyHash = string.IsNullOrEmpty(cachedAssetPath)
                ? default
                : AssetDatabase.GetAssetDependencyHash(cachedAssetPath);
            if (asset is Mesh mesh)
            {
                AddPart(mesh, Matrix4x4.identity, null);
                return;
            }
            if (asset is Sprite sprite)
            {
                AddSpritePart(sprite, Matrix4x4.identity, false, false);
                return;
            }
            if (!(asset is GameObject root)) return;

            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                MeshRenderer renderer = filter == null ? null : filter.GetComponent<MeshRenderer>();
                if (filter == null || filter.sharedMesh == null || renderer == null || !renderer.enabled ||
                    !IsActiveRelativeTo(root.transform, filter.transform))
                {
                    continue;
                }
                AddPart(filter.sharedMesh, RelativeMatrix(root.transform, filter.transform), null);
            }
            foreach (SkinnedMeshRenderer renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (renderer == null || renderer.sharedMesh == null || !renderer.enabled ||
                    !IsActiveRelativeTo(root.transform, renderer.transform))
                {
                    continue;
                }
                AddPart(renderer.sharedMesh, RelativeMatrix(root.transform, renderer.transform), null);
            }
            foreach (SpriteRenderer renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer == null || renderer.sprite == null || !renderer.enabled ||
                    !IsActiveRelativeTo(root.transform, renderer.transform))
                {
                    continue;
                }
                AddSpritePart(
                    renderer.sprite,
                    RelativeMatrix(root.transform, renderer.transform),
                    renderer.flipX,
                    renderer.flipY);
            }
        }

        private static void AddPart(Mesh mesh, Matrix4x4 localMatrix, Texture texture)
        {
            if (mesh == null) return;
            parts.Add(new RenderPart
            {
                Mesh = mesh,
                LocalMatrix = localMatrix,
                Texture = texture
            });
        }

        private static void AddSpritePart(
            Sprite sprite,
            Matrix4x4 localMatrix,
            bool flipX,
            bool flipY)
        {
            Mesh mesh = GetSpriteMesh(sprite);
            if (mesh == null) return;
            Matrix4x4 flip = Matrix4x4.Scale(new Vector3(flipX ? -1f : 1f, flipY ? -1f : 1f, 1f));
            AddPart(mesh, localMatrix * flip, sprite.texture);
        }

        private static Mesh GetSpriteMesh(Sprite sprite)
        {
            if (sprite == null) return null;
            if (spriteMeshes.TryGetValue(sprite, out Mesh cached)) return cached;
            Vector2[] sourceVertices = sprite.vertices;
            ushort[] sourceTriangles = sprite.triangles;
            if (sourceVertices == null || sourceVertices.Length == 0 ||
                sourceTriangles == null || sourceTriangles.Length == 0)
            {
                spriteMeshes[sprite] = null;
                return null;
            }

            var vertices = new Vector3[sourceVertices.Length];
            for (int index = 0; index < sourceVertices.Length; index++) vertices[index] = sourceVertices[index];
            var triangles = new int[sourceTriangles.Length];
            for (int index = 0; index < sourceTriangles.Length; index++) triangles[index] = sourceTriangles[index];
            var mesh = new Mesh
            {
                name = sprite.name + " Placement Ghost",
                hideFlags = HideFlags.HideAndDontSave,
                vertices = vertices,
                uv = sprite.uv,
                triangles = triangles
            };
            mesh.RecalculateBounds();
            spriteMeshes[sprite] = mesh;
            return mesh;
        }

        private static Matrix4x4 RelativeMatrix(Transform root, Transform child)
        {
            Matrix4x4 matrix = Matrix4x4.identity;
            Transform current = child;
            while (current != null && current != root)
            {
                matrix = Matrix4x4.TRS(current.localPosition, current.localRotation, current.localScale) * matrix;
                current = current.parent;
            }
            return matrix;
        }

        private static bool IsActiveRelativeTo(Transform root, Transform child)
        {
            Transform current = child;
            while (current != null)
            {
                if (!current.gameObject.activeSelf) return false;
                if (current == root) return true;
                current = current.parent;
            }
            return false;
        }

        private static void EnsureMaterials()
        {
            if (ghostMaterial == null)
            {
                Shader shader = Shader.Find("Hidden/Internal-Colored") ?? Shader.Find("Unlit/Color");
                if (shader != null)
                {
                    ghostMaterial = CreateMaterial(shader);
                }
            }
            if (spriteMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader != null) spriteMaterial = CreateMaterial(shader);
            }
        }

        private static void MarkDependenciesChanged()
        {
            dependenciesMayHaveChanged = true;
        }

        private static Material CreateMaterial(Shader shader)
        {
            var material = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave,
                renderQueue = (int)RenderQueue.Transparent
            };
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_Cull", (int)CullMode.Off);
            material.SetInt("_ZWrite", 0);
            material.SetInt("_ZTest", (int)CompareFunction.LessEqual);
            return material;
        }

        private static void Cleanup()
        {
            Invalidate();
            if (ghostMaterial != null) UnityEngine.Object.DestroyImmediate(ghostMaterial);
            if (spriteMaterial != null) UnityEngine.Object.DestroyImmediate(spriteMaterial);
            ghostMaterial = null;
            spriteMaterial = null;
        }
    }
}
