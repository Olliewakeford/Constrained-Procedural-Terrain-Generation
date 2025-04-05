using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using TerrainGeneration.Generators;
using TerrainGeneration.SmoothingAndErosion;
using TerrainGeneration.Core;

namespace TerrainGeneration.Editor
{
    [CustomEditor(typeof(TerrainManager))]
    public class TerrainManagerEditor : UnityEditor.Editor
    {
        #region Serialized Properties
        
        // Common settings properties
        private SerializedProperty terrainProp;
        private SerializedProperty maskProp;
        private SerializedProperty restoreTerrainProp;
        private SerializedProperty savedPresetsProp;
        
        // Generator instances - We'll keep these as regular objects in the editor
        // since we're creating and modifying them only through the editor UI
        private UniformHeightGenerator uniformGenerator = new UniformHeightGenerator();
        private RandomHeightGenerator randomGenerator = new RandomHeightGenerator();
        private PerlinNoiseGenerator perlinGenerator = new PerlinNoiseGenerator();
        private VoronoiGenerator voronoiGenerator = new VoronoiGenerator();
        
        // Smoother instances
        private BasicSmoother basicSmoother = new BasicSmoother();
        private DistanceBasedSmoother distanceBasedSmoother = new DistanceBasedSmoother();
        private AdaptiveSmoother adaptiveSmoother = new AdaptiveSmoother();
        
        #endregion

        #region Editor State
        
        private TerrainManager terrainManager;
        
        // Foldout states
        private bool showCommonSettings = true;
        private bool showOneClickGeneration = false;
        private bool showUniformGenerator = false;
        private bool showRandomGenerator = false;
        private bool showPerlinGenerator = false;
        private bool showMultiPerlinGenerator = false;
        private bool showVoronoiGenerator = false;
        private bool showSmoothing = false;
        private bool showDistanceGrid = false;
        private bool showPresets = false;
        
        // For building a generation preset
        private List<ITerrainGenerator> presetGenerators = new List<ITerrainGenerator>();
        private ITerrainSmoother presetSmoother = null;
        private string presetName = "New Preset";
        
        // For multi-Perlin
        private List<PerlinNoiseGenerator> perlinLayers = new List<PerlinNoiseGenerator>
        {
            new PerlinNoiseGenerator()
        };
        private List<bool> perlinLayersRemove = new List<bool> { false };
        
        #endregion

        #region Unity Methods
        
        private void OnEnable()
        {
            // Get a reference to the TerrainManager
            terrainManager = (TerrainManager)target;
            
            // Find serialized properties for common settings
            terrainProp = serializedObject.FindProperty("terrain");
            maskProp = serializedObject.FindProperty("mask");
            restoreTerrainProp = serializedObject.FindProperty("restoreTerrain");
            savedPresetsProp = serializedObject.FindProperty("savedPresets");
        }
        
        public override void OnInspectorGUI()
        {
            // Update the serializedObject representation
            serializedObject.Update();
            
            // Common settings
            DrawCommonSettings();
            
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            
            // One-click generation
            DrawOneClickGeneration();
            
            // Individual generators
            DrawUniformGenerator();
            DrawRandomGenerator();
            DrawPerlinGenerator();
            DrawMultiPerlinGenerator();
            DrawVoronoiGenerator();
            
            // Smoothing
            DrawSmoothing();
            
            // Distance grid
            DrawDistanceGrid();
            
            // Presets
            DrawPresets();
            
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            
            // Restore terrain button
            if (GUILayout.Button("Restore Terrain"))
            {
                terrainManager.RestoreTerrain();
            }
            
            // Apply modifications to the serializedObject
            serializedObject.ApplyModifiedProperties();
            
            // Unity handles the undo/redo and marking objects as dirty automatically
            // when using serializedObject.ApplyModifiedProperties()
        }
        
        #endregion

        #region Drawing Methods
        
        private void DrawCommonSettings()
        {
            showCommonSettings = EditorGUILayout.Foldout(showCommonSettings, "Common Settings");
            
            if (showCommonSettings)
            {
                EditorGUILayout.PropertyField(terrainProp, new GUIContent("Terrain"));
                EditorGUILayout.PropertyField(maskProp, new GUIContent("Mask Texture"));
                EditorGUILayout.PropertyField(restoreTerrainProp, new GUIContent("Reset Before Generating"));
            }
        }
        
        private void DrawOneClickGeneration()
        {
            showOneClickGeneration = EditorGUILayout.Foldout(showOneClickGeneration, "One-Click Generation");
            
            if (showOneClickGeneration)
            {
                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
                
                EditorGUILayout.LabelField("Build a Generation Preset", EditorStyles.boldLabel);
                
                // Preset name
                presetName = EditorGUILayout.TextField("Preset Name", presetName);
                
                // Add generators
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Generators", EditorStyles.boldLabel);
                
                // Show current generators in preset
                for (int i = 0; i < presetGenerators.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"{i + 1}. {presetGenerators[i].Name}");
                    
                    if (GUILayout.Button("Remove", GUILayout.Width(80)))
                    {
                        presetGenerators.RemoveAt(i);
                        i--;
                    }
                    
                    EditorGUILayout.EndHorizontal();
                }
                
                // Add generator buttons
                EditorGUILayout.BeginHorizontal();
                
                if (GUILayout.Button("Add Perlin"))
                {
                    presetGenerators.Add(perlinGenerator.Clone());
                }
                
                if (GUILayout.Button("Add Voronoi"))
                {
                    presetGenerators.Add(voronoiGenerator.Clone());
                }
                
                if (GUILayout.Button("Add Random"))
                {
                    presetGenerators.Add(randomGenerator.Clone());
                }
                
                EditorGUILayout.EndHorizontal();
                
                // Add smoother
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Smoother", EditorStyles.boldLabel);
                
                if (presetSmoother == null)
                {
                    EditorGUILayout.LabelField("No smoother selected");
                    
                    EditorGUILayout.BeginHorizontal();
                    
                    if (GUILayout.Button("Basic Smoother"))
                    {
                        presetSmoother = basicSmoother.Clone();
                    }
                    
                    if (GUILayout.Button("Distance Smoother"))
                    {
                        presetSmoother = distanceBasedSmoother.Clone();
                    }
                    
                    if (GUILayout.Button("Adaptive Smoother"))
                    {
                        presetSmoother = adaptiveSmoother.Clone();
                    }
                    
                    EditorGUILayout.EndHorizontal();
                }
                else
                {
                    EditorGUILayout.LabelField($"Selected: {presetSmoother.Name}");
                    
                    if (GUILayout.Button("Remove Smoother"))
                    {
                        presetSmoother = null;
                    }
                }
                
                // Save and generate buttons
                EditorGUILayout.Space(10);
                EditorGUILayout.BeginHorizontal();
                
                if (GUILayout.Button("Save Preset"))
                {
                    if (presetGenerators.Count == 0)
                    {
                        EditorUtility.DisplayDialog("Error", "You must add at least one generator to save a preset.", "OK");
                    }
                    else
                    {
                        TerrainGenerationPreset preset = new TerrainGenerationPreset(presetName);
                        preset.Generators.AddRange(presetGenerators);
                        preset.Smoother = presetSmoother;
                        
                        // Add the preset to the list
                        Undo.RecordObject(terrainManager, "Save Terrain Generation Preset");
                        terrainManager.savedPresets.Add(preset);
                        EditorUtility.SetDirty(terrainManager);
                        
                        EditorUtility.DisplayDialog("Success", $"Preset '{presetName}' saved successfully.", "OK");
                    }
                }
                
                if (GUILayout.Button("Generate"))
                {
                    if (presetGenerators.Count == 0)
                    {
                        EditorUtility.DisplayDialog("Error", "You must add at least one generator to generate terrain.", "OK");
                    }
                    else
                    {
                        TerrainGenerationPreset preset = new TerrainGenerationPreset(presetName);
                        preset.Generators.AddRange(presetGenerators);
                        preset.Smoother = presetSmoother;
                        
                        // Register this action for undo
                        Undo.RegisterCompleteObjectUndo(terrainManager.terrain.terrainData, "Generate Terrain");
                        terrainManager.ApplyPreset(preset);
                    }
                }
                
                EditorGUILayout.EndHorizontal();
            }
        }
        
        private void DrawUniformGenerator()
        {
            showUniformGenerator = EditorGUILayout.Foldout(showUniformGenerator, "Uniform Height Change");
            
            if (showUniformGenerator)
            {
                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
                
                // Uniform height parameters
                uniformGenerator.UniformStep = EditorGUILayout.Slider(
                    "Uniform Increment",
                    uniformGenerator.UniformStep,
                    -1.0f, 1.0f
                );
                
                if (GUILayout.Button("Apply Uniform Height"))
                {
                    // Register the operation for undo
                    Undo.RegisterCompleteObjectUndo(terrainManager.terrain.terrainData, "Apply Uniform Height");
                    terrainManager.ApplyGenerator(uniformGenerator);
                }
            }
        }
        
        private void DrawRandomGenerator()
        {
            showRandomGenerator = EditorGUILayout.Foldout(showRandomGenerator, "Random Height Change");
            
            if (showRandomGenerator)
            {
                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
                
                // Random height parameters - Handle differently because MinMaxSlider needs refs
                Vector2 heightLimits = randomGenerator.HeightLimits;
                EditorGUILayout.MinMaxSlider(
                    "Height Range",
                    ref heightLimits.x,
                    ref heightLimits.y,
                    0f, 1f
                );
                randomGenerator.HeightLimits = heightLimits;
                
                EditorGUILayout.LabelField($"Min: {randomGenerator.HeightLimits.x:F2}, Max: {randomGenerator.HeightLimits.y:F2}");
                
                if (GUILayout.Button("Apply Random Heights"))
                {
                    Undo.RegisterCompleteObjectUndo(terrainManager.terrain.terrainData, "Apply Random Heights");
                    terrainManager.ApplyGenerator(randomGenerator);
                }
            }
        }
        
        private void DrawPerlinGenerator()
        {
            showPerlinGenerator = EditorGUILayout.Foldout(showPerlinGenerator, "Perlin Noise");
            
            if (showPerlinGenerator)
            {
                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
                
                // Perlin noise parameters
                perlinGenerator.XFrequency = EditorGUILayout.Slider(
                    "X Frequency",
                    perlinGenerator.XFrequency,
                    0f, 0.1f
                );
                
                perlinGenerator.YFrequency = EditorGUILayout.Slider(
                    "Y Frequency",
                    perlinGenerator.YFrequency,
                    0f, 0.1f
                );
                
                perlinGenerator.XOffset = EditorGUILayout.IntSlider(
                    "X Offset",
                    perlinGenerator.XOffset,
                    0, 10000
                );
                
                perlinGenerator.YOffset = EditorGUILayout.IntSlider(
                    "Y Offset",
                    perlinGenerator.YOffset,
                    0, 10000
                );
                
                perlinGenerator.Octaves = EditorGUILayout.IntSlider(
                    "Octaves",
                    perlinGenerator.Octaves,
                    1, 10
                );
                
                perlinGenerator.Persistence = EditorGUILayout.Slider(
                    "Persistence",
                    perlinGenerator.Persistence,
                    0.1f, 10f
                );
                
                perlinGenerator.Amplitude = EditorGUILayout.Slider(
                    "Amplitude",
                    perlinGenerator.Amplitude,
                    0f, 1f
                );
                
                if (GUILayout.Button("Apply Perlin Noise"))
                {
                    Undo.RegisterCompleteObjectUndo(terrainManager.terrain.terrainData, "Apply Perlin Noise");
                    terrainManager.ApplyGenerator(perlinGenerator);
                }
                
                if (GUILayout.Button("Add to Preset"))
                {
                    presetGenerators.Add(perlinGenerator.Clone());
                }
            }
        }
        
        private void DrawMultiPerlinGenerator()
        {
            showMultiPerlinGenerator = EditorGUILayout.Foldout(showMultiPerlinGenerator, "Multiple Perlin Layers");
            
            if (showMultiPerlinGenerator)
            {
                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
                
                // Draw each layer
                for (int i = 0; i < perlinLayers.Count; i++)
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    
                    EditorGUILayout.LabelField($"Layer {i + 1}", EditorStyles.boldLabel);
                    
                    perlinLayers[i].XFrequency = EditorGUILayout.Slider(
                        "X Frequency", 
                        perlinLayers[i].XFrequency, 
                        0f, 0.1f
                    );
                    
                    perlinLayers[i].YFrequency = EditorGUILayout.Slider(
                        "Y Frequency", 
                        perlinLayers[i].YFrequency, 
                        0f, 0.1f
                    );
                    
                    perlinLayers[i].Octaves = EditorGUILayout.IntSlider(
                        "Octaves",
                        perlinLayers[i].Octaves,
                        1, 10
                    );
                    
                    perlinLayers[i].Persistence = EditorGUILayout.Slider(
                        "Persistence",
                        perlinLayers[i].Persistence,
                        0.1f, 10f
                    );
                    
                    perlinLayers[i].Amplitude = EditorGUILayout.Slider(
                        "Amplitude",
                        perlinLayers[i].Amplitude,
                        0f, 1f
                    );
                    
                    perlinLayersRemove[i] = EditorGUILayout.Toggle("Remove", perlinLayersRemove[i]);
                    
                    EditorGUILayout.EndVertical();
                    
                    EditorGUILayout.Space(5);
                }
                
                // Add/remove layer buttons
                EditorGUILayout.BeginHorizontal();
                
                if (GUILayout.Button("+"))
                {
                    perlinLayers.Add(new PerlinNoiseGenerator());
                    perlinLayersRemove.Add(false);
                }
                
                if (GUILayout.Button("-"))
                {
                    // Remove marked layers
                    for (int i = perlinLayers.Count - 1; i >= 0; i--)
                    {
                        if (perlinLayersRemove[i])
                        {
                            perlinLayers.RemoveAt(i);
                            perlinLayersRemove.RemoveAt(i);
                        }
                    }
                    
                    // Ensure we have at least one layer
                    if (perlinLayers.Count == 0)
                    {
                        perlinLayers.Add(new PerlinNoiseGenerator());
                        perlinLayersRemove.Add(false);
                    }
                }
                
                EditorGUILayout.EndHorizontal();
                
                // Apply button
                if (GUILayout.Button("Apply Multiple Perlin Layers"))
                {
                    Undo.RegisterCompleteObjectUndo(terrainManager.terrain.terrainData, "Apply Multiple Perlin Layers");
                    
                    bool wasRestore = terrainManager.restoreTerrain;
                    terrainManager.restoreTerrain = true;
                    
                    // Apply first layer
                    terrainManager.ApplyGenerator(perlinLayers[0]);
                    
                    // Apply subsequent layers without restoring
                    terrainManager.restoreTerrain = false;
                    for (int i = 1; i < perlinLayers.Count; i++)
                    {
                        terrainManager.ApplyGenerator(perlinLayers[i]);
                    }
                    
                    terrainManager.restoreTerrain = wasRestore;
                }
                
                if (GUILayout.Button("Add All Layers to Preset"))
                {
                    foreach (var layer in perlinLayers)
                    {
                        presetGenerators.Add(layer.Clone());
                    }
                }
            }
        }
        
        private void DrawVoronoiGenerator()
        {
            showVoronoiGenerator = EditorGUILayout.Foldout(showVoronoiGenerator, "Voronoi Tessellation");
            
            if (showVoronoiGenerator)
            {
                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
                
                // Voronoi parameters
                voronoiGenerator.PeakCount = EditorGUILayout.IntSlider(
                    "Peak Count",
                    voronoiGenerator.PeakCount,
                    1, 20
                );
                
                voronoiGenerator.FallRate = EditorGUILayout.Slider(
                    "Fall Rate",
                    voronoiGenerator.FallRate,
                    0f, 10f
                );
                
                voronoiGenerator.DropOff = EditorGUILayout.Slider(
                    "Drop Off",
                    voronoiGenerator.DropOff,
                    0f, 10f
                );
                
                voronoiGenerator.MinHeight = EditorGUILayout.Slider(
                    "Min Height",
                    voronoiGenerator.MinHeight,
                    0f, 1f
                );
                
                voronoiGenerator.MaxHeight = EditorGUILayout.Slider(
                    "Max Height",
                    voronoiGenerator.MaxHeight,
                    0f, 1f
                );
                
                voronoiGenerator.Type = (VoronoiGenerator.VoronoiType)EditorGUILayout.EnumPopup(
                    "Type",
                    voronoiGenerator.Type
                );
                
                if (GUILayout.Button("Apply Voronoi"))
                {
                    Undo.RegisterCompleteObjectUndo(terrainManager.terrain.terrainData, "Apply Voronoi");
                    terrainManager.ApplyGenerator(voronoiGenerator);
                }
                
                if (GUILayout.Button("Add to Preset"))
                {
                    presetGenerators.Add(voronoiGenerator.Clone());
                }
            }
        }
        
        private void DrawSmoothing()
        {
            showSmoothing = EditorGUILayout.Foldout(showSmoothing, "Smoothing");
            
            if (showSmoothing)
            {
                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
                
                // Basic smoother
                EditorGUILayout.LabelField("Basic Smoother", EditorStyles.boldLabel);
                
                basicSmoother.Iterations = EditorGUILayout.IntSlider(
                    "Iterations",
                    basicSmoother.Iterations,
                    1, 10
                );
                
                if (GUILayout.Button("Apply Basic Smoothing"))
                {
                    Undo.RegisterCompleteObjectUndo(terrainManager.terrain.terrainData, "Apply Basic Smoothing");
                    terrainManager.ApplySmoother(basicSmoother);
                }
                
                if (GUILayout.Button("Add to Preset"))
                {
                    presetSmoother = basicSmoother.Clone();
                }
                
                EditorGUILayout.Space(10);
                
                // Distance-based smoother
                EditorGUILayout.LabelField("Distance-Based Smoother", EditorStyles.boldLabel);
                
                EditorGUI.BeginDisabledGroup(!terrainManager.DistanceGridCalculated);
                
                distanceBasedSmoother.Iterations = EditorGUILayout.IntSlider(
                    "Iterations",
                    distanceBasedSmoother.Iterations,
                    1, 10
                );
                
                distanceBasedSmoother.BaseSmoothing = EditorGUILayout.Slider(
                    "Base Smoothing",
                    distanceBasedSmoother.BaseSmoothing,
                    0f, 10f
                );
                
                distanceBasedSmoother.DistanceFalloff = EditorGUILayout.Slider(
                    "Distance Falloff",
                    distanceBasedSmoother.DistanceFalloff,
                    0.1f, 5f
                );
                
                if (GUILayout.Button("Apply Distance-Based Smoothing"))
                {
                    Undo.RegisterCompleteObjectUndo(terrainManager.terrain.terrainData, "Apply Distance-Based Smoothing");
                    terrainManager.ApplySmoother(distanceBasedSmoother);
                }
                
                if (GUILayout.Button("Add to Preset"))
                {
                    presetSmoother = distanceBasedSmoother.Clone();
                }
                
                EditorGUILayout.Space(10);
                
                // Adaptive smoother
                EditorGUILayout.LabelField("Adaptive Smoother", EditorStyles.boldLabel);
                
                adaptiveSmoother.Iterations = EditorGUILayout.IntSlider(
                    "Iterations",
                    adaptiveSmoother.Iterations,
                    1, 10
                );
                
                adaptiveSmoother.BaseSmoothing = EditorGUILayout.Slider(
                    "Base Smoothing",
                    adaptiveSmoother.BaseSmoothing,
                    0f, 10f
                );
                
                adaptiveSmoother.DistanceFalloff = EditorGUILayout.Slider(
                    "Distance Falloff",
                    adaptiveSmoother.DistanceFalloff,
                    0.1f, 5f
                );
                
                adaptiveSmoother.DetailPreservation = EditorGUILayout.Slider(
                    "Detail Preservation",
                    adaptiveSmoother.DetailPreservation,
                    0f, 1f
                );
                
                if (GUILayout.Button("Apply Adaptive Smoothing"))
                {
                    Undo.RegisterCompleteObjectUndo(terrainManager.terrain.terrainData, "Apply Adaptive Smoothing");
                    terrainManager.ApplySmoother(adaptiveSmoother);
                }
                
                if (GUILayout.Button("Add to Preset"))
                {
                    presetSmoother = adaptiveSmoother.Clone();
                }
                
                EditorGUI.EndDisabledGroup();
                
                if (!terrainManager.DistanceGridCalculated)
                {
                    EditorGUILayout.HelpBox("Distance-based smoothing requires a distance grid. Please calculate it first.", MessageType.Warning);
                }
            }
        }
        
        private void DrawDistanceGrid()
        {
            showDistanceGrid = EditorGUILayout.Foldout(showDistanceGrid, "Distance Grid");
            
            if (showDistanceGrid)
            {
                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
                
                EditorGUI.BeginDisabledGroup(terrainManager.DistanceGridCalculated);
                if (GUILayout.Button("Calculate Distance Grid"))
                {
                    terrainManager.CalculateDistanceGrid();
                }
                EditorGUI.EndDisabledGroup();
                
                if (terrainManager.DistanceGridCalculated)
                {
                    EditorGUILayout.HelpBox("Distance grid has been calculated and saved.", MessageType.Info);
                    
                    EditorGUILayout.BeginHorizontal();
                    
                    if (GUILayout.Button("Force Recalculate"))
                    {
                        if (EditorUtility.DisplayDialog("Confirm Recalculation",
                                "Are you sure you want to recalculate the distance grid? This will overwrite the existing data.",
                                "Yes, Recalculate", "Cancel"))
                        {
                            terrainManager.CalculateDistanceGrid();
                        }
                    }
                    
                    if (GUILayout.Button("Visualize Distance Grid"))
                    {
                        terrainManager.VisualizeDistanceGrid();
                    }
                    
                    EditorGUILayout.EndHorizontal();
                }
                else
                {
                    EditorGUILayout.HelpBox("Distance grid has not been calculated yet. Click the button above to generate it.", MessageType.Warning);
                }
            }
        }
        
        private void DrawPresets()
        {
            showPresets = EditorGUILayout.Foldout(showPresets, "Saved Presets");
            
            if (showPresets)
            {
                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
                
                if (terrainManager.savedPresets.Count == 0)
                {
                    EditorGUILayout.HelpBox("No presets have been saved yet. Create a preset using the One-Click Generation section.", MessageType.Info);
                }
                else
                {
                    for (int i = 0; i < terrainManager.savedPresets.Count; i++)
                    {
                        TerrainGenerationPreset preset = terrainManager.savedPresets[i];
                        
                        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                        
                        EditorGUILayout.LabelField(preset.Name, EditorStyles.boldLabel);
                        
                        string generatorInfo = preset.Generators != null ? $"Generators: {preset.Generators.Count}" : "Generators: 0";
                        string smootherInfo = preset.Smoother != null ? $"Smoother: {preset.Smoother.Name}" : "No smoother";
                        
                        EditorGUILayout.LabelField(generatorInfo);
                        EditorGUILayout.LabelField(smootherInfo);
                        
                        EditorGUILayout.BeginHorizontal();
                        
                        if (GUILayout.Button("Apply"))
                        {
                            Undo.RegisterCompleteObjectUndo(terrainManager.terrain.terrainData, "Apply Terrain Preset");
                            terrainManager.ApplyPreset(preset);
                        }
                        
                        if (GUILayout.Button("Delete"))
                        {
                            if (EditorUtility.DisplayDialog("Confirm Deletion",
                                    $"Are you sure you want to delete the preset '{preset.Name}'?",
                                    "Yes, Delete", "Cancel"))
                            {
                                Undo.RecordObject(terrainManager, "Delete Terrain Generation Preset");
                                terrainManager.savedPresets.RemoveAt(i);
                                EditorUtility.SetDirty(terrainManager);
                                i--;
                            }
                        }
                        
                        EditorGUILayout.EndHorizontal();
                        
                        EditorGUILayout.EndVertical();
                        
                        EditorGUILayout.Space(5);
                    }
                }
                
                // Add buttons for project operations
                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("Project Presets", EditorStyles.boldLabel);

                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button("Save All Presets to Project"))
                {
                    terrainManager.SavePresetsToProject();
                }

                if (GUILayout.Button("Load Presets from Project"))
                {
                    terrainManager.LoadPresetsFromProject();
                }

                EditorGUILayout.EndHorizontal();

                // Add controls for individual preset project operations
                if (terrainManager.savedPresets.Count > 0)
                {
                    EditorGUILayout.Space(5);
                    EditorGUILayout.LabelField("Per-Preset Operations", EditorStyles.miniBoldLabel);
    
                    // Create array of preset names
                    string[] presetNames = new string[terrainManager.savedPresets.Count];
                    for (int i = 0; i < terrainManager.savedPresets.Count; i++)
                    {
                        presetNames[i] = terrainManager.savedPresets[i].Name;
                    }
                    int selectedPresetIndex = EditorGUILayout.Popup("Select Preset", -1, presetNames);
    
                    if (selectedPresetIndex >= 0)
                    {
                        EditorGUILayout.BeginHorizontal();
        
                        if (GUILayout.Button("Save to Project"))
                        {
                            terrainManager.SavePresetToProject(terrainManager.savedPresets[selectedPresetIndex]);
                        }
        
                        EditorGUILayout.EndHorizontal();
                    }
                }
                // Add this to your DrawPresets() method
                if (GUILayout.Button("Create Default Presets"))
                {
                    if (EditorUtility.DisplayDialog("Create Default Presets",
                            "This will replace all existing presets with default presets. Continue?",
                            "Yes", "Cancel"))
                    {
                        terrainManager.CreateDefaultPresets();
                    }
                }
            }
        }
        
        #endregion
    }
}