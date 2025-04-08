using System.Collections.Generic;
using TerrainGeneration.Generators;
using UnityEngine;

namespace TerrainGeneration.Core
{
    /// <summary>
    /// Main class responsible for managing terrain generation and modifications.
    /// </summary>
    [ExecuteInEditMode]
    public class TerrainManager : MonoBehaviour
    {
        #region Properties and Fields
        
        // Terrain references
        public Terrain terrain;
        public TerrainData terrainData;
        
        // Mask for prohibited areas where the heightmap should not be modified
        public Texture2D mask;
        public bool restoreTerrain = true;
        
        // Distance grid
        private DistanceGridManager _distanceGridManager;
        public bool DistanceGridCalculated => _distanceGridManager is { IsCalculated: true };
        
        // Preset management
        [SerializeField]
        public List<TerrainGenerationPreset> savedPresets = new();
        
        // Heightmap resolution shorthand
        private int HeightmapResolution => terrainData != null ? terrainData.heightmapResolution : 0;
        
        #endregion

        #region Unity Lifecycle

        private void OnEnable()
        {
            Debug.Log("Initializing Terrain Manager");
    
            // Initialize terrain references
            if (terrain == null)
            {
                terrain = GetComponent<Terrain>();
            }
    
            if (terrain != null)
            {
                terrainData = terrain.terrainData;
            }
            else
            {
                Debug.LogError("Terrain component not found on this GameObject!");
                return;
            }
    
            // Initialize distance grid manager
            _distanceGridManager = new DistanceGridManager(this);
    
            // Load or create presets
            LoadPresetsFromProject();
        }

        #endregion

        #region Core Methods
        
        // This method allows the user to use Unity's Undo to revert changes made to the terrain
        private void RecordUndo(string operationName)
        {
            #if UNITY_EDITOR
            // Record full Undo for terrain data (checking terrain exists first)
            if (terrain && terrain.terrainData)
            {
                UnityEditor.Undo.RegisterCompleteObjectUndo(terrain.terrainData, operationName);
            }
            #endif
        }
        
        /// <summary>
        /// Applies a terrain generator to the terrain
        /// </summary>
        public void ApplyGenerator(ITerrainGenerator generator)
        {
            if (!terrainData) return;
    
            // Record undo state before modification
            RecordUndo($"Apply {generator.Name}");
    
            // For UniformHeightGenerator, never reset the terrain, regardless of restoreTerrain setting
            bool originalRestoreValue = restoreTerrain;
            if (generator is UniformHeightGenerator)
            {
                restoreTerrain = false;
            }
    
            float[,] heightMap = GetHeightMap();
    
            // Restore the original restoreTerrain value
            restoreTerrain = originalRestoreValue;
    
            // Apply the generator
            generator.Generate(
                heightMap, 
                HeightmapResolution, 
                HeightmapResolution, 
                ShouldModifyTerrain
            );
    
            // Update the terrain
            terrainData.SetHeights(0, 0, heightMap);
        }
        
        // ReSharper disable Unity.PerformanceAnalysis
        /// <summary>
        /// Applies a terrain smoother to the terrain
        /// </summary>
        public void ApplySmoother(ITerrainSmoother smoother)
        {
            if (!terrainData) return;
            
            // Check if we need a distance grid
            if (smoother.RequiresDistanceGrid && !DistanceGridCalculated)
            {
                Debug.LogWarning("This smoother requires a distance grid. Please calculate it first.");
                return;
            }
            
            // Record undo state before modification
            RecordUndo($"Apply {smoother.Name}");
            
            float[,] heightMap = terrainData.GetHeights(0, 0, HeightmapResolution, HeightmapResolution);
            
            // Apply the smoother
            smoother.Smooth(
                heightMap, 
                HeightmapResolution, 
                HeightmapResolution, 
                ShouldModifyTerrain,
                smoother.RequiresDistanceGrid ? _distanceGridManager.DistanceGrid : null
            );
            
            // Update the terrain
            terrainData.SetHeights(0, 0, heightMap);
        }
        
        // ReSharper disable Unity.PerformanceAnalysis
        /// <summary>
        /// Applies a preset to the terrain
        /// </summary>
        public void ApplyPreset(TerrainGenerationPreset preset)
        {
            if (!terrainData) return;
    
            // Record undo state before any modifications
            RecordUndo($"Apply Preset: {preset.name}");
    
            // Start with a clean slate if needed
            if (restoreTerrain)
            {
                InternalRestoreTerrain();
            }
    
            // Apply all generators in sequence
            bool originalRestoreValue = restoreTerrain;
            restoreTerrain = false; // Don't reset between generators
    
            foreach (var generator in preset.Generators)
            {
                float[,] heightMap = GetHeightMap();
                generator.Generate(
                    heightMap, 
                    HeightmapResolution, 
                    HeightmapResolution, 
                    ShouldModifyTerrain
                );
                terrainData.SetHeights(0, 0, heightMap);
            }
    
            // Apply all smoothers in sequence
            foreach (var smoother in preset.Smoothers)
            {
                if (smoother == null) continue;
        
                // Check if we need a distance grid for this smoother
                if (smoother.RequiresDistanceGrid && !DistanceGridCalculated)
                {
                    Debug.LogWarning($"Smoother {smoother.Name} requires a distance grid. Please calculate it first.");
                    continue;
                }
        
                float[,] heightMap = terrainData.GetHeights(0, 0, HeightmapResolution, HeightmapResolution);
                smoother.Smooth(
                    heightMap, 
                    HeightmapResolution, 
                    HeightmapResolution, 
                    ShouldModifyTerrain,
                    smoother.RequiresDistanceGrid ? _distanceGridManager.DistanceGrid : null
                );
                terrainData.SetHeights(0, 0, heightMap);
            }
    
            // Reset flag
            restoreTerrain = originalRestoreValue;
        }
        
        // ReSharper disable Unity.PerformanceAnalysis
        /// <summary>
        /// Saves the currently loaded presets to the project directory
        /// </summary>
        public void SavePresetsToProject()
        {
            foreach (var preset in savedPresets)
            {
                TerrainPresetManager.SavePresetToProject(preset, true);
            }
        }
        
        // ReSharper disable Unity.PerformanceAnalysis
        /// <summary>
        /// Loads all presets from the project directory
        /// </summary>
        public void LoadPresetsFromProject()
        {
            List<TerrainGenerationPreset> projectPresets = TerrainPresetManager.LoadAllPresetsFromProject();

            // Clear existing presets
            savedPresets.Clear();

            // Add project presets to the list
            foreach (var preset in projectPresets)
            {
                savedPresets.Add(preset);
            }
    
            Debug.Log($"Loaded {savedPresets.Count} presets into TerrainManager");
        }
        
        // ReSharper disable Unity.PerformanceAnalysis
        /// <summary>
        /// Calculates the distance grid for distance-based operations
        /// </summary>
        public void CalculateDistanceGrid()
        {
            _distanceGridManager ??= new DistanceGridManager(this);

            _distanceGridManager.CalculateDistanceGrid(HeightmapResolution, ShouldModifyTerrain);
        }
        
        // ReSharper disable Unity.PerformanceAnalysis
        /// <summary>
        /// Visualizes the distance grid for debugging
        /// </summary>
        public void VisualizeDistanceGrid()
        {
            _distanceGridManager?.VisualizeDistanceGrid();
        }
        
        /// <summary>
        /// Restores the terrain to its base state (with protected areas maintained)
        /// </summary>
        public void RestoreTerrain()
        {
            RecordUndo("Restore Terrain");
            InternalRestoreTerrain();
        }
        
        /// <summary>
        /// Internal implementation of restore terrain without recording undo
        /// </summary>
        private void InternalRestoreTerrain()
        {
            if (!terrainData) return;
            
            // Create a new heightmap with zero heights
            float[,] resetHeightMap = new float[HeightmapResolution, HeightmapResolution];
            
            // Get the current heightmap
            float[,] currentHeightMap = terrainData.GetHeights(0, 0, HeightmapResolution, HeightmapResolution);
            
            // Iterate over each point in the heightmap
            for (int y = 0; y < HeightmapResolution; y++)
            {
                for (int x = 0; x < HeightmapResolution; x++)
                {
                    // If this point should not be modified, keep the current height
                    if (!ShouldModifyTerrain(x, y))
                    {
                        resetHeightMap[x, y] = currentHeightMap[x, y];
                    }
                    // Otherwise, set the height to 0
                    else
                    {
                        resetHeightMap[x, y] = 0;
                    }
                }
            }
            
            // Apply the reset heightmap to the terrain
            terrainData.SetHeights(0, 0, resetHeightMap);
        }
        
        #endregion

        #region Utility Methods

        private bool ShouldModifyTerrain(int x, int y)
        {
            if (!mask) return true;
            
            // Normalize coordinates to map to mask resolution
            float normX = y / (float)HeightmapResolution; // Swap y to x for rotation
            float normY = x / (float)HeightmapResolution; // Swap x to y for rotation
            
            // Get the corresponding pixel color in the mask
            Color maskColor = mask.GetPixelBilinear(normX, normY);
            
            // Only modify terrain where the mask is black (or a threshold of darkness)
            return maskColor is { r: < 0.1f, g: < 0.1f, b: < 0.1f };
        }
        
        /// <summary>
        /// Gets the current heightmap, optionally resetting modifiable areas to 0
        /// </summary>
        private float[,] GetHeightMap()
        {
            if (!terrainData) return null;
            
            // Get the current heightmap
            float[,] currentHeightMap = terrainData.GetHeights(0, 0, HeightmapResolution, HeightmapResolution);
            
            if (!restoreTerrain)
            {
                // Return the current heightmap if not resetting
                return currentHeightMap;
            }
            
            // Create a new heightmap to modify
            float[,] modifiedHeightMap = new float[HeightmapResolution, HeightmapResolution];
                
            // Iterate over each point in the heightmap
            for (int y = 0; y < HeightmapResolution; y++)
            {
                for (int x = 0; x < HeightmapResolution; x++)
                {
                    // If this point should not be modified, retain the original height
                    if (!ShouldModifyTerrain(x, y))
                    {
                        modifiedHeightMap[x, y] = currentHeightMap[x, y];
                    }
                    // Otherwise, set the height to 0
                    else
                    {
                        modifiedHeightMap[x, y] = 0;
                    }
                }
            }
                
            return modifiedHeightMap;
        }
        
        #endregion
    }
}