using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DansToolbox.EditorTools.BetterHierarchy
{
    internal sealed class BetterHierarchyPreviewCache
    {
        private readonly Dictionary<int, Texture2D> previews = new Dictionary<int, Texture2D>();

        internal Texture2D Get(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return null;
            }

            int id = gameObject.GetInstanceID();
            if (previews.TryGetValue(id, out Texture2D cached) && cached != null)
            {
                return cached;
            }

            Component representative = GetRepresentativeComponent(gameObject);
            if (representative != null)
            {
                Texture2D componentIcon = EditorGUIUtility.ObjectContent(
                    null,
                    representative.GetType()).image as Texture2D;
                if (componentIcon != null)
                {
                    previews[id] = componentIcon;
                    return componentIcon;
                }
            }

            UnityEngine.Object previewTarget = GetPreviewTarget(gameObject);
            Texture2D preview = AssetPreview.GetAssetPreview(previewTarget);
            if (preview == null)
            {
                SpriteRenderer spriteRenderer = gameObject.GetComponent<SpriteRenderer>();
                if (spriteRenderer != null && spriteRenderer.sprite != null)
                {
                    preview = AssetPreview.GetAssetPreview(spriteRenderer.sprite);
                }
            }

            if (preview == null)
            {
                preview = AssetPreview.GetMiniThumbnail(previewTarget);
            }

            if (preview != null)
            {
                previews[id] = preview;
            }

            return preview;
        }

        internal Texture2D Get(UnityEngine.Object asset)
        {
            if (asset == null)
            {
                return null;
            }

            int id = asset.GetInstanceID();
            if (previews.TryGetValue(id, out Texture2D cached) && cached != null)
            {
                return cached;
            }

            Texture2D preview = AssetPreview.GetAssetPreview(asset) ?? AssetPreview.GetMiniThumbnail(asset);
            if (preview != null)
            {
                previews[id] = preview;
            }

            return preview;
        }

        internal void Clear()
        {
            previews.Clear();
        }

        private static UnityEngine.Object GetPreviewTarget(GameObject gameObject)
        {
            GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(gameObject);
            if (source != null)
            {
                return source;
            }

            MeshFilter meshFilter = gameObject.GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                return meshFilter.sharedMesh;
            }

            SkinnedMeshRenderer skinned = gameObject.GetComponent<SkinnedMeshRenderer>();
            if (skinned != null && skinned.sharedMesh != null)
            {
                return skinned.sharedMesh;
            }

            return gameObject;
        }

        internal static System.Type GetRepresentativeComponentType(GameObject gameObject)
        {
            return GetRepresentativeComponent(gameObject)?.GetType();
        }

        private static Component GetRepresentativeComponent(GameObject gameObject)
        {
            if (gameObject == null || HasRenderableGeometry(gameObject))
            {
                return null;
            }

            return gameObject.GetComponents<Component>()
                .FirstOrDefault(component => component != null && !(component is Transform));
        }

        private static bool HasRenderableGeometry(GameObject gameObject)
        {
            return gameObject.GetComponentsInChildren<MeshFilter>(true)
                       .Any(filter => filter.sharedMesh != null) ||
                   gameObject.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                       .Any(renderer => renderer.sharedMesh != null) ||
                   gameObject.GetComponentsInChildren<SpriteRenderer>(true)
                       .Any(renderer => renderer.sprite != null);
        }
    }
}
