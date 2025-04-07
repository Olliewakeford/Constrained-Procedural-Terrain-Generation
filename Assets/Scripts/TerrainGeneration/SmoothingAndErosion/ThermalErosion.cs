using System;
using System.Collections.Generic;
using TerrainGeneration.Core;
using TerrainGeneration.Utilities;
using UnityEngine;
using UnityEditor;

namespace TerrainGeneration.SmoothingAndErosion
{
    /// <summary>
    /// Simulates thermal erosion (material slumping) on terrain while respecting road constraints
    /// </summary>
    [Serializable]
    public class ThermalErosion : ITerrainSmoother
    {
        #region Parameters
        
        [SerializeField] private int iterations = 5;  // Number of erosion iterations to perform
        [SerializeField] private float talus = 0.4f;  // Maximum stable slope angle (as a height/width ratio)
        [SerializeField] private float erosionRate = 0.5f;  // Rate at which material is transferred (0-1)
        
        // Road integration parameters
        [SerializeField] private bool respectRoadSlopes = true;  // Whether to align slopes with nearby roads
        [SerializeField] private float roadInfluenceDistance = 0.3f;  // Distance at which roads influence erosion (normalized 0-1)
        [SerializeField] private float roadSlopeFactor = 0.8f;  // How strongly to enforce road slope matching (0-1)
        
        #endregion
        
        #region Interface Implementation
        
        public string Name => "Thermal Erosion";
        
        public bool RequiresDistanceGrid => true;
        
        public void Smooth(float[,] heightMap, int width, int height, Func<int, int, bool> shouldModify, int[,] distanceGrid = null)
        {
            if (distanceGrid == null && respectRoadSlopes)
            {
                Debug.LogError("Distance grid is required for road-aware thermal erosion");
                return;
            }
            
            // Find the maximum distance value for normalization
            float maxDistanceValue = 1;
            if (distanceGrid != null)
            {
                maxDistanceValue = FindMaxDistanceValue(distanceGrid, width, height);
                if (maxDistanceValue <= 0)
                {
                    Debug.LogError("Invalid distance grid: no valid distances found");
                    return;
                }
            }
            
            // Process multiple iterations
            for (int iteration = 0; iteration < iterations; iteration++)
            {
                // Show progress bar
                EditorUtility.DisplayProgressBar("Thermal Erosion", 
                    $"Processing iteration {iteration + 1}/{iterations}", 
                    (float)iteration / iterations);
                
                // Create a copy of the height map to read from
                float[,] originalHeightMap = new float[width, height];
                Array.Copy(heightMap, originalHeightMap, heightMap.Length);
                
                // Process each cell in the heightmap
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        // Skip if we can't modify this cell
                        if (!shouldModify(x, y)) continue;
                        
                        // Get current cell height
                        float currentHeight = originalHeightMap[x, y];
                        
                        // Get neighboring cells
                        List<Vector2> neighbors = TerrainUtils.GenerateNeighbours(new Vector2(x, y), width, height);
                        
                        // Calculate modified talus value based on road proximity if needed
                        float localTalus = talus;
                        float normalizedDistance = 1.0f;
                        
                        if (respectRoadSlopes && distanceGrid != null)
                        {
                            normalizedDistance = distanceGrid[x, y] / maxDistanceValue;
                            if (normalizedDistance < roadInfluenceDistance)
                            {
                                // Near roads, we want to enforce gentler slopes to match road grades
                                float roadInfluence = 1 - (normalizedDistance / roadInfluenceDistance);
                                
                                // Find nearest road height and calculate desired talus
                                float nearestRoadHeight = FindNearestRoadHeight(x, y, originalHeightMap, shouldModify, width, height);
                                float heightDifference = Mathf.Abs(currentHeight - nearestRoadHeight);
                                
                                // Only influence talus if we found a road
                                if (heightDifference > 0.001f)
                                {
                                    // Calculate ideal road talus - smaller value = gentler slope
                                    float roadTalus = talus * (1 - roadSlopeFactor);
                                    
                                    // Blend between regular talus and road talus based on distance
                                    localTalus = Mathf.Lerp(localTalus, roadTalus, roadInfluence);
                                }
                            }
                        }
                        
                        // Process each neighbor for potential material transfer
                        foreach (Vector2 neighbor in neighbors)
                        {
                            int nx = (int)neighbor.x;
                            int ny = (int)neighbor.y;
                            
                            // Skip if this neighbor can't be modified
                            if (!shouldModify(nx, ny)) continue;
                            
                            // Get this neighbor's height
                            float neighborHeight = originalHeightMap[nx, ny];
                            
                            // Calculate height difference and slope
                            float heightDifference = currentHeight - neighborHeight;
                            float slope = heightDifference; // For simplicity, assume grid spacing = 1
                            
                            // Only erode if slope exceeds the talus angle
                            if (slope > localTalus)
                            {
                                // Calculate amount to transfer
                                float transferAmount = (slope - localTalus) * erosionRate;
                                
                                // Adjust transfer rate near roads if needed
                                if (respectRoadSlopes && distanceGrid != null && normalizedDistance < roadInfluenceDistance)
                                {
                                    // Increase transfer rate near roads for quicker smoothing
                                    float roadInfluence = 1 - (normalizedDistance / roadInfluenceDistance);
                                    transferAmount *= 1 + roadInfluence;
                                }
                                
                                // Apply the height transfer
                                heightMap[x, y] -= transferAmount;
                                heightMap[nx, ny] += transferAmount;
                            }
                        }
                    }
                }
            }
            
            EditorUtility.ClearProgressBar();
        }
        
        public ITerrainSmoother Clone()
        {
            return new ThermalErosion
            {
                iterations = this.iterations,
                talus = this.talus,
                erosionRate = this.erosionRate,
                respectRoadSlopes = this.respectRoadSlopes,
                roadInfluenceDistance = this.roadInfluenceDistance,
                roadSlopeFactor = this.roadSlopeFactor
            };
        }
        
        #endregion
        
        #region Property Accessors
        
        public int Iterations
        {
            get => iterations;
            set => iterations = Mathf.Max(1, value);
        }
        
        public float Talus
        {
            get => talus;
            set => talus = Mathf.Max(0.01f, value);
        }
        
        public float ErosionRate
        {
            get => erosionRate;
            set => erosionRate = Mathf.Clamp01(value);
        }
        
        public bool RespectRoadSlopes
        {
            get => respectRoadSlopes;
            set => respectRoadSlopes = value;
        }
        
        public float RoadInfluenceDistance
        {
            get => roadInfluenceDistance;
            set => roadInfluenceDistance = Mathf.Clamp01(value);
        }
        
        public float RoadSlopeFactor
        {
            get => roadSlopeFactor;
            set => roadSlopeFactor = Mathf.Clamp01(value);
        }
        
        #endregion
        
        #region Private Methods
        
        private float FindMaxDistanceValue(int[,] distanceGrid, int width, int height)
        {
            float max = 0;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (distanceGrid[x, y] != int.MaxValue && distanceGrid[x, y] > max)
                    {
                        max = distanceGrid[x, y];
                    }
                }
            }
            return max;
        }
        
        private float FindNearestRoadHeight(int x, int y, float[,] heightMap, Func<int, int, bool> shouldModify, int width, int height)
        {
            // Simple breadth-first search to find the nearest road point
            Queue<Vector2Int> queue = new Queue<Vector2Int>();
            HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
            
            // Start at the current point
            Vector2Int start = new Vector2Int(x, y);
            queue.Enqueue(start);
            visited.Add(start);
            
            // Define search directions (8 directions)
            Vector2Int[] directions = new Vector2Int[]
            {
                new Vector2Int(-1, -1), new Vector2Int(-1, 0), new Vector2Int(-1, 1),
                new Vector2Int(0, -1),                         new Vector2Int(0, 1),
                new Vector2Int(1, -1),  new Vector2Int(1, 0),  new Vector2Int(1, 1)
            };
            
            // Search for the nearest road point
            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();
                
                // If this is a road point (not modifiable), return its height
                if (!shouldModify(current.x, current.y))
                {
                    return heightMap[current.x, current.y];
                }
                
                // Add all unvisited neighbors to the queue
                foreach (Vector2Int dir in directions)
                {
                    Vector2Int next = new Vector2Int(current.x + dir.x, current.y + dir.y);
                    
                    // Check if the neighbor is within bounds
                    if (next.x < 0 || next.x >= width || next.y < 0 || next.y >= height)
                    {
                        continue;
                    }
                    
                    // Check if the neighbor has been visited
                    if (visited.Contains(next))
                    {
                        continue;
                    }
                    
                    // Add the neighbor to the queue and mark as visited
                    queue.Enqueue(next);
                    visited.Add(next);
                }
                
                // Limit search depth to avoid performance issues
                if (visited.Count > 100)
                {
                    break;
                }
            }
            
            // No road point found within search radius, return current height
            return heightMap[x, y];
        }
        
        #endregion
    }
}