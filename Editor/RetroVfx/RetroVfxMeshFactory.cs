using System.Collections.Generic;
using DansToolbox.RetroVfx;
using UnityEngine;

namespace DansToolbox.EditorTools.RetroVfx
{
    internal static class RetroVfxMeshFactory
    {
        internal static Mesh Create(RetroVfxLayer layer, int seed)
        {
            if (layer.sourceMesh != null)
            {
                return layer.sourceMesh;
            }

            return layer.kind switch
            {
                RetroVfxLayerKind.Arc => CreateArc(layer, seed),
                RetroVfxLayerKind.Ring => CreateRing(layer, seed),
                RetroVfxLayerKind.Beam => CreateRibbon(layer, seed),
                RetroVfxLayerKind.Trail => CreateRibbon(layer, seed),
                _ => CreateQuad()
            };
        }

        private static Mesh CreateArc(RetroVfxLayer layer, int seed)
        {
            int segments = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(8f, 28f, layer.spread / 360f)), 8, 32);
            float arc = Mathf.Clamp(layer.spread, 24f, 330f) * Mathf.Deg2Rad;
            float radius = 0.72f;
            float width = Mathf.Clamp(0.08f + layer.aspect.y * 0.1f, 0.06f, 0.3f);
            float start = -arc * 0.5f;
            List<Vector3> vertices = new List<Vector3>((segments + 1) * 2);
            List<Vector2> uv = new List<Vector2>((segments + 1) * 2);
            List<int> triangles = new List<int>(segments * 6);
            for (int index = 0; index <= segments; index++)
            {
                float t = index / (float)segments;
                float angle = start + arc * t;
                float wobble = (Hash(index, seed) - 0.5f) * 0.025f;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                vertices.Add(direction * (radius - width + wobble));
                vertices.Add(direction * (radius + width + wobble));
                uv.Add(new Vector2(t, 0f));
                uv.Add(new Vector2(t, 1f));
                if (index == segments)
                {
                    continue;
                }
                int root = index * 2;
                triangles.Add(root);
                triangles.Add(root + 3);
                triangles.Add(root + 1);
                triangles.Add(root);
                triangles.Add(root + 2);
                triangles.Add(root + 3);
            }
            return Build("Retro VFX Arc", vertices, uv, triangles);
        }

        private static Mesh CreateRing(RetroVfxLayer layer, int seed)
        {
            RetroVfxLayer copy = layer.Clone();
            copy.spread = 360f;
            return CreateArc(copy, seed);
        }

        private static Mesh CreateRibbon(RetroVfxLayer layer, int seed)
        {
            int segments = 10;
            float halfWidth = Mathf.Clamp(0.06f * layer.aspect.y, 0.025f, 0.24f);
            List<Vector3> vertices = new List<Vector3>((segments + 1) * 2);
            List<Vector2> uv = new List<Vector2>((segments + 1) * 2);
            List<int> triangles = new List<int>(segments * 6);
            for (int index = 0; index <= segments; index++)
            {
                float t = index / (float)segments;
                float x = Mathf.Lerp(-0.5f, 0.5f, t);
                float y = (Hash(index, seed) - 0.5f) * 0.08f * Mathf.Sin(t * Mathf.PI);
                float width = halfWidth * Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
                vertices.Add(new Vector3(x, y - width, 0f));
                vertices.Add(new Vector3(x, y + width, 0f));
                uv.Add(new Vector2(t, 0f));
                uv.Add(new Vector2(t, 1f));
                if (index == segments)
                {
                    continue;
                }
                int root = index * 2;
                triangles.Add(root);
                triangles.Add(root + 3);
                triangles.Add(root + 1);
                triangles.Add(root);
                triangles.Add(root + 2);
                triangles.Add(root + 3);
            }
            return Build("Retro VFX Ribbon", vertices, uv, triangles);
        }

        private static Mesh CreateQuad()
        {
            Mesh mesh = new Mesh
            {
                name = "Retro VFX Quad",
                hideFlags = HideFlags.HideAndDontSave,
                vertices = new[]
                {
                    new Vector3(-0.5f, -0.5f), new Vector3(0.5f, -0.5f),
                    new Vector3(-0.5f, 0.5f), new Vector3(0.5f, 0.5f)
                },
                uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one },
                triangles = new[] { 0, 2, 1, 1, 2, 3 }
            };
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh Build(string name, List<Vector3> vertices, List<Vector2> uv, List<int> triangles)
        {
            Mesh mesh = new Mesh
            {
                name = name,
                hideFlags = HideFlags.HideAndDontSave
            };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            return mesh;
        }

        private static float Hash(int index, int seed)
        {
            unchecked
            {
                uint value = (uint)(index * 374761393 + seed * 668265263);
                value = (value ^ (value >> 13)) * 1274126177u;
                return (value & 0x00ffffff) / 16777215f;
            }
        }
    }
}
