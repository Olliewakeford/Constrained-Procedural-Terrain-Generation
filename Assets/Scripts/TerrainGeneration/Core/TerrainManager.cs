using System;
using System.Collections.Generic;
using UnityEngine;

namespace TerrainGeneration.Core
{
    /// <summary>
    /// Main class responsible for managing terrain generation and modifications.
    /// Replaces the original CustomTerrain class with a more modular approach.
    /// </summary>
    [ExecuteInEditMode]
    public class TerrainManager : MonoBehaviour
    {
        #region Properties and Fields
        
        // Core references
        public Terrain terrain;
        public TerrainData terrainData;
        
        // Mask for protected areas
        public Texture2D mask;
        public bool restoreTerrain = true;
        
        // Events
        public event Action OnTerrainChanged;
        
        // Distance grid
        private DistanceGridManager distanceGridManager;
        public bool DistanceGridCalculated => distanceGridManager != null && distanceGridManager.IsCalculated;
        
        // Preset management
        [SerializeField]
        public List<TerrainGenerationPreset> savedPresets = new List<TerrainGenerationPreset>();
        
        // Heightmap resolution shorthand
        public int HeightmapResolution => terrainData != null ? terrainData.heightmapResolution : 0;
        
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
            distanceGridManager = new DistanceGridManager(this);
        }

        #endregion

        #region Core Methods
        
        /// <summary>
        /// Records an undo operation before modifying the terrain
        /// </summary>
        private void RecordUndo(string operationName)
        {
            #if UNITY_EDITOR
            // Record full Undo for terrain data
            if (terrain != null && terrain.terrainData != null)
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
            if (terrainData == null) return;
            
            // Record undo state before modification
            RecordUndo($"Apply {generator.Name}");
            
            float[,] heightMap = GetHeightMap();
            
            // Apply the generator
            generator.Generate(
                heightMap, 
                HeightmapResolution, 
                HeightmapResolution, 
                ShouldModifyTerrain
            );
            
            // Update the terrain
            terrainData.SetHeights(0, 0, heightMap);
            
            // Notify listeners
            OnTerrainChanged?.Invoke();
        }
        
        /// <summary>
        /// Applies a terrain smoother to the terrain
        /// </summary>
        public void ApplySmoother(ITerrainSmoother smoother)
        {
            if (terrainData == null) return;
            
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
                smoother.RequiresDistanceGrid ? distanceGridManager.DistanceGrid : null
            );
            
            // Update the terrain
            terrainData.SetHeights(0, 0, heightMap);
            
            // Notify listeners
            OnTerrainChanged?.Invoke();
        }
        
        /// <summary>
        /// Applies a preset to the terrain
        /// </summary>
        public void ApplyPreset(TerrainGenerationPreset preset)
        {
            if (terrainData == null) return;
            
            // Record undo state before any modifications
            RecordUndo($"Apply Preset: {preset.Name}");
            
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
            
            // Apply smoother if provided
            if (preset.Smoother != null)
            {
                float[,] heightMap = terrainData.GetHeights(0, 0, HeightmapResolution, HeightmapResolution);
                preset.Smoother.Smooth(
                    heightMap, 
                    HeightmapResolution, 
                    HeightmapResolution, 
                    ShouldModifyTerrain,
                    preset.Smoother.RequiresDistanceGrid ? distanceGridManager.DistanceGrid : null
                );
                terrainData.SetHeights(0, 0, heightMap);
            }
            
            // Reset flag
            restoreTerrain = originalRestoreValue;
            
            // Notify listeners
            OnTerrainChanged?.Invoke();
        }
        
        /// <summary>
        /// Calculates the distance grid for distance-based operations
        /// </summary>
        public void CalculateDistanceGrid()
        {
            if (distanceGridManager == null)
            {
                distanceGridManager = new DistanceGridManager(this);
            }
            
            distanceGridManager.CalculateDistanceGrid(HeightmapResolution, ShouldModifyTerrain);
        }
        
        /// <summary>
        /// Visualizes the distance grid for debugging
        /// </summary>
        public void VisualizeDistanceGrid()
        {
            if (distanceGridManager != null)
            {
                distanceGridManager.VisualizeDistanceGrid();
            }
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
            if (terrainData == null) return;
            
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
            
            // Notify listeners
            OnTerrainChanged?.Invoke();
        }
        
        #endregion

        #region Utility Methods
        
        /// <summary>
        /// Determines if a point on the terrain should be modified based on the mask
        /// </summary>
        public bool ShouldModifyTerrain(int x, int y)
        {
            if (mask == null) return true;
            
            // Normalize coordinates to map to mask resolution
            float normX = y / (float)HeightmapResolution; // Swap y to x for rotation
            float normY = x / (float)HeightmapResolution; // Swap x to y for rotation
            
            // Get the corresponding pixel color in the mask
            Color maskColor = mask.GetPixelBilinear(normX, normY);
            
            // Only modify terrain where the mask is black (or a threshold of darkness)
            return maskColor.r < 0.1f && maskColor.g < 0.1f && maskColor.b < 0.1f;
        }
        
        /// <summary>
        /// Gets the current heightmap, optionally resetting modifiable areas to 0
        /// </summary>
        private float[,] GetHeightMap()
        {
            if (terrainData == null) return null;
            
            // Get the current heightmap
            float[,] currentHeightMap = terrainData.GetHeights(0, 0, HeightmapResolution, HeightmapResolution);
            
            if (!restoreTerrain)
            {
                // Return the current heightmap if not resetting
                return currentHeightMap;
            }
            else
            {
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
        }
        
        /// <summary>
        /// Generates a list of neighboring points around a given position
        /// </summary>
        public static List<Vector2> GenerateNeighbours(Vector2 pos, int width, int height)
        {
            List<Vector2> neighbours = new List<Vector2>();
            
            // Loop through a 3x3 grid centered on the given position
            for (int y = -1; y < 2; y++)
            {
                for (int x = -1; x < 2; x++)
                {
                    if (!(x == 0 && y == 0))
                    {
                        Vector2 neighbourPos = new Vector2(
                            Mathf.Clamp(pos.x + x, 0, width - 1),
                            Mathf.Clamp(pos.y + y, 0, height - 1));
                        
                        if (!neighbours.Contains(neighbourPos))
                        {
                            neighbours.Add(neighbourPos);
                        }
                    }
                }
            }
            
            return neighbours;
        }
        
        #endregion
    }
}