using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DansToolbox.EditorTools.BetterHierarchy
{
    internal sealed class BetterHierarchyPreviewCache
    {
        private readonly Dictionary<int, Texture2D> previews = new Dictionary<int, Texture2D>();
        private readonly Dictionary<int, System.Type> representativeTypes = new Dictionary<int, System.Type>();
        private readonly HashSet<int> assetPreviewIds = new HashSet<int>();

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

            System.Type representative = GetRepresentativeComponentTypeCached(gameObject);
            if (representative != null)
            {
                Texture2D componentIcon = EditorGUIUtility.ObjectContent(
                    null,
                    representative).image as Texture2D;
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
                assetPreviewIds.Add(id);
            }

            return preview;
        }

        internal void Clear()
        {
            previews.Clear();
            representativeTypes.Clear();
            assetPreviewIds.Clear();
        }

        internal void ClearAssets()
        {
            foreach (int id in assetPreviewIds) previews.Remove(id);
            assetPreviewIds.Clear();
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

        private System.Type GetRepresentativeComponentTypeCached(GameObject gameObject)
        {
            int id = gameObject.GetInstanceID();
            if (representativeTypes.TryGetValue(id, out System.Type type)) return type;
            type = GetRepresentativeComponentType(gameObject);
            representativeTypes[id] = type;
            return type;
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
            foreach (Renderer renderer in gameObject.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is SkinnedMeshRenderer skinned && skinned.sharedMesh != null) return true;
                if (renderer is SpriteRenderer sprite && sprite.sprite != null) return true;
                if (renderer is MeshRenderer)
                {
                    MeshFilter filter = renderer.GetComponent<MeshFilter>();
                    if (filter != null && filter.sharedMesh != null) return true;
                }
            }
            return false;
        }
    }
}
