using System;
using System.Collections.Generic;
using TerrainGeneration.Generators;
using TerrainGeneration.SmoothingAndErosion;
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
    
            // Load or create presets
            LoadOrCreateDefaultPresets();
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
        /// Saves the currently loaded presets to the project directory
        /// </summary>
        public void SavePresetsToProject()
        {
            foreach (var preset in savedPresets)
            {
                TerrainPresetManager.SavePresetToProject(preset, true);
            }
        }
        
        /// <summary>
        /// Loads all presets from the project directory
        /// </summary>
        public void LoadPresetsFromProject()
        {
            List<TerrainGenerationPreset> projectPresets = TerrainPresetManager.LoadAllPresetsFromProject();
    
            // Clear existing presets if desired, or merge them
            // savedPresets.Clear(); // Uncomment to replace instead of merge
    
            // Add project presets to the list
            foreach (var preset in projectPresets)
            {
                // Check for duplicates by name
                bool isDuplicate = false;
                foreach (var existingPreset in savedPresets)
                {
                    if (existingPreset.Name == preset.Name)
                    {
                        isDuplicate = true;
                        break;
                    }
                }
        
                if (!isDuplicate)
                {
                    savedPresets.Add(preset);
                }
            }
        }

        /// <summary>
        /// Saves a specific preset to the project directory
        /// </summary>
        public void SavePresetToProject(TerrainGenerationPreset preset)
        {
            TerrainPresetManager.SavePresetToProject(preset, true);
        }
        
        /// <summary>
        /// Creates and saves default presets
        /// </summary>
        public void CreateDefaultPresets()
        {
            // Clear existing presets
            savedPresets.Clear();
            
            // Create a mountains preset
            TerrainGenerationPreset mountainsPreset = new TerrainGenerationPreset("Mountains");
            
            // Add a Perlin noise generator for the base terrain
            PerlinNoiseGenerator baseNoise = new PerlinNoiseGenerator
            {
                XFrequency = 0.01f,
                YFrequency = 0.01f,
                Octaves = 3,
                Persistence = 1.5f,
                Amplitude = 0.3f
            };
            mountainsPreset.Generators.Add(baseNoise);
            
            // Add a Voronoi generator for mountain peaks
            VoronoiGenerator mountains = new VoronoiGenerator
            {
                PeakCount = 10,
                FallRate = 2.5f,
                DropOff = 2.0f,
                MinHeight = 0.2f,
                MaxHeight = 0.9f,
                Type = VoronoiGenerator.VoronoiType.Combined
            };
            mountainsPreset.Generators.Add(mountains);
            
            // Add adaptive smoother
            AdaptiveSmoother smoother = new AdaptiveSmoother
            {
                Iterations = 3,
                BaseSmoothing = 2.0f,
                DistanceFalloff = 1.5f,
                DetailPreservation = 0.6f
            };
            mountainsPreset.Smoother = smoother;
            
            // Add to saved presets
            savedPresets.Add(mountainsPreset);
            
            // Create a rolling hills preset
            TerrainGenerationPreset hillsPreset = new TerrainGenerationPreset("Rolling Hills");
            
            // Add a Perlin noise generator for the base terrain
            PerlinNoiseGenerator hillsNoise = new PerlinNoiseGenerator
            {
                XFrequency = 0.02f,
                YFrequency = 0.02f,
                Octaves = 5,
                Persistence = 1.2f,
                Amplitude = 0.15f
            };
            hillsPreset.Generators.Add(hillsNoise);
            
            // Add a smoother
            DistanceBasedSmoother hillsSmoother = new DistanceBasedSmoother
            {
                Iterations = 2,
                BaseSmoothing = 1.5f,
                DistanceFalloff = 2.0f
            };
            hillsPreset.Smoother = hillsSmoother;
            
            // Add to saved presets
            savedPresets.Add(hillsPreset);
            
            // Create a flat plains preset
            TerrainGenerationPreset plainsPreset = new TerrainGenerationPreset("Flat Plains");
            
            // Add a Perlin noise generator for subtle variation
            PerlinNoiseGenerator plainsNoise = new PerlinNoiseGenerator
            {
                XFrequency = 0.03f,
                YFrequency = 0.03f,
                Octaves = 2,
                Persistence = 0.8f,
                Amplitude = 0.05f
            };
            plainsPreset.Generators.Add(plainsNoise);
            
            // Add a basic smoother
            BasicSmoother plainsSmoother = new BasicSmoother
            {
                Iterations = 3
            };
            plainsPreset.Smoother = plainsSmoother;
            
            // Add to saved presets
            savedPresets.Add(plainsPreset);
            
            // Save all presets to project
            SavePresetsToProject();
            
            // Log success message
            Debug.Log("Created and saved default terrain presets");
        }
        
        /// <summary>
        /// Loads presets or creates defaults if none exist
        /// </summary>
        public void LoadOrCreateDefaultPresets()
        {
            // Load any existing presets
            LoadPresetsFromProject();
    
            // If no presets were loaded, create defaults
            if (savedPresets.Count == 0)
            {
                CreateDefaultPresets();
            }
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
        
        
        
        #endregion
    }
}