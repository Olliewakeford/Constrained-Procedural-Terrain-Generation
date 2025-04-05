using System;
using System.Collections.Generic;
using TerrainGeneration.Core;
using UnityEngine;
using UnityEditor;

namespace TerrainGeneration.SmoothingAndErosion
{
    /// <summary>
    /// Advanced smoother that uses variable smoothing with adaptive parameters
    /// </summary>
    [Serializable]
    public class AdaptiveSmoother : ITerrainSmoother
    {
        [SerializeField] private float baseSmoothing = 1f;
        [SerializeField] private float distanceFalloff = 0.5f;
        [SerializeField] private float detailPreservation = 0.5f;
        [SerializeField] private int iterations = 1;
        
        public string Name => "Adaptive Smoother";
        
        public bool RequiresDistanceGrid => true;
        
        public float BaseSmoothing
        {
            get => baseSmoothing;
            set => baseSmoothing = value;
        }
        
        public float DistanceFalloff
        {
            get => distanceFalloff;
            set => distanceFalloff = value;
        }
        
        public float DetailPreservation
        {
            get => detailPreservation;
            set => detailPreservation = Mathf.Clamp01(value);
        }
        
        public int Iterations
        {
            get => iterations;
            set => iterations = Mathf.Max(1, value);
        }
        
        public void Smooth(float[,] heightMap, int width, int height, Func<int, int, bool> shouldModify, int[,] distanceGrid = null)
        {
            if (distanceGrid == null)
            {
                Debug.LogError("Distance grid is required for adaptive smoothing");
                return;
            }
            
            // Find the maximum distance value for normalization
            float maxDistanceValue = 0;
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (distanceGrid[x, y] != int.MaxValue && distanceGrid[x, y] > maxDistanceValue)
                    {
                        maxDistanceValue = distanceGrid[x, y];
                    }
                }
            }
            
            if (maxDistanceValue == 0)
            {
                Debug.LogError("Invalid distance grid: no valid distances found");
                return;
            }
            
            float smoothProgress = 0;
            int totalIterations = iterations * width * height;
            
            // Show progress bar
            EditorUtility.DisplayProgressBar("Adaptive Smoothing", "Progress", smoothProgress);
            
            for (int iteration = 0; iteration < iterations; iteration++)
            {
                // Create a copy of the height map to reference original heights
                float[,] originalHeightMap = new float[width, height];
                Array.Copy(heightMap, originalHeightMap, heightMap.Length);
                
                // Calculate local height variation to detect areas with high detail
                float[,] localVariation = CalculateLocalVariation(originalHeightMap, width, height);
                float maxVariation = FindMaxVariation(localVariation, width, height);
                
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        if (!shouldModify(x, y)) continue;
                        
                        // Normalize the distance to [0,1] range
                        float normalizedDistance = distanceGrid[x, y] / maxDistanceValue;
                        
                        // Calculate the local detail factor (0 = low detail, 1 = high detail)
                        float normalizedVariation = maxVariation > 0 ? localVariation[x, y] / maxVariation : 0;
                        
                        // Reduce smoothing in areas with high detail
                        float detailFactor = 1 - (normalizedVariation * detailPreservation);
                        
                        // Calculate final smoothing strength
                        float smoothingFactor = baseSmoothing * detailFactor * Mathf.Pow(1 - normalizedDistance, distanceFalloff);
                        
                        if (smoothingFactor < 0.01f) continue; // Skip if smoothing effect would be negligible
                        
                        // Get neighboring heights and calculate weighted average
                        List<Vector2> neighbours = TerrainManager.GenerateNeighbours(new Vector2(x, y), width, height);
                        float totalWeight = smoothingFactor;
                        float smoothedHeight = originalHeightMap[x, y] * smoothingFactor;
                        
                        foreach (Vector2 n in neighbours)
                        {
                            int nx = (int)n.x;
                            int ny = (int)n.y;
                            
                            float neighborWeight = smoothingFactor;
                            totalWeight += neighborWeight;
                            smoothedHeight += originalHeightMap[nx, ny] * neighborWeight;
                        }
                        
                        // Apply weighted average
                        if (totalWeight > 0)
                        {
                            // Blend between original and smoothed height based on detail factor
                            float blendFactor = Mathf.Lerp(0.25f, 1f, detailFactor);
                            heightMap[x, y] = Mathf.Lerp(
                                originalHeightMap[x, y],
                                smoothedHeight / totalWeight,
                                blendFactor
                            );
                        }
                        
                        // Update progress
                        smoothProgress++;
                        if (smoothProgress % 1000 == 0) // Update progress bar every 1000 pixels
                        {
                            EditorUtility.DisplayProgressBar("Adaptive Smoothing", 
                                $"Iteration {iteration + 1}/{iterations}", 
                                smoothProgress / totalIterations);
                        }
                    }
                }
            }
            
            EditorUtility.ClearProgressBar();
        }
        
        public ITerrainSmoother Clone()
        {
            return new AdaptiveSmoother
            {
                baseSmoothing = this.baseSmoothing,
                distanceFalloff = this.distanceFalloff,
                detailPreservation = this.detailPreservation,
                iterations = this.iterations
            };
        }
        
        /// <summary>
        /// Calculates local height variation for each point in the heightmap
        /// </summary>
        private float[,] CalculateLocalVariation(float[,] heightMap, int width, int height)
        {
            float[,] variation = new float[width, height];
            
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    List<Vector2> neighbours = TerrainManager.GenerateNeighbours(new Vector2(x, y), width, height);
                    float heightSum = 0;
                    float heightSqSum = 0;
                    int count = neighbours.Count + 1;
                    
                    // Add center point
                    heightSum += heightMap[x, y];
                    heightSqSum += heightMap[x, y] * heightMap[x, y];
                    
                    // Add neighbors
                    foreach (Vector2 n in neighbours)
                    {
                        float h = heightMap[(int)n.x, (int)n.y];
                        heightSum += h;
                        heightSqSum += h * h;
                    }
                    
                    // Calculate standard deviation as a measure of local variation
                    float mean = heightSum / count;
                    float variance = (heightSqSum / count) - (mean * mean);
                    variation[x, y] = Mathf.Sqrt(Mathf.Max(0, variance));
                }
            }
            
            return variation;
        }
        
        /// <summary>
        /// Finds the maximum variation value in the variation map
        /// </summary>
        private float FindMaxVariation(float[,] variation, int width, int height)
        {
            float max = 0;
            
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (variation[x, y] > max)
                    {
                        max = variation[x, y];
                    }
                }
            }
            
            return max;
        }
    }
}