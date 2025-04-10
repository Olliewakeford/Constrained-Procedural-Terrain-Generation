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
        
        [SerializeField] private int iterations = 25;  // Number of erosion iterations to perform
        [SerializeField] private float talus = 0.01f;  // Maximum stable slope angle (as a height/width ratio)
        [SerializeField] private float erosionRate = 0.9f;  // Rate at which material is transferred (0-1)
        
        #endregion
        
        #region Interface Implementation
        
        public string Name => "Thermal Erosion";
        
        public bool RequiresDistanceGrid => false;
        
        public void Smooth(float[,] heightMap, int width, int height, Func<int, int, bool> shouldModify, int[,] distanceGrid = null)
        {
            // Analyze height range to better understand scale
            float minHeight = float.MaxValue;
            float maxHeight = float.MinValue;
            int modifiablePoints = 0;
            float maxSlope = 0f;
            
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (shouldModify(x, y))
                    {
                        modifiablePoints++;
                        minHeight = Mathf.Min(minHeight, heightMap[x, y]);
                        maxHeight = Mathf.Max(maxHeight, heightMap[x, y]);
                        
                        // Check neighbors for max slope
                        List<Vector2> neighbors = TerrainUtils.GenerateNeighbours(new Vector2(x, y), width, height);
                        foreach (Vector2 neighbor in neighbors)
                        {
                            int nx = (int)neighbor.x;
                            int ny = (int)neighbor.y;
                            float slope = Mathf.Abs(heightMap[x, y] - heightMap[nx, ny]);
                            maxSlope = Mathf.Max(maxSlope, slope);
                        }
                    }
                }
            }
            
            Debug.Log($"Terrain analysis: Height range = {minHeight} to {maxHeight}, Range = {maxHeight - minHeight}");
            Debug.Log($"Modifiable points: {modifiablePoints} out of {width * height}");
            Debug.Log($"Maximum slope found: {maxSlope}, Current talus threshold: {talus}");
            
            // If max slope is very small, auto-adjust talus
            if (maxSlope > 0 && maxSlope < talus)
            {
                float newTalus = maxSlope * 0.5f; // Set talus to half the max slope
                Debug.Log($"Auto-adjusting talus from {talus} to {newTalus} based on terrain analysis");
                talus = newTalus;
            }
            
            // Simple approach that doesn't depend on the distance grid
            Debug.Log($"Starting simplified thermal erosion with talus = {talus}, erosionRate = {erosionRate}, iterations = {iterations}");
            
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
                
                // Track changes for debugging
                int changesThisIteration = 0;
                float largestChange = 0f;
                
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
                        
                        // Use a fixed talus value everywhere for simplicity
                        float localTalus = talus;
                        
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
                            
                            // Only erode if slope exceeds the talus angle and we're higher than neighbor
                            if (heightDifference > localTalus)
                            {
                                // Calculate amount to transfer
                                float transferAmount = (heightDifference - localTalus) * erosionRate;
                                
                                // Apply the height transfer
                                heightMap[x, y] -= transferAmount;
                                heightMap[nx, ny] += transferAmount;
                                
                                // Track changes for debugging
                                changesThisIteration++;
                                largestChange = Mathf.Max(largestChange, transferAmount);
                                
                                // Debug log for significant transfers
                                if (transferAmount > maxHeight * 0.01f) {
                                    Debug.Log($"Transfer: {transferAmount:F4} from [{x},{y}] to [{nx},{ny}], Slope: {heightDifference:F4}");
                                }
                            }
                        }
                    }
                }
                
                // Log summary of changes for this iteration
                Debug.Log($"Iteration {iteration+1}: {changesThisIteration} changes made, largest change: {largestChange:F4}");
                
                // If no significant changes were made, we can break early
                if (largestChange < (maxHeight - minHeight) * 0.0001f) {
                    Debug.Log($"Early stopping at iteration {iteration+1} - no significant changes");
                    break;
                }
            }
            
            EditorUtility.ClearProgressBar();
            Debug.Log("Thermal erosion completed");
        }
        
        public ITerrainSmoother Clone()
        {
            return new ThermalErosion
            {
                iterations = this.iterations,
                talus = this.talus,
                erosionRate = this.erosionRate
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
            set => talus = Mathf.Max(0.0001f, value);
        }
        
        public float ErosionRate
        {
            get => erosionRate;
            set => erosionRate = Mathf.Clamp01(value);
        }
        
        #endregion
    }
}