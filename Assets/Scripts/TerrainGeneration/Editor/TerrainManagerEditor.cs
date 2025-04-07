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
        private PerlinNoiseGenerator perlinGenerator = new PerlinNoiseGenerator();
        private VoronoiGenerator voronoiGenerator = new VoronoiGenerator();
        private MidpointDisplacementGenerator midpointDisplacementGenerator = new MidpointDisplacementGenerator();
        
        // Smoother instances
        private BasicSmoother basicSmoother = new BasicSmoother();
        private DistanceBasedSmoother distanceBasedSmoother = new DistanceBasedSmoother();
        private AdaptiveSmoother adaptiveSmoother = new AdaptiveSmoother();
        private DirectionalGradientSmoother directionalGradientSmoother = new DirectionalGradientSmoother();
        
        // Erosion instances
        private HydraulicErosion hydraulicErosion = new HydraulicErosion();
        private ThermalErosion thermalErosion = new ThermalErosion();
        
        #endregion

        #region Editor State
        
        private TerrainManager terrainManager;
        
        // Foldout states
        private bool showCommonSettings = true;
        private bool showOneClickGeneration = false;
        private bool showUniformGenerator = false;
        private bool showPerlinGenerator = false;
        private bool showMultiPerlinGenerator = false;
        private bool showVoronoiGenerator = false;
        private bool showMidpointDisplacementGenerator = false;
        private bool showSmoothing = false;
        private bool showErosion = false;
        private bool showDistanceGrid = false;
        private bool showPresets = false;
        
        // For building a generation preset
        private List<ITerrainGenerator> presetGenerators = new List<ITerrainGenerator>();
        private List<ITerrainSmoother> presetSmoothers = new List<ITerrainSmoother>();
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
            DrawPerlinGenerator();
            DrawMultiPerlinGenerator();
            DrawVoronoiGenerator();
            DrawMidpointDisplacementGenerator();
            
            // Smoothing
            DrawSmoothing();
            
            // Erosion
            DrawErosion();
            
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
                
                if (GUILayout.Button("Add Midpoint"))
                {
                    presetGenerators.Add(midpointDisplacementGenerator.Clone());
                }
                
                if (GUILayout.Button("Add Uniform"))
                {
                    presetGenerators.Add(uniformGenerator.Clone());
                }
                
                EditorGUILayout.EndHorizontal();
                
                // Add smoother section
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Smoothers & Erosion", EditorStyles.boldLabel);

                // Show current smoothers in preset
                for (int i = 0; i < presetSmoothers.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"{i + 1}. {presetSmoothers[i].Name}");
    
                    if (GUILayout.Button("Remove", GUILayout.Width(80)))
                    {
                        presetSmoothers.RemoveAt(i);
                        i--;
                    }
    
                    EditorGUILayout.EndHorizontal();
                }

                // Add smoother buttons
                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button("Add Basic Smoother"))
                {
                    presetSmoothers.Add(basicSmoother.Clone());
                }

                if (GUILayout.Button("Add Distance Smoother"))
                {
                    presetSmoothers.Add(distanceBasedSmoother.Clone());
                }

                if (GUILayout.Button("Add Adaptive Smoother"))
                {
                    presetSmoothers.Add(adaptiveSmoother.Clone());
                }

                if (GUILayout.Button("Add Gradient Smoother"))
                {
                    presetSmoothers.Add(directionalGradientSmoother.Clone());
                }

                EditorGUILayout.EndHorizontal();
                
                // Add erosion buttons
                EditorGUILayout.BeginHorizontal();
                
                if (GUILayout.Button("Add Hydraulic Erosion"))
                {
                    presetSmoothers.Add(hydraulicErosion.Clone());
                }
                
                if (GUILayout.Button("Add Thermal Erosion"))
                {
                    presetSmoothers.Add(thermalErosion.Clone());
                }
                
                EditorGUILayout.EndHorizontal();
                
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
                        preset.Smoothers.AddRange(presetSmoothers); // Add all smoothers to the preset
        
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
                        preset.Smoothers.AddRange(presetSmoothers); // Add all smoothers to the preset
        
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
                
                uniformGenerator.NormalizeToMinimum = EditorGUILayout.Toggle(
                    "Normalize to Minimum Height",
                    uniformGenerator.NormalizeToMinimum
                );

                if (uniformGenerator.NormalizeToMinimum)
                {
                    EditorGUILayout.HelpBox(
                        "This will offset all modifiable terrain to bring the minimum height to zero. " +
                        "The uniform increment setting will be ignored.", 
                        MessageType.Info
                    );
                }
        
                EditorGUILayout.BeginHorizontal();
        
                if (GUILayout.Button("Apply Uniform Height"))
                {
                    // Register the operation for undo
                    Undo.RegisterCompleteObjectUndo(terrainManager.terrain.terrainData, "Apply Uniform Height");
                    terrainManager.ApplyGenerator(uniformGenerator);
                }
        
                if (GUILayout.Button("Add to Preset"))
                {
                    presetGenerators.Add(uniformGenerator.Clone());
                }
        
                EditorGUILayout.EndHorizontal();
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
        
        private void DrawMidpointDisplacementGenerator()
{
    showMidpointDisplacementGenerator = EditorGUILayout.Foldout(showMidpointDisplacementGenerator, "Midpoint Displacement");

    if (showMidpointDisplacementGenerator)
    {
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

        // Midpoint displacement parameters
        midpointDisplacementGenerator.MinHeight = EditorGUILayout.FloatField(
            "Min Height", 
            midpointDisplacementGenerator.MinHeight
        );

        midpointDisplacementGenerator.MaxHeight = EditorGUILayout.FloatField(
            "Max Height", 
            midpointDisplacementGenerator.MaxHeight
        );

        midpointDisplacementGenerator.Roughness = EditorGUILayout.Slider(
            "Roughness", 
            midpointDisplacementGenerator.Roughness, 
            0.1f, 1.0f
        );

        midpointDisplacementGenerator.InitialRandomRange = EditorGUILayout.Slider(
            "Initial Random Range", 
            midpointDisplacementGenerator.InitialRandomRange, 
            0.0f, 1.0f
        );

        midpointDisplacementGenerator.NormalizeResult = EditorGUILayout.Toggle(
            "Normalize Result",
            midpointDisplacementGenerator.NormalizeResult
        );

        midpointDisplacementGenerator.UseAbsoluteRandom = EditorGUILayout.Toggle(
            "Use Absolute Random",
            midpointDisplacementGenerator.UseAbsoluteRandom
        );

        midpointDisplacementGenerator.Seed = EditorGUILayout.IntField(
            "Random Seed", 
            midpointDisplacementGenerator.Seed
        );

        if (GUILayout.Button("Apply Midpoint Displacement"))
        {
            Undo.RegisterCompleteObjectUndo(terrainManager.terrain.terrainData, "Apply Midpoint Displacement");
            terrainManager.ApplyGenerator(midpointDisplacementGenerator);
        }

        if (GUILayout.Button("Add to Preset"))
        {
            presetGenerators.Add(midpointDisplacementGenerator.Clone());
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
                    presetSmoothers.Add(basicSmoother.Clone());
                }
                
                EditorGUILayout.Space(10);
                
                // Distance-based smoother
                EditorGUILayout.LabelField("Distance-Based Smoother", EditorStyles.boldLabel);
                
                EditorGUI.BeginDisabledGroup(!terrainManager.DistanceGridCalculated);
                
                distanceBasedSmoother.Iterations = EditorGUILayout.IntSlider(
                    "Iterations",
                    distanceBasedSmoother.Iterations,
                    1, 100
                );
                
                distanceBasedSmoother.BaseSmoothing = EditorGUILayout.Slider(
                    "Base Smoothing",
                    distanceBasedSmoother.BaseSmoothing,
                    0f, 100f
                );
                
                distanceBasedSmoother.DistanceFalloff = EditorGUILayout.Slider(
                    "Distance Falloff",
                    distanceBasedSmoother.DistanceFalloff,
                    0.1f, 100f
                );
                
                if (GUILayout.Button("Apply Distance-Based Smoothing"))
                {
                    Undo.RegisterCompleteObjectUndo(terrainManager.terrain.terrainData, "Apply Distance-Based Smoothing");
                    terrainManager.ApplySmoother(distanceBasedSmoother);
                }
                
                if (GUILayout.Button("Add to Preset"))
                {
                    presetSmoothers.Add(distanceBasedSmoother.Clone());
                }
                
                EditorGUILayout.Space(10);
                
                // Distance-based smoother
                EditorGUILayout.LabelField("Gradient-Based Smoother", EditorStyles.boldLabel);
                
                EditorGUI.BeginDisabledGroup(!terrainManager.DistanceGridCalculated);
                
                directionalGradientSmoother.SearchRadius = EditorGUILayout.FloatField("Search Radius", directionalGradientSmoother.SearchRadius);
                directionalGradientSmoother.AdjustmentStrength = EditorGUILayout.Slider("Adjustment Strength", directionalGradientSmoother.AdjustmentStrength, 0f, 1f);
                directionalGradientSmoother.DirectionInfluence = EditorGUILayout.Slider("Direction Influence", directionalGradientSmoother.DirectionInfluence, 0f, 1f);
                directionalGradientSmoother.DetailPreservation = EditorGUILayout.Slider("Detail Preservation", directionalGradientSmoother.DetailPreservation, 0f, 1f);
        
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel("Falloff Type");
                directionalGradientSmoother.DistanceFalloff = (DirectionalGradientSmoother.FalloffType)EditorGUILayout.EnumPopup(directionalGradientSmoother.DistanceFalloff);
                EditorGUILayout.EndHorizontal();
                
                
                if (GUILayout.Button("Apply Directional Gradient-Based Smoothing"))
                {
                    Undo.RegisterCompleteObjectUndo(terrainManager.terrain.terrainData, "Apply Gradient-Based Smoothing");
                    terrainManager.ApplySmoother(directionalGradientSmoother);
                }
                
                if (GUILayout.Button("Add to Preset"))
                {
                    presetSmoothers.Add(directionalGradientSmoother.Clone());
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
                    presetSmoothers.Add(adaptiveSmoother.Clone());
                }
                
                EditorGUI.EndDisabledGroup();
                
                if (!terrainManager.DistanceGridCalculated)
                {
                    EditorGUILayout.HelpBox("Distance-based smoothing requires a distance grid. Please calculate it first.", MessageType.Warning);
                }
            }
        }
        
        private void DrawErosion()
        {
            showErosion = EditorGUILayout.Foldout(showErosion, "Erosion");
            
            if (showErosion)
            {
                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
                
                // Hydraulic erosion
                EditorGUILayout.LabelField("Hydraulic Erosion", EditorStyles.boldLabel);
                
                EditorGUI.BeginDisabledGroup(!terrainManager.DistanceGridCalculated);
                
                EditorGUILayout.HelpBox("Simulates water flow over the terrain, creating valleys and ridges while respecting road constraints.", MessageType.Info);
                
                EditorGUILayout.LabelField("Basic Parameters", EditorStyles.boldLabel);
                
                hydraulicErosion.DropletCount = EditorGUILayout.IntSlider(
                    "Droplet Count",
                    hydraulicErosion.DropletCount,
                    1000, 100000
                );
                
                hydraulicErosion.MaxDropletLifetime = EditorGUILayout.IntSlider(
                    "Max Droplet Lifetime",
                    hydraulicErosion.MaxDropletLifetime,
                    5, 100
                );
                
                hydraulicErosion.InitialWaterVolume = EditorGUILayout.Slider(
                    "Initial Water Volume",
                    hydraulicErosion.InitialWaterVolume,
                    0.1f, 5.0f
                );
                
                EditorGUILayout.LabelField("Erosion Physics", EditorStyles.boldLabel);
                
                hydraulicErosion.Inertia = EditorGUILayout.Slider(
                    "Inertia",
                    hydraulicErosion.Inertia,
                    0.0f, 1.0f
                );
                
                hydraulicErosion.SedimentCapacityFactor = EditorGUILayout.Slider(
                    "Sediment Capacity",
                    hydraulicErosion.SedimentCapacityFactor,
                    0.1f, 10.0f
                );
                
                hydraulicErosion.ErodeSpeed = EditorGUILayout.Slider(
                    "Erosion Speed",
                    hydraulicErosion.ErodeSpeed,
                    0.0f, 1.0f
                );
                
                hydraulicErosion.DepositSpeed = EditorGUILayout.Slider(
                    "Deposit Speed",
                    hydraulicErosion.DepositSpeed,
                    0.0f, 1.0f
                );
                
                hydraulicErosion.EvaporationRate = EditorGUILayout.Slider(
                    "Evaporation Rate",
                    hydraulicErosion.EvaporationRate,
                    0.0f, 0.1f
                );
                
                EditorGUILayout.LabelField("Brush Settings", EditorStyles.boldLabel);
                
                hydraulicErosion.ErosionRadius = EditorGUILayout.IntSlider(
                    "Erosion Radius",
                    hydraulicErosion.ErosionRadius,
                    1, 10
                );
                
                hydraulicErosion.ErosionFalloff = EditorGUILayout.Slider(
                    "Erosion Falloff",
                    hydraulicErosion.ErosionFalloff,
                    0.1f, 2.0f
                );
                
                EditorGUILayout.LabelField("Road Integration", EditorStyles.boldLabel);
                
                hydraulicErosion.MaxErosionDepth = EditorGUILayout.Slider(
                    "Max Erosion Depth",
                    hydraulicErosion.MaxErosionDepth,
                    0.01f, 0.5f
                );
                
                hydraulicErosion.RoadInfluenceMultiplier = EditorGUILayout.Slider(
                    "Road Influence Multiplier",
                    hydraulicErosion.RoadInfluenceMultiplier,
                    0.0f, 1.0f
                );
                
                hydraulicErosion.RoadInfluenceDistance = EditorGUILayout.Slider(
                    "Road Influence Distance",
                    hydraulicErosion.RoadInfluenceDistance,
                    0.05f, 1.0f
                );
                
                if (GUILayout.Button("Apply Hydraulic Erosion"))
                {
                    Undo.RegisterCompleteObjectUndo(terrainManager.terrain.terrainData, "Apply Hydraulic Erosion");
                    terrainManager.ApplySmoother(hydraulicErosion);
                }
                
                if (GUILayout.Button("Add to Preset"))
                {
                    presetSmoothers.Add(hydraulicErosion.Clone());
                }
                
                EditorGUILayout.Space(10);
                
                // Thermal erosion
                EditorGUILayout.LabelField("Thermal Erosion", EditorStyles.boldLabel);
                
                EditorGUILayout.HelpBox("Simulates material slumping on steep slopes to create more natural terrain gradients.", MessageType.Info);
                
                thermalErosion.Iterations = EditorGUILayout.IntSlider(
                    "Iterations",
                    thermalErosion.Iterations,
                    1, 20
                );
                
                thermalErosion.Talus = EditorGUILayout.Slider(
                    "Maximum Stable Slope",
                    thermalErosion.Talus,
                    0.01f, 2.0f
                );
                
                thermalErosion.ErosionRate = EditorGUILayout.Slider(
                    "Erosion Rate",
                    thermalErosion.ErosionRate,
                    0.0f, 1.0f
                );
                
                thermalErosion.RespectRoadSlopes = EditorGUILayout.Toggle(
                    "Respect Road Slopes",
                    thermalErosion.RespectRoadSlopes
                );
                
                if (thermalErosion.RespectRoadSlopes)
                {
                    thermalErosion.RoadInfluenceDistance = EditorGUILayout.Slider(
                        "Road Influence Distance",
                        thermalErosion.RoadInfluenceDistance,
                        0.05f, 1.0f
                    );
                    
                    thermalErosion.RoadSlopeFactor = EditorGUILayout.Slider(
                        "Road Slope Factor",
                        thermalErosion.RoadSlopeFactor,
                        0.0f, 1.0f
                    );
                }
                
                if (GUILayout.Button("Apply Thermal Erosion"))
                {
                    Undo.RegisterCompleteObjectUndo(terrainManager.terrain.terrainData, "Apply Thermal Erosion");
                    terrainManager.ApplySmoother(thermalErosion);
                }
                
                if (GUILayout.Button("Add to Preset"))
                {
                    presetSmoothers.Add(thermalErosion.Clone());
                }
                
                EditorGUI.EndDisabledGroup();
                
                if (!terrainManager.DistanceGridCalculated)
                {
                    EditorGUILayout.HelpBox("Erosion requires a distance grid. Please calculate it first.", MessageType.Warning);
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
                        string smootherInfo = preset.Smoothers != null && preset.Smoothers.Count > 0 ? 
                            $"Smoothers: {preset.Smoothers.Count}" : "No smoothers";
                        
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
            }
        }
        
        #endregion
    }
}