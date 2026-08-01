using System;
using System.Collections.Generic;
using System.Linq;
using DansToolbox.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DansToolbox.EditorTools.BetterHierarchy
{
    internal sealed class BetterHierarchyAtlasView : IDisposable
    {
        private readonly BetterHierarchyWindow host;
        private readonly BetterHierarchyPreviewCache previews = new BetterHierarchyPreviewCache();
        private List<AtlasEntry> entries = new List<AtlasEntry>();
        private bool dirty = true;
        private bool assetsDirty = true;
        private string cachedSearch = string.Empty;
        private BetterHierarchyAtlasSource cachedSource;
        private List<GameObject> prefabAssets = new List<GameObject>();

        internal BetterHierarchyAtlasView(BetterHierarchyWindow host)
        {
            this.host = host;
        }

        internal void Draw(
            Rect rect,
            ref Vector2 scroll,
            string search,
            ref BetterHierarchyAtlasSource source,
            ref float tileSize,
            DansToolboxPalette palette)
        {
            Rect tools = new Rect(rect.x, rect.y, rect.width, 34f);
            Rect grid = new Rect(rect.x, tools.yMax, rect.width, Mathf.Max(1f, rect.height - tools.height));
            DrawTools(tools, source, ref tileSize, palette, out BetterHierarchyAtlasSource selectedSource);
            if (selectedSource != source)
            {
                source = selectedSource;
                dirty = true;
            }

            EnsureEntries(search, source);
            DrawGrid(grid, ref scroll, tileSize, palette);
        }

        internal BetterHierarchyAtlasSource DrawTools(
            Rect rect,
            BetterHierarchyAtlasSource source,
            ref float tileSize,
            DansToolboxPalette palette,
            out BetterHierarchyAtlasSource selected)
        {
            EditorGUI.DrawRect(rect, palette.Inset);
            selected = source;
            float x = rect.x + 6f;
            foreach ((BetterHierarchyAtlasSource value, string label) in new[]
                     {
                         (BetterHierarchyAtlasSource.Scene, "ALL"),
                         (BetterHierarchyAtlasSource.Selection, "BRANCH"),
                         (BetterHierarchyAtlasSource.Favorites, "★"),
                         (BetterHierarchyAtlasSource.Recent, "RECENT"),
                         (BetterHierarchyAtlasSource.Prefabs, "CREATE")
                     })
            {
                float width = label.Length <= 1 ? 28f : label.Length * 7f + 14f;
                Rect button = new Rect(x, rect.y + 6f, width, 22f);
                bool active = source == value;
                if (BetterHierarchyWindow.DrawFlatButton(button, label, value.ToString(), active, palette))
                {
                    selected = value;
                    dirty = true;
                }
                x += width + 4f;
            }

            if (rect.width > 470f)
            {
                Rect slider = new Rect(rect.xMax - 112f, rect.y + 8f, 104f, 18f);
                tileSize = GUI.HorizontalSlider(slider, tileSize, 72f, 180f);
            }
            return selected;
        }

        private void DrawGrid(Rect viewport, ref Vector2 scroll, float requestedTile, DansToolboxPalette palette)
        {
            EditorGUI.DrawRect(viewport, BetterHierarchyWindow.CanvasColor);
            if (entries.Count == 0)
            {
                DrawEmpty(viewport, palette);
                HandleBlankInput(viewport);
                return;
            }

            float gap = 8f;
            float available = Mathf.Max(80f, viewport.width - 16f);
            int columns = Mathf.Max(1, Mathf.FloorToInt((available + gap) / (requestedTile + gap)));
            float cardWidth = (available - gap * (columns - 1)) / columns;
            float previewHeight = Mathf.Max(54f, cardWidth - 26f);
            float cardHeight = previewHeight + 34f;
            int rows = Mathf.CeilToInt(entries.Count / (float)columns);
            Rect content = new Rect(0f, 0f, available, Mathf.Max(viewport.height, rows * (cardHeight + gap) + 8f));
            scroll = GUI.BeginScrollView(viewport, scroll, content, false, true);
            EditorGUI.DrawRect(content, BetterHierarchyWindow.CanvasColor);

            int firstRow = Mathf.Max(0, Mathf.FloorToInt(scroll.y / (cardHeight + gap)) - 1);
            int lastRow = Mathf.Min(rows - 1,
                Mathf.CeilToInt((scroll.y + viewport.height) / (cardHeight + gap)) + 1);
            for (int row = firstRow; row <= lastRow; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    int index = row * columns + column;
                    if (index >= entries.Count)
                    {
                        break;
                    }

                    Rect card = new Rect(
                        column * (cardWidth + gap),
                        row * (cardHeight + gap) + 4f,
                        cardWidth,
                        cardHeight);
                    DrawCard(card, entries[index], previewHeight, palette);
                }
            }

            GUI.EndScrollView();
            HandleBlankInput(viewport);
        }

        private void DrawCard(Rect rect, AtlasEntry entry, float previewHeight, DansToolboxPalette palette)
        {
            Event current = Event.current;
            bool hovered = rect.Contains(current.mousePosition);
            bool selected = entry.GameObject != null && Selection.gameObjects.Contains(entry.GameObject) ||
                            entry.Asset != null && Selection.objects.Contains(entry.Asset);
            Color border = selected ? palette.Accent : hovered ? palette.BorderStrong : palette.Border;
            EditorGUI.DrawRect(rect, border);
            EditorGUI.DrawRect(new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, rect.height - 2f),
                hovered ? palette.Raised : palette.Panel);

            Rect previewRect = new Rect(rect.x + 5f, rect.y + 5f, rect.width - 10f, previewHeight - 8f);
            EditorGUI.DrawRect(previewRect, palette.Inset);
            Texture2D preview = entry.GameObject != null
                ? previews.Get(entry.GameObject)
                : previews.Get(entry.Asset);
            if (preview != null)
            {
                GUI.DrawTexture(previewRect, preview, ScaleMode.ScaleToFit, true);
            }
            else
            {
                GUI.Label(previewRect, entry.IsPrefab ? "PREFAB" : "OBJECT", new GUIStyle(EditorStyles.miniBoldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = palette.Muted }
                });
            }

            Rect nameRect = new Rect(rect.x + 7f, previewRect.yMax + 6f, rect.width - 14f, 17f);
            GUI.Label(nameRect, new GUIContent(entry.Name, entry.Path), new GUIStyle(EditorStyles.miniBoldLabel)
            {
                clipping = TextClipping.Clip,
                normal = { textColor = palette.Text }
            });

            if (entry.GameObject != null)
            {
                BetterHierarchyDiagnosticFlags diagnostics = BetterHierarchyUserSettings.Diagnostics
                    ? BetterHierarchyDiagnostics.Get(entry.GameObject)
                    : BetterHierarchyDiagnosticFlags.None;
                if (diagnostics != BetterHierarchyDiagnosticFlags.None)
                {
                    Rect warning = new Rect(rect.xMax - 21f, rect.y + 7f, 14f, 14f);
                    EditorGUI.DrawRect(warning, BetterHierarchyDiagnostics.IsCritical(diagnostics)
                        ? palette.Danger
                        : palette.Warning);
                    GUI.Label(warning, new GUIContent("!", BetterHierarchyDiagnostics.GetTooltip(diagnostics)),
                        new GUIStyle(EditorStyles.miniBoldLabel)
                        {
                            alignment = TextAnchor.MiddleCenter,
                            normal = { textColor = Color.black }
                        });
                }

                if (BetterHierarchyUserSettings.IsFavorite(entry.GameObject))
                {
                    GUI.Label(new Rect(rect.x + 8f, rect.y + 7f, 16f, 16f), "★",
                        new GUIStyle(EditorStyles.miniBoldLabel) { normal = { textColor = palette.Accent } });
                }
            }

            HandleCardInput(rect, entry);
        }

        private void HandleCardInput(Rect rect, AtlasEntry entry)
        {
            Event current = Event.current;
            if (!rect.Contains(current.mousePosition))
            {
                return;
            }

            if (current.type == EventType.MouseDown && current.button == 0)
            {
                if (entry.GameObject != null)
                {
                    if (current.control || current.command)
                    {
                        List<UnityEngine.Object> selection = Selection.objects.ToList();
                        if (!selection.Remove(entry.GameObject))
                        {
                            selection.Add(entry.GameObject);
                        }
                        Selection.objects = selection.ToArray();
                    }
                    else
                    {
                        Selection.activeGameObject = entry.GameObject;
                    }

                    if (current.clickCount == 2)
                    {
                        SceneView.lastActiveSceneView?.FrameSelected();
                    }
                }
                else if (entry.Asset != null)
                {
                    Selection.activeObject = entry.Asset;
                    if (current.clickCount == 2 && entry.Asset is GameObject prefab)
                    {
                        PlacePrefab(prefab);
                    }
                }

                current.Use();
            }
            else if (current.type == EventType.MouseDrag && current.button == 0)
            {
                DragAndDrop.PrepareStartDrag();
                DragAndDrop.objectReferences = new[] { entry.GameObject != null ? entry.GameObject : entry.Asset };
                DragAndDrop.StartDrag(entry.Name);
                current.Use();
            }
            else if (current.type == EventType.ContextClick)
            {
                if (entry.GameObject != null)
                {
                    if (!Selection.gameObjects.Contains(entry.GameObject))
                    {
                        Selection.activeGameObject = entry.GameObject;
                    }
                    host.ShowGameObjectContextMenu(entry.GameObject);
                }
                else
                {
                    ShowContext(entry);
                }
                current.Use();
            }
        }

        private void HandleBlankInput(Rect viewport)
        {
            Event current = Event.current;
            if (!viewport.Contains(current.mousePosition))
            {
                return;
            }

            if (current.type == EventType.MouseDown && current.button == 0)
            {
                Selection.objects = Array.Empty<UnityEngine.Object>();
                current.Use();
            }
            else if (current.type == EventType.ContextClick)
            {
                Selection.objects = Array.Empty<UnityEngine.Object>();
                host.ShowBlankContextMenu();
                current.Use();
            }
        }

        private void ShowContext(AtlasEntry entry)
        {
            GenericMenu menu = new GenericMenu();
            if (entry.GameObject != null)
            {
                GameObject gameObject = entry.GameObject;
                menu.AddItem(new GUIContent("Frame"), false, () =>
                {
                    Selection.activeGameObject = gameObject;
                    SceneView.lastActiveSceneView?.FrameSelected();
                });
                menu.AddItem(new GUIContent("Isolate"), false, () => SceneVisibilityManager.instance.Isolate(gameObject, true));
                menu.AddItem(new GUIContent("Favorite"), BetterHierarchyUserSettings.IsFavorite(gameObject), () =>
                {
                    BetterHierarchyUserSettings.ToggleFavorite(gameObject);
                    Invalidate();
                });
                menu.AddItem(new GUIContent(gameObject.activeSelf ? "Disable" : "Enable"), false, () =>
                {
                    Undo.RecordObject(gameObject, "Toggle GameObject");
                    gameObject.SetActive(!gameObject.activeSelf);
                });
                menu.AddItem(new GUIContent("Virtual Collection"), false, () =>
                {
                    Selection.activeGameObject = gameObject;
                    host.ShowCollectionPopup(true);
                });
            }
            else if (entry.Asset is GameObject prefab)
            {
                menu.AddItem(new GUIContent("Place"), false, () => PlacePrefab(prefab));
                menu.AddItem(new GUIContent("Ping"), false, () => EditorGUIUtility.PingObject(prefab));
                menu.AddItem(new GUIContent("Open Prefab"), false, () => AssetDatabase.OpenAsset(prefab));
            }
            menu.ShowAsContext();
        }

        private static void PlacePrefab(GameObject prefab)
        {
            Transform parent = BetterHierarchyUserSettings.DefaultParent != null
                ? BetterHierarchyUserSettings.DefaultParent.transform
                : Selection.activeTransform;
            Scene scene = parent != null ? parent.gameObject.scene : SceneManager.GetActiveScene();
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            if (instance == null)
            {
                return;
            }

            Undo.RegisterCreatedObjectUndo(instance, "Place " + prefab.name);
            if (parent != null && !PrefabUtility.IsPartOfPrefabAsset(parent))
            {
                Undo.SetTransformParent(instance.transform, parent, "Parent " + prefab.name);
            }
            Selection.activeGameObject = instance;
            SceneView.lastActiveSceneView?.FrameSelected();
        }

        private void EnsureEntries(string search, BetterHierarchyAtlasSource source)
        {
            if (!dirty && string.Equals(search, cachedSearch, StringComparison.Ordinal) && cachedSource == source)
            {
                return;
            }

            dirty = false;
            cachedSearch = search ?? string.Empty;
            cachedSource = source;
            BetterHierarchyQuery query = BetterHierarchyQuery.Parse(cachedSearch);
            entries.Clear();

            if (source == BetterHierarchyAtlasSource.Prefabs)
            {
                EnsurePrefabAssets();
                foreach (GameObject prefab in prefabAssets)
                {
                    if (query.IsEmpty || query.Matches(prefab, BetterHierarchyDiagnosticFlags.None))
                    {
                        entries.Add(new AtlasEntry(prefab));
                    }
                }
            }
            else
            {
                IEnumerable<GameObject> sourceObjects = GetSourceObjects(source);
                foreach (GameObject gameObject in sourceObjects.Where(gameObject => gameObject != null).Distinct())
                {
                    BetterHierarchyDiagnosticFlags diagnostics = BetterHierarchyUserSettings.Diagnostics
                        ? BetterHierarchyDiagnostics.Get(gameObject)
                        : BetterHierarchyDiagnosticFlags.None;
                    if (query.IsEmpty || query.Matches(gameObject, diagnostics, BetterHierarchyCollections.Contains))
                    {
                        entries.Add(new AtlasEntry(gameObject, false));
                    }
                }
            }

            entries = entries
                .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private IEnumerable<GameObject> GetSourceObjects(BetterHierarchyAtlasSource source)
        {
            switch (source)
            {
                case BetterHierarchyAtlasSource.Selection:
                    return Selection.gameObjects.SelectMany(GetBranch);
                case BetterHierarchyAtlasSource.Favorites:
                    return BetterHierarchyUserSettings.Favorites;
                case BetterHierarchyAtlasSource.Recent:
                    return BetterHierarchyUserSettings.Recent;
                default:
                    return GetLoadedSceneObjects();
            }
        }

        private static IEnumerable<GameObject> GetLoadedSceneObjects()
        {
            for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                Scene scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.isLoaded)
                {
                    continue;
                }

                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (GameObject gameObject in GetBranch(root))
                    {
                        yield return gameObject;
                    }
                }
            }
        }

        private static IEnumerable<GameObject> GetBranch(GameObject root)
        {
            if (root == null)
            {
                yield break;
            }

            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                yield return transform.gameObject;
            }
        }

        private void EnsurePrefabAssets()
        {
            if (!assetsDirty)
            {
                return;
            }

            assetsDirty = false;
            prefabAssets = AssetDatabase.FindAssets("t:Prefab")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.StartsWith("Assets/", StringComparison.Ordinal))
                .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
                .Where(prefab => prefab != null)
                .ToList();
        }

        private static void DrawEmpty(Rect rect, DansToolboxPalette palette)
        {
            GUI.Label(new Rect(rect.x + 20f, rect.center.y - 26f, rect.width - 40f, 22f), "NO MATCHES",
                new GUIStyle(EditorStyles.boldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = palette.Text }
                });
            GUI.Label(new Rect(rect.x + 20f, rect.center.y, rect.width - 40f, 20f), "Clear filters or choose another source.",
                new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = palette.Muted }
                });
        }

        internal void SelectAllEntries()
        {
            Selection.objects = entries
                .Select(entry => entry.GameObject != null ? entry.GameObject : entry.Asset)
                .Where(target => target != null)
                .Distinct()
                .ToArray();
        }

        internal void Invalidate()
        {
            dirty = true;
            previews.Clear();
            host.Repaint();
        }

        internal void InvalidateAssets()
        {
            assetsDirty = true;
            Invalidate();
        }

        public void Dispose()
        {
            previews.Clear();
        }

        private readonly struct AtlasEntry
        {
            internal AtlasEntry(GameObject gameObject, bool prefabAsset = true)
            {
                GameObject = prefabAsset && AssetDatabase.Contains(gameObject) ? null : gameObject;
                Asset = prefabAsset && AssetDatabase.Contains(gameObject) ? gameObject : null;
                Name = gameObject != null ? gameObject.name : string.Empty;
                Path = gameObject != null
                    ? AssetDatabase.Contains(gameObject)
                        ? AssetDatabase.GetAssetPath(gameObject)
                        : BetterHierarchyQuery.GetPath(gameObject.transform)
                    : string.Empty;
                IsPrefab = Asset != null;
            }

            internal GameObject GameObject { get; }
            internal UnityEngine.Object Asset { get; }
            internal string Name { get; }
            internal string Path { get; }
            internal bool IsPrefab { get; }
        }
    }
}
