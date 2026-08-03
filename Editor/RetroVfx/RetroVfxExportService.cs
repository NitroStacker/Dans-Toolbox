using System;
using System.Collections.Generic;
using System.IO;
using DansToolbox.RetroVfx;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DansToolbox.EditorTools.RetroVfx
{
    internal readonly struct RetroVfxExportResult
    {
        internal RetroVfxExportResult(bool success, string message, IReadOnlyList<string> assetPaths)
        {
            Success = success;
            Message = message;
            AssetPaths = assetPaths;
        }

        internal bool Success { get; }
        internal string Message { get; }
        internal IReadOnlyList<string> AssetPaths { get; }
    }

    internal static class RetroVfxExportService
    {
        internal const string DefaultOutputFolder = "Assets/RetroVfx/Generated";

        internal static RetroVfxExportResult Export(
            RetroVfxRecipe recipe,
            string outputFolder,
            string exportName,
            RetroVfxOutputMode outputMode,
            int flipbookFrameSize,
            int flipbookColumns,
            int flipbookRows)
        {
            List<string> paths = new List<string>();
            if (!TryValidate(recipe, outputFolder, exportName, flipbookFrameSize, flipbookColumns, flipbookRows, out string error))
            {
                return new RetroVfxExportResult(false, error, paths);
            }

            try
            {
                EnsureAssetFolder(outputFolder);
                string safeName = SanitizeFileName(exportName);
                if (outputMode == RetroVfxOutputMode.ParticlePrefab || outputMode == RetroVfxOutputMode.Both)
                {
                    paths.Add(ExportParticlePrefab(recipe, outputFolder, safeName));
                }

                if (outputMode == RetroVfxOutputMode.Flipbook || outputMode == RetroVfxOutputMode.Both)
                {
                    paths.AddRange(BakeFlipbook(
                        recipe,
                        outputFolder,
                        safeName,
                        flipbookFrameSize,
                        flipbookColumns,
                        flipbookRows));
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                if (paths.Count > 0)
                {
                    Object asset = AssetDatabase.LoadMainAssetAtPath(paths[paths.Count - 1]);
                    Selection.activeObject = asset;
                    EditorGUIUtility.PingObject(asset);
                }

                return new RetroVfxExportResult(
                    true,
                    $"Exported {paths.Count} asset{(paths.Count == 1 ? string.Empty : "s")} to {outputFolder}.",
                    paths);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return new RetroVfxExportResult(false, "Retro VFX export failed: " + exception.Message, paths);
            }
        }

        internal static RetroVfxRecipe SaveRecipe(
            RetroVfxRecipe workingRecipe,
            RetroVfxRecipe destination,
            string outputFolder,
            string suggestedName)
        {
            if (workingRecipe == null)
            {
                throw new ArgumentNullException(nameof(workingRecipe));
            }

            workingRecipe.Normalize();
            if (destination == null)
            {
                EnsureAssetFolder(outputFolder);
                string path = AssetDatabase.GenerateUniqueAssetPath(
                    CombineAssetPath(outputFolder, SanitizeFileName(suggestedName) + " Recipe.asset"));
                destination = ScriptableObject.CreateInstance<RetroVfxRecipe>();
                EditorUtility.CopySerialized(workingRecipe, destination);
                destination.hideFlags = HideFlags.None;
                AssetDatabase.CreateAsset(destination, path);
            }
            else
            {
                Undo.RecordObject(destination, "Save Retro VFX Recipe");
                EditorUtility.CopySerialized(workingRecipe, destination);
                destination.hideFlags = HideFlags.None;
                EditorUtility.SetDirty(destination);
            }

            AssetDatabase.SaveAssets();
            Selection.activeObject = destination;
            EditorGUIUtility.PingObject(destination);
            return destination;
        }

        internal static bool TryValidate(
            RetroVfxRecipe recipe,
            string outputFolder,
            string exportName,
            int flipbookFrameSize,
            int flipbookColumns,
            int flipbookRows,
            out string error)
        {
            if (recipe == null)
            {
                error = "No effect recipe is loaded.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(outputFolder) ||
                !(outputFolder == "Assets" || outputFolder.StartsWith("Assets/", StringComparison.Ordinal)))
            {
                error = "The export folder must be inside this project's Assets folder.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(exportName))
            {
                error = "Enter an export name.";
                return false;
            }
            if (flipbookFrameSize < 32 || flipbookFrameSize > 1024)
            {
                error = "Flipbook frame size must be between 32 and 1024 pixels.";
                return false;
            }
            if (flipbookColumns < 1 || flipbookRows < 1 || flipbookColumns * flipbookRows > 256)
            {
                error = "Flipbook layout must contain between 1 and 256 frames.";
                return false;
            }

            recipe.Normalize();
            if (recipe.layers.Count == 0 && recipe.advanced.importedFlipbook == null && recipe.advanced.vfxGraphAsset == null)
            {
                error = "Add at least one layer, imported flipbook, or VFX Graph asset before exporting.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        internal static string SanitizeFileName(string value)
        {
            value = RetroVfxEffectBuilder.SanitizeObjectName(value);
            foreach (char character in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(character, '_');
            }
            return string.IsNullOrWhiteSpace(value) ? "Retro VFX" : value.Trim();
        }

        private static string ExportParticlePrefab(
            RetroVfxRecipe recipe,
            string outputFolder,
            string safeName)
        {
            GameObject root = RetroVfxEffectBuilder.Build(recipe, false);
            try
            {
                PersistGeneratedRendererAssets(root, outputFolder, safeName);
                string path = AssetDatabase.GenerateUniqueAssetPath(
                    CombineAssetPath(outputFolder, safeName + ".prefab"));
                PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
                if (!success)
                {
                    throw new InvalidOperationException("Unity could not save the generated particle prefab.");
                }
                return path;
            }
            finally
            {
                DestroyGeneratedObject(root);
            }
        }

        private static IReadOnlyList<string> BakeFlipbook(
            RetroVfxRecipe recipe,
            string outputFolder,
            string safeName,
            int frameSize,
            int columns,
            int rows)
        {
            int frameCount = columns * rows;
            int sheetWidth = frameSize * columns;
            int sheetHeight = frameSize * rows;
            if (sheetWidth > SystemInfo.maxTextureSize || sheetHeight > SystemInfo.maxTextureSize)
            {
                throw new InvalidOperationException(
                    $"The requested {sheetWidth}×{sheetHeight} flipbook exceeds this GPU's {SystemInfo.maxTextureSize}px texture limit.");
            }

            Scene previewScene = EditorSceneManager.NewPreviewScene();
            GameObject root = null;
            GameObject cameraObject = null;
            RenderTexture target = null;
            Texture2D frame = null;
            Texture2D sheet = null;
            try
            {
                root = RetroVfxEffectBuilder.Build(recipe, true);
                root.hideFlags = HideFlags.None;
                SceneManager.MoveGameObjectToScene(root, previewScene);
                cameraObject = new GameObject("Retro VFX Flipbook Camera");
                SceneManager.MoveGameObjectToScene(cameraObject, previewScene);
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.clear;
                camera.orthographic = true;
                camera.orthographicSize = Mathf.Max(0.25f, 2.6f / Mathf.Max(0.25f, recipe.scale));
                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = 100f;
                camera.transform.position = new Vector3(0f, 0f, -10f);
                camera.transform.rotation = Quaternion.identity;

                target = RenderTexture.GetTemporary(frameSize, frameSize, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
                target.antiAliasing = 1;
                camera.targetTexture = target;
                frame = new Texture2D(frameSize, frameSize, TextureFormat.RGBA32, false, false);
                sheet = new Texture2D(sheetWidth, sheetHeight, TextureFormat.RGBA32, false, false);
                Color[] clear = new Color[sheetWidth * sheetHeight];
                sheet.SetPixels(clear);

                ParticleSystem[] systems = root.GetComponentsInChildren<ParticleSystem>(true);
                float duration = RetroVfxEffectBuilder.CalculateDuration(recipe);
                RenderTexture previous = RenderTexture.active;
                for (int index = 0; index < frameCount; index++)
                {
                    float sampleTime = frameCount <= 1
                        ? 0f
                        : duration * index / (frameCount - 1f);
                    foreach (ParticleSystem system in systems)
                    {
                        system.Simulate(sampleTime, false, true, false);
                    }
                    camera.Render();
                    RenderTexture.active = target;
                    frame.ReadPixels(new Rect(0f, 0f, frameSize, frameSize), 0, 0, false);
                    frame.Apply(false, false);
                    int cellX = index % columns;
                    int visualRow = index / columns;
                    int cellY = rows - 1 - visualRow;
                    sheet.SetPixels(cellX * frameSize, cellY * frameSize, frameSize, frameSize, frame.GetPixels());
                }
                RenderTexture.active = previous;
                sheet.Apply(false, false);

                string texturePath = AssetDatabase.GenerateUniqueAssetPath(
                    CombineAssetPath(outputFolder, safeName + " Flipbook.png"));
                File.WriteAllBytes(ToAbsolutePath(texturePath), sheet.EncodeToPNG());
                AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);
                ConfigureFlipbookTexture(texturePath);
                Texture2D imported = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);

                string materialPath = AssetDatabase.GenerateUniqueAssetPath(
                    CombineAssetPath(outputFolder, safeName + " Flipbook.mat"));
                Material material = CreateFlipbookMaterial(recipe, imported);
                AssetDatabase.CreateAsset(material, materialPath);

                string prefabPath = CreateFlipbookPrefab(
                    recipe,
                    imported,
                    material,
                    outputFolder,
                    safeName,
                    columns,
                    rows,
                    frameCount / Mathf.Max(0.01f, duration));
                return new[] { texturePath, materialPath, prefabPath };
            }
            finally
            {
                if (target != null)
                {
                    RenderTexture.ReleaseTemporary(target);
                }
                if (frame != null)
                {
                    Object.DestroyImmediate(frame);
                }
                if (sheet != null)
                {
                    Object.DestroyImmediate(sheet);
                }
                if (root != null)
                {
                    DestroyGeneratedObject(root);
                }
                if (cameraObject != null)
                {
                    Object.DestroyImmediate(cameraObject);
                }
                EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }

        private static void PersistGeneratedRendererAssets(
            GameObject root,
            string outputFolder,
            string safeName)
        {
            Dictionary<Texture, Texture> textures = new Dictionary<Texture, Texture>();
            int index = 0;
            foreach (ParticleSystemRenderer renderer in root.GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                Material source = renderer.sharedMaterial;
                if (source == null)
                {
                    continue;
                }

                Texture sourceTexture = source.mainTexture;
                Texture persistentTexture = sourceTexture;
                if (sourceTexture != null && !AssetDatabase.Contains(sourceTexture))
                {
                    if (!textures.TryGetValue(sourceTexture, out persistentTexture))
                    {
                        Texture2D texture2D = sourceTexture as Texture2D;
                        if (texture2D != null)
                        {
                            string texturePath = AssetDatabase.GenerateUniqueAssetPath(
                                CombineAssetPath(outputFolder, $"{safeName} {index:00} Texture.png"));
                            File.WriteAllBytes(ToAbsolutePath(texturePath), texture2D.EncodeToPNG());
                            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);
                            ConfigureParticleTexture(texturePath, texture2D.filterMode);
                            persistentTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                            textures.Add(sourceTexture, persistentTexture);
                        }
                    }
                }

                Material persistent = new Material(source)
                {
                    name = $"{safeName} {index:00} {renderer.gameObject.name}",
                    hideFlags = HideFlags.None
                };
                SetMaterialTexture(persistent, persistentTexture);
                string materialPath = AssetDatabase.GenerateUniqueAssetPath(
                    CombineAssetPath(outputFolder, persistent.name + ".mat"));
                AssetDatabase.CreateAsset(persistent, materialPath);
                renderer.sharedMaterial = persistent;
                index++;
            }
        }

        private static string CreateFlipbookPrefab(
            RetroVfxRecipe recipe,
            Texture2D texture,
            Material material,
            string outputFolder,
            string safeName,
            int columns,
            int rows,
            float framesPerSecond)
        {
            GameObject root = new GameObject(safeName + " Flipbook");
            try
            {
                ParticleSystem system = root.AddComponent<ParticleSystem>();
                ParticleSystem.MainModule main = system.main;
                float duration = columns * rows / Mathf.Max(1f, framesPerSecond);
                main.duration = Mathf.Max(0.05f, duration);
                main.startLifetime = main.duration;
                main.startSpeed = 0f;
                main.startSize = recipe.scale;
                main.loop = recipe.loopPreview;
                main.playOnAwake = true;
                main.maxParticles = 1;
                ParticleSystem.EmissionModule emission = system.emission;
                emission.rateOverTime = 0f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
                ParticleSystem.ShapeModule shape = system.shape;
                shape.enabled = false;
                ParticleSystem.TextureSheetAnimationModule animation = system.textureSheetAnimation;
                animation.enabled = true;
                animation.mode = ParticleSystemAnimationMode.Grid;
                animation.animation = ParticleSystemAnimationType.WholeSheet;
                animation.numTilesX = columns;
                animation.numTilesY = rows;
                animation.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, 1f));
                animation.cycleCount = 1;
                ParticleSystemRenderer renderer = root.GetComponent<ParticleSystemRenderer>();
                renderer.sharedMaterial = material;

                AudioSource source = null;
                if (recipe.audioClip != null)
                {
                    source = root.AddComponent<AudioSource>();
                    source.clip = recipe.audioClip;
                    source.playOnAwake = true;
                    source.spatialBlend = 1f;
                }

                Light light = null;
                if (recipe.advanced.lightEnabled)
                {
                    light = root.AddComponent<Light>();
                    light.type = recipe.advanced.lightType;
                    light.color = recipe.advanced.lightColor;
                    light.intensity = recipe.advanced.lightIntensity;
                    light.range = recipe.advanced.lightRange;
                    light.shadows = recipe.advanced.lightShadows;
                }

                RetroVfxPlayer player = root.AddComponent<RetroVfxPlayer>();
                player.Configure(
                    main.duration,
                    new[] { system },
                    source,
                    light,
                    recipe.advanced.lightIntensity,
                    recipe.advanced.lightIntensityOverLifetime,
                    recipe.advanced.cameraShakeEnabled,
                    recipe.advanced.cameraShakeAmplitude,
                    recipe.advanced.cameraShakeDuration,
                    recipe.advanced.hitStopEventEnabled,
                    recipe.advanced.hitStopDuration,
                    recipe.advanced.decalEventEnabled,
                    recipe.advanced.decalPrefab);

                string path = AssetDatabase.GenerateUniqueAssetPath(
                    CombineAssetPath(outputFolder, safeName + " Flipbook.prefab"));
                PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
                if (!success)
                {
                    throw new InvalidOperationException("Unity could not save the generated flipbook prefab.");
                }
                return path;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static Material CreateFlipbookMaterial(RetroVfxRecipe recipe, Texture texture)
        {
            Material material;
            if (recipe.advanced.customMaterial != null)
            {
                material = new Material(recipe.advanced.customMaterial);
            }
            else
            {
                Shader shader = recipe.advanced.customShader ?? FindParticleShader();
                material = new Material(shader);
            }
            material.name = recipe.displayName + " Flipbook";
            material.hideFlags = HideFlags.None;
            SetMaterialTexture(material, texture);
            return material;
        }

        private static void ConfigureParticleTexture(string path, FilterMode filterMode = FilterMode.Bilinear)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                return;
            }
            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = filterMode;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        private static void ConfigureFlipbookTexture(string path)
        {
            ConfigureParticleTexture(path);
        }

        private static void SetMaterialTexture(Material material, Texture texture)
        {
            if (texture == null)
            {
                return;
            }
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", texture);
            }
            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", texture);
            }
            material.mainTexture = texture;
        }

        private static Shader FindParticleShader()
        {
            return Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
                   Shader.Find("Particles/Standard Unlit") ??
                   Shader.Find("Legacy Shaders/Particles/Additive") ??
                   Shader.Find("Unlit/Transparent") ??
                   Shader.Find("Sprites/Default");
        }

        private static void DestroyGeneratedObject(GameObject root)
        {
            if (root == null)
            {
                return;
            }
            HashSet<Object> transient = new HashSet<Object>();
            foreach (ParticleSystemRenderer renderer in root.GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                Material material = renderer.sharedMaterial;
                if (material != null && !AssetDatabase.Contains(material))
                {
                    transient.Add(material);
                    if (material.mainTexture != null && !AssetDatabase.Contains(material.mainTexture))
                    {
                        transient.Add(material.mainTexture);
                    }
                }
            }
            Object.DestroyImmediate(root);
            foreach (Object item in transient)
            {
                if (item != null)
                {
                    Object.DestroyImmediate(item);
                }
            }
        }

        private static void EnsureAssetFolder(string folder)
        {
            string normalized = folder.Replace('\\', '/').TrimEnd('/');
            if (normalized == "Assets")
            {
                return;
            }
            string[] parts = normalized.Split('/');
            string current = parts[0];
            for (int index = 1; index < parts.Length; index++)
            {
                string next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[index]);
                }
                current = next;
            }
        }

        private static string CombineAssetPath(string folder, string fileName)
        {
            return folder.TrimEnd('/', '\\') + "/" + fileName;
        }

        private static string ToAbsolutePath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
