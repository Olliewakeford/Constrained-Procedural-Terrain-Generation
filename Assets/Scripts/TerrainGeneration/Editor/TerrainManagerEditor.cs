using System.Collections.Generic;
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
        private SerializedProperty _terrainProp;
        private SerializedProperty _maskProp;
        private SerializedProperty _restoreTerrainProp;
        
        // Generator instances - We'll keep these as regular objects in the editor
        // since we're creating and modifying them only through the editor UI
        private readonly UniformHeightGenerator _uniformGenerator = new();
        private readonly PerlinNoiseGenerator _perlinGenerator = new();
        private readonly VoronoiGenerator _voronoiGenerator = new();
        private readonly MidpointDisplacementGenerator _midpointDisplacementGenerator = new();
        
        // Smoother instances
        private readonly BasicSmoother _basicSmoother = new();
        private readonly EnhancedDistanceSmoother _enhancedDistanceSmoother = new();
        
        // Erosion instances
        private readonly HydraulicErosion _hydraulicErosion = new();
        private readonly ThermalErosion _thermalErosion = new();
        
        #endregion

        #region Editor State
        
        private TerrainManager _terrainManager;
        
        // Foldout states
        private bool _showCommonSettings = true;
        private bool _showOneClickGeneration;
        private bool _showUniformGenerator;
        private bool _showPerlinGenerator;
        private bool _showMultiPerlinGenerator;
        private bool _showVoronoiGenerator;
        private bool _showMidpointDisplacementGenerator;
        private bool _showSmoothing;
        private bool _showErosion;
        private bool _showDistanceGrid;
        private bool _showPresets;
        
        // Enhanced smoother configuration foldouts
        private bool _showEnhancedBasicSettings = true;
        private bool _showEnhancedDistanceSettings = true;
        private bool _showEnhancedDetailSettings = true;
        private bool _showEnhancedBlendingSettings = true;
        
        // For building a generation preset
        private readonly List<ITerrainGenerator> _presetGenerators = new();
        private readonly List<ITerrainSmoother> _presetSmoothers = new();
        private string _presetName = "New Preset";
        
        // For multi-Perlin
        private readonly List<PerlinNoiseGenerator> _perlinLayers = new()
        {
            new PerlinNoiseGenerator()
        };
        private readonly List<bool> _perlinLayersRemove = new() { false };
        
        #endregion

        #region Unity Methods
        
        private void OnEnable()
        {
            // Get a reference to the TerrainManager
            _terrainManager = (TerrainManager)target;
            
            // Find serialized properties for common settings
            _terrainProp = serializedObject.FindProperty("terrain");
            _maskProp = serializedObject.FindProperty("mask");
            _restoreTerrainProp = serializedObject.FindProperty("restoreTerrain");
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
                _terrainManager.RestoreTerrain();
            }
            
            // Apply modifications to the serializedObject
            serializedObject.ApplyModifiedProperties();
        }
        
        #endregion

        #region Drawing Methods
        
        private void DrawCommonSettings()
        {
            _showCommonSettings = EditorGUILayout.Foldout(_showCommonSettings, "Common Settings");

            if (!_showCommonSettings) return;
            EditorGUILayout.PropertyField(_terrainProp, new GUIContent("Terrain"));
            EditorGUILayout.PropertyField(_maskProp, new GUIContent("Mask Texture"));
            EditorGUILayout.PropertyField(_restoreTerrainProp, new GUIContent("Reset Before Generating"));
        }
        
        private void DrawOneClickGeneration()
        {
            _showOneClickGeneration = EditorGUILayout.Foldout(_showOneClickGeneration, "One-Click Generation");

            if (!_showOneClickGeneration) return;
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
                
            EditorGUILayout.LabelField("Build a Generation Preset", EditorStyles.boldLabel);
                
            // Preset Name
            _presetName = EditorGUILayout.TextField("Preset Name", _presetName);
                
            // Add generators
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Generators", EditorStyles.boldLabel);
                
            // Show current generators in preset
            for (int i = 0; i < _presetGenerators.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"{i + 1}. {_presetGenerators[i].Name}");
                    
                if (GUILayout.Button("Remove", GUILayout.Width(80)))
                {
                    _presetGenerators.RemoveAt(i);
                    i--;
                }
                    
                EditorGUILayout.EndHorizontal();
            }
                
            // Add generator buttons
            if (GUILayout.Button("Add Perlin"))
            {
                _presetGenerators.Add(_perlinGenerator.Clone());
            }
                
            if (GUILayout.Button("Add Voronoi"))
            {
                _presetGenerators.Add(_voronoiGenerator.Clone());
            }
                
            if (GUILayout.Button("Add Midpoint"))
            {
                _presetGenerators.Add(_midpointDisplacementGenerator.Clone());
            }
                
            if (GUILayout.Button("Add Uniform"))
            {
                _presetGenerators.Add(_uniformGenerator.Clone());
            }
                
            // Add smoother section
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Smoothers & Erosion", EditorStyles.boldLabel);

            // Show current smoothers in preset
            for (int i = 0; i < _presetSmoothers.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"{i + 1}. {_presetSmoothers[i].Name}");
    
                if (GUILayout.Button("Remove", GUILayout.Width(80)))
                {
                    _presetSmoothers.RemoveAt(i);
                    i--;
                }
    
                EditorGUILayout.EndHorizontal();
            }

            // Add smoother buttons
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Add Basic Smoother"))
            {
                _presetSmoothers.Add(_basicSmoother.Clone());
            }

            EditorGUILayout.EndHorizontal();
                
            // Add erosion buttons
            EditorGUILayout.BeginHorizontal();
                
            if (GUILayout.Button("Add Hydraulic Erosion"))
            {
                _presetSmoothers.Add(_hydraulicErosion.Clone());
            }
                
            if (GUILayout.Button("Add Thermal Erosion"))
            {
                _presetSmoothers.Add(_thermalErosion.Clone());
            }
                
            EditorGUILayout.EndHorizontal();
                
            // Save and generate buttons
            EditorGUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();
                
            if (GUILayout.Button("Save Preset"))
            {
                if (_presetGenerators.Count == 0)
                {
                    EditorUtility.DisplayDialog("Error", "You must add at least one generator to save a preset.", "OK");
                }
                else
                {
                    TerrainGenerationPreset preset = new TerrainGenerationPreset(_presetName);
                    preset.Generators.AddRange(_presetGenerators);  // Add all generators to the preset
                    preset.Smoothers.AddRange(_presetSmoothers); // Add all smoothers to the preset
        
                    // Add the preset to the list
                    Undo.RecordObject(_terrainManager, "Save Terrain Generation Preset");
                    _terrainManager.savedPresets.Add(preset);
                    EditorUtility.SetDirty(_terrainManager);
        
                    EditorUtility.DisplayDialog("Success", $"Preset '{_presetName}' saved successfully.", "OK");
                }
            }
                
            if (GUILayout.Button("Generate"))
            {
                if (_presetGenerators.Count == 0)
                {
                    EditorUtility.DisplayDialog("Error", "You must add at least one generator to generate terrain.", "OK");
                }
                else
                {
                    TerrainGenerationPreset preset = new TerrainGenerationPreset(_presetName);
                    preset.Generators.AddRange(_presetGenerators); 
                    preset.Smoothers.AddRange(_presetSmoothers);
        
                    // Register this action for undo
                    Undo.RegisterCompleteObjectUndo(_terrainManager.terrain.terrainData, "Generate Terrain");
                    _terrainManager.ApplyPreset(preset);
                }
            }
                
            EditorGUILayout.EndHorizontal();
        }
        
        private void DrawUniformGenerator()
        {
            _showUniformGenerator = EditorGUILayout.Foldout(_showUniformGenerator, "Uniform Height Change");

            if (!_showUniformGenerator) return;
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        
            // Uniform height parameters
            _uniformGenerator.UniformStep = EditorGUILayout.Slider(
                "Uniform Increment",
                _uniformGenerator.UniformStep,
                -1.0f, 1.0f
            );
                
            _uniformGenerator.NormalizeToMinimum = EditorGUILayout.Toggle(
                "Normalize to Minimum Height",
                _uniformGenerator.NormalizeToMinimum
            );

            if (_uniformGenerator.NormalizeToMinimum)
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
                Undo.RegisterCompleteObjectUndo(_terrainManager.terrain.terrainData, "Apply Uniform Height");
                _terrainManager.ApplyGenerator(_uniformGenerator);
            }
        
            if (GUILayout.Button("Add to Preset"))
            {
                _presetGenerators.Add(_uniformGenerator.Clone());
            }
        
            EditorGUILayout.EndHorizontal();
        }
        
        private void DrawPerlinGenerator()
        {
            _showPerlinGenerator = EditorGUILayout.Foldout(_showPerlinGenerator, "Perlin Noise");

            if (!_showPerlinGenerator) return;
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
                
            // Perlin noise parameters
            _perlinGenerator.XFrequency = EditorGUILayout.Slider(
                "X Frequency",
                _perlinGenerator.XFrequency,
                0f, 0.1f
            );
                
            _perlinGenerator.YFrequency = EditorGUILayout.Slider(
                "Y Frequency",
                _perlinGenerator.YFrequency,
                0f, 0.1f
            );
                
            _perlinGenerator.XOffset = EditorGUILayout.IntSlider(
                "X Offset",
                _perlinGenerator.XOffset,
                0, 10000
            );
                
            _perlinGenerator.YOffset = EditorGUILayout.IntSlider(
                "Y Offset",
                _perlinGenerator.YOffset,
                0, 10000
            );
                
            _perlinGenerator.Octaves = EditorGUILayout.IntSlider(
                "Octaves",
                _perlinGenerator.Octaves,
                1, 10
            );
                
            _perlinGenerator.Persistence = EditorGUILayout.Slider(
                "Persistence",
                _perlinGenerator.Persistence,
                0.1f, 10f
            );
                
            _perlinGenerator.Amplitude = EditorGUILayout.Slider(
                "Amplitude",
                _perlinGenerator.Amplitude,
                0f, 1f
            );
                
            if (GUILayout.Button("Apply Perlin Noise"))
            {
                Undo.RegisterCompleteObjectUndo(_terrainManager.terrain.terrainData, "Apply Perlin Noise");
                _terrainManager.ApplyGenerator(_perlinGenerator);
            }
                
            if (GUILayout.Button("Add to Preset"))
            {
                _presetGenerators.Add(_perlinGenerator.Clone());
            }
        }
        
        private void DrawMultiPerlinGenerator()
        {
            _showMultiPerlinGenerator = EditorGUILayout.Foldout(_showMultiPerlinGenerator, "Multiple Perlin Layers");

            if (!_showMultiPerlinGenerator) return;
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
                
            // Draw each layer
            for (int i = 0; i < _perlinLayers.Count; i++)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    
                EditorGUILayout.LabelField($"Layer {i + 1}", EditorStyles.boldLabel);
                    
                _perlinLayers[i].XFrequency = EditorGUILayout.Slider(
                    "X Frequency", 
                    _perlinLayers[i].XFrequency, 
                    0f, 0.1f
                );
                    
                _perlinLayers[i].YFrequency = EditorGUILayout.Slider(
                    "Y Frequency", 
                    _perlinLayers[i].YFrequency, 
                    0f, 0.1f
                );
                    
                _perlinLayers[i].Octaves = EditorGUILayout.IntSlider(
                    "Octaves",
                    _perlinLayers[i].Octaves,
                    1, 10
                );
                    
                _perlinLayers[i].Persistence = EditorGUILayout.Slider(
                    "Persistence",
                    _perlinLayers[i].Persistence,
                    0.1f, 10f
                );
                    
                _perlinLayers[i].Amplitude = EditorGUILayout.Slider(
                    "Amplitude",
                    _perlinLayers[i].Amplitude,
                    0f, 1f
                );
                    
                _perlinLayersRemove[i] = EditorGUILayout.Toggle("Remove", _perlinLayersRemove[i]);
                    
                EditorGUILayout.EndVertical();
                    
                EditorGUILayout.Space(5);
            }
                
            // Add/remove layer buttons
            EditorGUILayout.BeginHorizontal();
                
            if (GUILayout.Button("+"))
            {
                _perlinLayers.Add(new PerlinNoiseGenerator());
                _perlinLayersRemove.Add(false);
            }
                
            if (GUILayout.Button("-"))
            {
                // Remove marked layers
                for (int i = _perlinLayers.Count - 1; i >= 0; i--)
                {
                    if (_perlinLayersRemove[i])
                    {
                        _perlinLayers.RemoveAt(i);
                        _perlinLayersRemove.RemoveAt(i);
                    }
                }
                    
                // Ensure we have at least one layer
                if (_perlinLayers.Count == 0)
                {
                    _perlinLayers.Add(new PerlinNoiseGenerator());
                    _perlinLayersRemove.Add(false);
                }
            }
                
            EditorGUILayout.EndHorizontal();
                
            // Apply button
            if (GUILayout.Button("Apply Multiple Perlin Layers"))
            {
                Undo.RegisterCompleteObjectUndo(_terrainManager.terrain.terrainData, "Apply Multiple Perlin Layers");
                    
                bool wasRestore = _terrainManager.restoreTerrain;
                    
                // Apply first layer
                _terrainManager.ApplyGenerator(_perlinLayers[0]);
                    
                // Apply subsequent layers without restoring
                _terrainManager.restoreTerrain = false;
                for (int i = 1; i < _perlinLayers.Count; i++)
                {
                    _terrainManager.ApplyGenerator(_perlinLayers[i]);
                }
                    
                _terrainManager.restoreTerrain = wasRestore;
            }

            if (!GUILayout.Button("Add All Layers to Preset")) return;
            foreach (var layer in _perlinLayers)
            {
                _presetGenerators.Add(layer.Clone());
            }
        }
        
        private void DrawVoronoiGenerator()
        {
            _showVoronoiGenerator = EditorGUILayout.Foldout(_showVoronoiGenerator, "Voronoi Tessellation");

            if (!_showVoronoiGenerator) return;
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
                
            // Voronoi parameters
            _voronoiGenerator.PeakCount = EditorGUILayout.IntSlider(
                "Peak Count",
                _voronoiGenerator.PeakCount,
                1, 20
            );
                
            _voronoiGenerator.FallRate = EditorGUILayout.Slider(
                "Fall Rate",
                _voronoiGenerator.FallRate,
                0f, 10f
            );
                
            _voronoiGenerator.DropOff = EditorGUILayout.Slider(
                "Drop Off",
                _voronoiGenerator.DropOff,
                0f, 10f
            );
                
            _voronoiGenerator.MinHeight = EditorGUILayout.Slider(
                "Min Height",
                _voronoiGenerator.MinHeight,
                0f, 1f
            );
                
            _voronoiGenerator.MaxHeight = EditorGUILayout.Slider(
                "Max Height",
                _voronoiGenerator.MaxHeight,
                0f, 1f
            );
                
            _voronoiGenerator.Type = (VoronoiGenerator.VoronoiType)EditorGUILayout.EnumPopup(
                "Type",
                _voronoiGenerator.Type
            );
                
            if (GUILayout.Button("Apply Voronoi"))
            {
                Undo.RegisterCompleteObjectUndo(_terrainManager.terrain.terrainData, "Apply Voronoi");
                _terrainManager.ApplyGenerator(_voronoiGenerator);
            }
                
            if (GUILayout.Button("Add to Preset"))
            {
                _presetGenerators.Add(_voronoiGenerator.Clone());
            }
        }
        
        private void DrawMidpointDisplacementGenerator()
        {
            _showMidpointDisplacementGenerator = EditorGUILayout.Foldout(_showMidpointDisplacementGenerator, "Midpoint Displacement");

            if (!_showMidpointDisplacementGenerator) return;
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            // Midpoint displacement parameters
            _midpointDisplacementGenerator.MinHeight = EditorGUILayout.FloatField(
                "Min Height", 
                _midpointDisplacementGenerator.MinHeight
            );

            _midpointDisplacementGenerator.MaxHeight = EditorGUILayout.FloatField(
                "Max Height", 
                _midpointDisplacementGenerator.MaxHeight
            );

            _midpointDisplacementGenerator.Roughness = EditorGUILayout.Slider(
                "Roughness", 
                _midpointDisplacementGenerator.Roughness, 
                0.1f, 1.0f
            );

            _midpointDisplacementGenerator.InitialRandomRange = EditorGUILayout.Slider(
                "Initial Random Range", 
                _midpointDisplacementGenerator.InitialRandomRange, 
                0.0f, 1.0f
            );

            _midpointDisplacementGenerator.NormalizeResult = EditorGUILayout.Toggle(
                "Normalize Result",
                _midpointDisplacementGenerator.NormalizeResult
            );

            _midpointDisplacementGenerator.UseAbsoluteRandom = EditorGUILayout.Toggle(
                "Use Absolute Random",
                _midpointDisplacementGenerator.UseAbsoluteRandom
            );

            _midpointDisplacementGenerator.Seed = EditorGUILayout.IntField(
                "Random Seed", 
                _midpointDisplacementGenerator.Seed
            );

            if (GUILayout.Button("Apply Midpoint Displacement"))
            {
                Undo.RegisterCompleteObjectUndo(_terrainManager.terrain.terrainData, "Apply Midpoint Displacement");
                _terrainManager.ApplyGenerator(_midpointDisplacementGenerator);
            }

            if (GUILayout.Button("Add to Preset"))
            {
                _presetGenerators.Add(_midpointDisplacementGenerator.Clone());
            }
        }
        
        private void DrawSmoothing()
        {
            _showSmoothing = EditorGUILayout.Foldout(_showSmoothing, "Smoothing");

            if (!_showSmoothing) return;
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
                
            // Basic smoother
            EditorGUILayout.LabelField("Basic Smoother", EditorStyles.boldLabel);
                
            _basicSmoother.Iterations = EditorGUILayout.IntSlider(
                "Iterations",
                _basicSmoother.Iterations,
                1, 10
            );
                
            if (GUILayout.Button("Apply Basic Smoothing"))
            {
                Undo.RegisterCompleteObjectUndo(_terrainManager.terrain.terrainData, "Apply Basic Smoothing");
                _terrainManager.ApplySmoother(_basicSmoother);
            }
                
            if (GUILayout.Button("Add to Preset"))
            {
                _presetSmoothers.Add(_basicSmoother.Clone());
            }
                
            EditorGUILayout.Space(10);
                
            // Enhanced Distance Smoother (replaces both Distance-Based and Adaptive smoothers)
            EditorGUILayout.LabelField("Enhanced Distance Smoother", EditorStyles.boldLabel);
                
            EditorGUI.BeginDisabledGroup(!_terrainManager.DistanceGridCalculated);
                
            // Basic Settings
            _showEnhancedBasicSettings = EditorGUILayout.Foldout(_showEnhancedBasicSettings, "Basic Settings", true);
            if (_showEnhancedBasicSettings)
            {
                _enhancedDistanceSmoother.Iterations = EditorGUILayout.IntSlider(
                    "Iterations",
                    _enhancedDistanceSmoother.Iterations,
                    1, 100
                );
                    
                _enhancedDistanceSmoother.BaseSmoothing = EditorGUILayout.Slider(
                    "Base Smoothing",
                    _enhancedDistanceSmoother.BaseSmoothing,
                    0f, 10f
                );
            }
                
            // Distance-Based Settings
            _showEnhancedDistanceSettings = EditorGUILayout.Foldout(_showEnhancedDistanceSettings, "Distance-Based Settings", true);
            if (_showEnhancedDistanceSettings)
            {
                _enhancedDistanceSmoother.DistanceFalloff = EditorGUILayout.Slider(
                    "Distance Falloff",
                    _enhancedDistanceSmoother.DistanceFalloff,
                    0.1f, 5f
                );
                    
                _enhancedDistanceSmoother.UseDistanceThreshold = EditorGUILayout.Toggle(
                    "Use Distance Threshold",
                    _enhancedDistanceSmoother.UseDistanceThreshold
                );
                    
                if (_enhancedDistanceSmoother.UseDistanceThreshold)
                {
                    _enhancedDistanceSmoother.DistanceThreshold = EditorGUILayout.Slider(
                        "Distance Threshold",
                        _enhancedDistanceSmoother.DistanceThreshold,
                        0.01f, 0.99f
                    );
                }
                    
                _enhancedDistanceSmoother.RoadProximityWeight = EditorGUILayout.Slider(
                    "Road Proximity Weight",
                    _enhancedDistanceSmoother.RoadProximityWeight,
                    1f, 10f
                );
            }
                
            // Detail Preservation Settings
            _showEnhancedDetailSettings = EditorGUILayout.Foldout(_showEnhancedDetailSettings, "Detail Preservation", true);
            if (_showEnhancedDetailSettings)
            {
                _enhancedDistanceSmoother.PreserveDetail = EditorGUILayout.Toggle(
                    "Preserve Detail",
                    _enhancedDistanceSmoother.PreserveDetail
                );
                    
                if (_enhancedDistanceSmoother.PreserveDetail)
                {
                    _enhancedDistanceSmoother.DetailPreservation = EditorGUILayout.Slider(
                        "Detail Preservation",
                        _enhancedDistanceSmoother.DetailPreservation,
                        0f, 1f
                    );
                        
                    _enhancedDistanceSmoother.MinSmoothingFactor = EditorGUILayout.Slider(
                        "Min Smoothing Factor",
                        _enhancedDistanceSmoother.MinSmoothingFactor,
                        0.01f, 0.5f
                    );
                }
            }
                
            // Blending Options
            _showEnhancedBlendingSettings = EditorGUILayout.Foldout(_showEnhancedBlendingSettings, "Blending Options", true);
            if (_showEnhancedBlendingSettings)
            {
                _enhancedDistanceSmoother.UseLinearBlending = EditorGUILayout.Toggle(
                    "Use Linear Blending",
                    _enhancedDistanceSmoother.UseLinearBlending
                );
                    
                if (_enhancedDistanceSmoother.UseLinearBlending)
                {
                    _enhancedDistanceSmoother.MinBlendFactor = EditorGUILayout.Slider(
                        "Min Blend Factor",
                        _enhancedDistanceSmoother.MinBlendFactor,
                        0.25f, 1f
                    );
                }
            }
                
            if (GUILayout.Button("Apply Enhanced Distance Smoothing"))
            {
                Undo.RegisterCompleteObjectUndo(_terrainManager.terrain.terrainData, "Apply Enhanced Distance Smoothing");
                _terrainManager.ApplySmoother(_enhancedDistanceSmoother);
            }
                
            if (GUILayout.Button("Add to Preset"))
            {
                _presetSmoothers.Add(_enhancedDistanceSmoother.Clone());
            }
                
            EditorGUILayout.Space(10);
                
            EditorGUI.EndDisabledGroup();
                
            if (!_terrainManager.DistanceGridCalculated)
            {
                EditorGUILayout.HelpBox("Distance-based smoothing requires a distance grid. Please calculate it first.", MessageType.Warning);
            }
        }
        
        private void DrawErosion()
        {
            _showErosion = EditorGUILayout.Foldout(_showErosion, "Erosion");

            if (!_showErosion) return;
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
                
            // Hydraulic erosion
            EditorGUILayout.LabelField("Hydraulic Erosion", EditorStyles.boldLabel);
                
            EditorGUI.BeginDisabledGroup(!_terrainManager.DistanceGridCalculated);
                
            EditorGUILayout.HelpBox("Simulates water flow over the terrain, creating valleys and ridges while respecting road constraints.", MessageType.Info);
                
            EditorGUILayout.LabelField("Basic Parameters", EditorStyles.boldLabel);
                
            _hydraulicErosion.DropletCount = EditorGUILayout.IntSlider(
                "Droplet Count",
                _hydraulicErosion.DropletCount,
                1000, 100000
            );
                
            _hydraulicErosion.MaxDropletLifetime = EditorGUILayout.IntSlider(
                "Max Droplet Lifetime",
                _hydraulicErosion.MaxDropletLifetime,
                5, 100
            );
                
            _hydraulicErosion.InitialWaterVolume = EditorGUILayout.Slider(
                "Initial Water Volume",
                _hydraulicErosion.InitialWaterVolume,
                0.1f, 5.0f
            );
                
            EditorGUILayout.LabelField("Erosion Physics", EditorStyles.boldLabel);
                
            _hydraulicErosion.Inertia = EditorGUILayout.Slider(
                "Inertia",
                _hydraulicErosion.Inertia,
                0.0f, 1.0f
            );
                
            _hydraulicErosion.SedimentCapacityFactor = EditorGUILayout.Slider(
                "Sediment Capacity",
                _hydraulicErosion.SedimentCapacityFactor,
                0.1f, 10.0f
            );
                
            _hydraulicErosion.ErodeSpeed = EditorGUILayout.Slider(
                "Erosion Speed",
                _hydraulicErosion.ErodeSpeed,
                0.0f, 1.0f
            );
                
            _hydraulicErosion.DepositSpeed = EditorGUILayout.Slider(
                "Deposit Speed",
                _hydraulicErosion.DepositSpeed,
                0.0f, 1.0f
            );
                
            _hydraulicErosion.EvaporationRate = EditorGUILayout.Slider(
                "Evaporation Rate",
                _hydraulicErosion.EvaporationRate,
                0.0f, 0.1f
            );
                
            EditorGUILayout.LabelField("Brush Settings", EditorStyles.boldLabel);
                
            _hydraulicErosion.ErosionRadius = EditorGUILayout.IntSlider(
                "Erosion Radius",
                _hydraulicErosion.ErosionRadius,
                1, 10
            );
                
            _hydraulicErosion.ErosionFalloff = EditorGUILayout.Slider(
                "Erosion Falloff",
                _hydraulicErosion.ErosionFalloff,
                0.1f, 2.0f
            );
                
            EditorGUILayout.LabelField("Road Integration", EditorStyles.boldLabel);
                
            _hydraulicErosion.MaxErosionDepth = EditorGUILayout.Slider(
                "Max Erosion Depth",
                _hydraulicErosion.MaxErosionDepth,
                0.01f, 0.5f
            );
                
            _hydraulicErosion.RoadInfluenceMultiplier = EditorGUILayout.Slider(
                "Road Influence Multiplier",
                _hydraulicErosion.RoadInfluenceMultiplier,
                0.0f, 1.0f
            );
                
            _hydraulicErosion.RoadInfluenceDistance = EditorGUILayout.Slider(
                "Road Influence Distance",
                _hydraulicErosion.RoadInfluenceDistance,
                0.05f, 1.0f
            );
                
            if (GUILayout.Button("Apply Hydraulic Erosion"))
            {
                Undo.RegisterCompleteObjectUndo(_terrainManager.terrain.terrainData, "Apply Hydraulic Erosion");
                _terrainManager.ApplySmoother(_hydraulicErosion);
            }
                
            if (GUILayout.Button("Add to Preset"))
            {
                _presetSmoothers.Add(_hydraulicErosion.Clone());
            }
                
            EditorGUILayout.Space(10);
                
            // Thermal erosion
            EditorGUILayout.LabelField("Thermal Erosion", EditorStyles.boldLabel);
                
            EditorGUILayout.HelpBox("Simulates material slumping on steep slopes to create more natural terrain gradients.", MessageType.Info);
                
            _thermalErosion.Iterations = EditorGUILayout.IntSlider(
                "Iterations",
                _thermalErosion.Iterations,
                1, 20
            );
                
            _thermalErosion.Talus = EditorGUILayout.Slider(
                "Maximum Stable Slope",
                _thermalErosion.Talus,
                0.01f, 2.0f
            );
                
            _thermalErosion.ErosionRate = EditorGUILayout.Slider(
                "Erosion Rate",
                _thermalErosion.ErosionRate,
                0.0f, 1.0f
            );
                
            _thermalErosion.RespectRoadSlopes = EditorGUILayout.Toggle(
                "Respect Road Slopes",
                _thermalErosion.RespectRoadSlopes
            );
                
            if (_thermalErosion.RespectRoadSlopes)
            {
                _thermalErosion.RoadInfluenceDistance = EditorGUILayout.Slider(
                    "Road Influence Distance",
                    _thermalErosion.RoadInfluenceDistance,
                    0.05f, 1.0f
                );
                    
                _thermalErosion.RoadSlopeFactor = EditorGUILayout.Slider(
                    "Road Slope Factor",
                    _thermalErosion.RoadSlopeFactor,
                    0.0f, 1.0f
                );
            }
                
            if (GUILayout.Button("Apply Thermal Erosion"))
            {
                Undo.RegisterCompleteObjectUndo(_terrainManager.terrain.terrainData, "Apply Thermal Erosion");
                _terrainManager.ApplySmoother(_thermalErosion);
            }
                
            if (GUILayout.Button("Add to Preset"))
            {
                _presetSmoothers.Add(_thermalErosion.Clone());
            }
                
            EditorGUI.EndDisabledGroup();
                
            if (!_terrainManager.DistanceGridCalculated)
            {
                EditorGUILayout.HelpBox("Erosion requires a distance grid. Please calculate it first.", MessageType.Warning);
            }
        }
        
        private void DrawDistanceGrid()
        {
            _showDistanceGrid = EditorGUILayout.Foldout(_showDistanceGrid, "Distance Grid");

            if (!_showDistanceGrid) return;
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
                
            EditorGUI.BeginDisabledGroup(_terrainManager.DistanceGridCalculated);
            if (GUILayout.Button("Calculate Distance Grid"))
            {
                _terrainManager.CalculateDistanceGrid();
            }
            EditorGUI.EndDisabledGroup();
                
            if (_terrainManager.DistanceGridCalculated)
            {
                EditorGUILayout.HelpBox("Distance grid has been calculated and saved.", MessageType.Info);
                    
                EditorGUILayout.BeginHorizontal();
                    
                if (GUILayout.Button("Force Recalculate"))
                {
                    if (EditorUtility.DisplayDialog("Confirm Recalculation",
                            "Are you sure you want to recalculate the distance grid? This will overwrite the existing data.",
                            "Yes, Recalculate", "Cancel"))
                    {
                        _terrainManager.CalculateDistanceGrid();
                    }
                }
                    
                if (GUILayout.Button("Visualize Distance Grid"))
                {
                    _terrainManager.VisualizeDistanceGrid();
                }
                    
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.HelpBox("Distance grid has not been calculated yet. Click the button above to generate it.", MessageType.Warning);
            }
        }
        
        private void DrawPresets()
        {
            _showPresets = EditorGUILayout.Foldout(_showPresets, "Saved Presets");

            if (!_showPresets) return;
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
                
            if (_terrainManager.savedPresets.Count == 0)
            {
                EditorGUILayout.HelpBox("No presets have been saved yet. Create a preset using the One-Click Generation section.", MessageType.Info);
            }
            else
            {
                for (int i = 0; i < _terrainManager.savedPresets.Count; i++)
                {
                    TerrainGenerationPreset preset = _terrainManager.savedPresets[i];
                        
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                        
                    EditorGUILayout.LabelField(preset.name, EditorStyles.boldLabel);
                        
                    string generatorInfo = preset.Generators != null ? $"Generators: {preset.Generators.Count}" : "Generators: 0";
                    string smootherInfo = preset.Smoothers is { Count: > 0 } ? 
                        $"Smoothers: {preset.Smoothers.Count}" : "No smoothers";
                        
                    EditorGUILayout.LabelField(generatorInfo);
                    EditorGUILayout.LabelField(smootherInfo);
                        
                    EditorGUILayout.BeginHorizontal();
                        
                    if (GUILayout.Button("Apply"))
                    {
                        Undo.RegisterCompleteObjectUndo(_terrainManager.terrain.terrainData, "Apply Terrain Preset");
                        _terrainManager.ApplyPreset(preset);
                    }
                        
                    if (GUILayout.Button("Delete"))
                    {
                        if (EditorUtility.DisplayDialog("Confirm Deletion",
                                $"Are you sure you want to delete the preset '{preset.name}'?",
                                "Yes, Delete", "Cancel"))
                        {
                            Undo.RecordObject(_terrainManager, "Delete Terrain Generation Preset");
                            _terrainManager.savedPresets.RemoveAt(i);
                            EditorUtility.SetDirty(_terrainManager);
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
                _terrainManager.SavePresetsToProject();
            }

            if (GUILayout.Button("Load Presets from Project"))
            {
                _terrainManager.LoadPresetsFromProject();
            }

            EditorGUILayout.EndHorizontal();
        }
        
        #endregion
    }
}