using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using TerrainGeneration.Utilities;

namespace TerrainGeneration.Core
{
    /// <summary>
    /// Manages distance grid calculations and operations
    /// </summary>
    public class DistanceGridManager
    {
        #region Properties
        
        private readonly TerrainManager _terrainManager;

        private static readonly Vector2Int[] Neighbors = {
            new(-1, -1), new(-1, 0), new(-1, 1),
            new(0, -1),              new(0, 1),
            new(1, -1),  new(1, 0),  new(1, 1)
        };
        
        public int[,] DistanceGrid { get; private set; }

        public bool IsCalculated { get; private set; }
        
        private string DistanceGridSavePath
        {
            get
            {
                // Get the current scene Name
                string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                // Get the terrain object Name
                string terrainName = _terrainManager.gameObject.name;
                // Create a clean filename
                string cleanedName = string.Join("_", new[] { sceneName, terrainName }
                    .Select(s => string.Join("", s.Split(Path.GetInvalidFileNameChars()))));
                
                // Construct the full path
                return Path.Combine("Assets", "Resources", "DistanceGrids", $"{cleanedName}_distance_grid.dat");
            }
        }
        
        #endregion

        #region Constructor
        
        public DistanceGridManager(TerrainManager manager)
        {
            _terrainManager = manager;
            TryLoadDistanceGrid();
        }
        
        #endregion

        #region Public Methods
        
        /// <summary>
        /// Calculates a grid where each cell contains the minimum distance to a protected area
        /// </summary>
        public void CalculateDistanceGrid(int resolution, Func<int, int, bool> shouldModify)
        {
            // Check if we can load from saved file
            if (TryLoadDistanceGrid())
            {
                Debug.Log("Loaded pre-calculated distance grid from file");
                return;
            }
            
            DistanceGrid = new int[resolution, resolution];
            Queue<Vector2Int> queue = new Queue<Vector2Int>();
            int totalPixels = resolution * resolution;
            int processedPixels = 0;
            
            EditorUtility.DisplayProgressBar("Calculating Distance Grid", "Initializing grid...", 0f);
            
            // First pass: Initialize and find protected pixels
            for (int x = 0; x < resolution; x++)
            {
                for (int z = 0; z < resolution; z++)
                {
                    if (!shouldModify(x, z))
                    {
                        DistanceGrid[x, z] = 0;
                        queue.Enqueue(new Vector2Int(x, z));
                    }
                    else
                    {
                        DistanceGrid[x, z] = int.MaxValue;
                    }
                    
                    processedPixels++;
                    if (processedPixels % 1000 == 0)
                    {
                        EditorUtility.DisplayProgressBar("Calculating Distance Grid", 
                            "Initializing grid...", 
                            processedPixels / (float)totalPixels * 0.2f);
                    }
                }
            }
            
            // Second pass: BFS to propagate distances
            int initialQueueSize = queue.Count;
            int processed = 0;
            
            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();
                int currentDist = DistanceGrid[current.x, current.y];
                
                foreach (var offset in Neighbors)
                {
                    int newX = current.x + offset.x;
                    int newY = current.y + offset.y;
                    
                    if (newX < 0 || newX >= resolution || newY < 0 || newY >= resolution) continue;
                    
                    if (DistanceGrid[newX, newY] <= currentDist + 1) continue;
                    
                    DistanceGrid[newX, newY] = currentDist + 1;
                    queue.Enqueue(new Vector2Int(newX, newY));
                }
                
                processed++;
                if (processed % 1000 != 0) continue;
                float progress = 0.2f + (processed / (float)initialQueueSize * 0.8f);
                EditorUtility.DisplayProgressBar("Calculating Distance Grid", 
                    $"Processing pixels... ({processed}/{initialQueueSize})", 
                    progress);
            }
            
            EditorUtility.DisplayProgressBar("Calculating Distance Grid", "Saving results...", 1f);
            SaveDistanceGrid();
            IsCalculated = true;
            
            EditorUtility.ClearProgressBar();
            Debug.Log("Distance grid calculation completed and saved");
        }
        
        /// <summary>
        /// Creates a visual representation of the distance grid
        /// </summary>
        public void VisualizeDistanceGrid()
        {
            if (DistanceGrid == null)
            {
                if (!TryLoadDistanceGrid())
                {
                    Debug.LogError("No distance grid data available to visualize.");
                    return;
                }
            }
            
            string visualizationPath = Path.Combine(
                Path.GetDirectoryName(DistanceGridSavePath) ?? string.Empty,
                Path.GetFileNameWithoutExtension(DistanceGridSavePath) + "_visualization.png"
            );
            
            DistanceGridVisualizer.CreateVisualization(DistanceGrid, visualizationPath);
        }
        
        #endregion

        #region Private Methods
        
        /// <summary>
        /// Tries to load a previously calculated distance grid
        /// </summary>
        private bool TryLoadDistanceGrid(string customPath = null)
        {
            string path = customPath ?? DistanceGridSavePath;
            
            if (!File.Exists(path)) return false;
            
            try
            {
                using BinaryReader reader = new BinaryReader(File.Open(path, FileMode.Open));
                // Read and verify dimensions
                int savedWidth = reader.ReadInt32();
                int savedHeight = reader.ReadInt32();
                    
                if (_terrainManager.terrainData != null && 
                    (savedWidth != _terrainManager.terrainData.heightmapResolution || 
                     savedHeight != _terrainManager.terrainData.heightmapResolution))
                {
                    Debug.LogWarning("Saved distance grid dimensions don't match current terrain");
                    return false;
                }
                    
                // Read the grid data
                DistanceGrid = new int[savedWidth, savedHeight];
                for (int x = 0; x < savedWidth; x++)
                {
                    for (int z = 0; z < savedHeight; z++)
                    {
                        DistanceGrid[x, z] = reader.ReadInt32();
                    }
                }
                    
                IsCalculated = true;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Error loading distance grid: {e.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Saves the calculated distance grid
        /// </summary>
        private void SaveDistanceGrid(string customPath = null)
        {
            if (DistanceGrid == null) return;

            string path = customPath ?? DistanceGridSavePath;
            
            try
            {
                // Create directory if it doesn't exist
                string directory = Path.GetDirectoryName(path);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory ?? string.Empty);
                }
                
                using (BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Create)))
                {
                    // Write dimensions
                    writer.Write(DistanceGrid.GetLength(0));
                    writer.Write(DistanceGrid.GetLength(1));
                    
                    // Write the grid data
                    for (int x = 0; x < DistanceGrid.GetLength(0); x++)
                    {
                        for (int z = 0; z < DistanceGrid.GetLength(1); z++)
                        {
                            writer.Write(DistanceGrid[x, z]);
                        }
                    }
                }
                
                Debug.Log($"Distance grid saved to: {path}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Error saving distance grid: {e.Message}");
            }
        }
        
        #endregion
    }
}
