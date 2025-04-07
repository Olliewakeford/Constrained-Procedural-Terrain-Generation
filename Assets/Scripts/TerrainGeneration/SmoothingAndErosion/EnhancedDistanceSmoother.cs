using System;
using System.Collections.Generic;
using TerrainGeneration.Core;
using TerrainGeneration.Utilities;
using UnityEngine;
using UnityEditor;

namespace TerrainGeneration.SmoothingAndErosion
{
    /// <summary>
    /// Enhanced smoother that combines distance-based smoothing with detail preservation
    /// </summary>
    [Serializable]
    public class EnhancedDistanceSmoother : ITerrainSmoother
    {
        [Header("Basic Settings")]
        [SerializeField] private float baseSmoothing = 1f;
        [SerializeField] private int iterations = 1;
        
        [Header("Distance-Based Settings")]
        [SerializeField] private float distanceFalloff = 0.5f;
        [SerializeField] private bool useDistanceThreshold = true;
        [SerializeField] private float distanceThreshold = 0.4f; // Percentage of max distance
        [SerializeField] private float roadProximityWeight = 3f; // Weight for neighbors closer to roads
        
        [Header("Detail Preservation")]
        [SerializeField] private bool preserveDetail = true;
        [SerializeField] private float detailPreservation = 0.5f;
        [SerializeField] [Range(0, 1)] private float minSmoothingFactor = 0.05f;
        
        [Header("Blending Options")]
        [SerializeField] private bool useLinearBlending = false;
        [SerializeField] [Range(0.25f, 1f)] private float minBlendFactor = 0.25f;
        
        public string Name => "Enhanced Distance Smoother";
        
        public bool RequiresDistanceGrid => true;
        
        #region Property Accessors
        public float BaseSmoothing
        {
            get => baseSmoothing;
            set => baseSmoothing = value;
        }
        
        public int Iterations
        {
            get => iterations;
            set => iterations = Mathf.Max(1, value);
        }
        
        public float DistanceFalloff
        {
            get => distanceFalloff;
            set => distanceFalloff = value;
        }
        
        public bool UseDistanceThreshold
        {
            get => useDistanceThreshold;
            set => useDistanceThreshold = value;
        }
        
        public float DistanceThreshold
        {
            get => distanceThreshold;
            set => distanceThreshold = Mathf.Clamp01(value);
        }
        
        public float RoadProximityWeight
        {
            get => roadProximityWeight;
            set => roadProximityWeight = Mathf.Max(1f, value);
        }
        
        public bool PreserveDetail
        {
            get => preserveDetail;
            set => preserveDetail = value;
        }
        
        public float DetailPreservation
        {
            get => detailPreservation;
            set => detailPreservation = Mathf.Clamp01(value);
        }
        
        public float MinSmoothingFactor
        {
            get => minSmoothingFactor;
            set => minSmoothingFactor = Mathf.Clamp01(value);
        }
        
        public bool UseLinearBlending
        {
            get => useLinearBlending;
            set => useLinearBlending = value;
        }
        
        public float MinBlendFactor
        {
            get => minBlendFactor;
            set => minBlendFactor = Mathf.Clamp(value, 0.25f, 1f);
        }
        #endregion
        
        public void Smooth(float[,] heightMap, int width, int height, Func<int, int, bool> shouldModify,
            int[,] distanceGrid = null)
        {
            if (distanceGrid == null)
            {
                Debug.LogError("Distance grid is required for enhanced distance smoothing");
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
            EditorUtility.DisplayProgressBar("Enhanced Distance Smoothing", "Progress", smoothProgress);

            // Calculate local height variation if detail preservation is enabled
            float[,] localVariation = null;
            float maxVariation = 0;
            
            if (preserveDetail)
            {
                localVariation = CalculateLocalVariation(heightMap, width, height);
                maxVariation = FindMaxVariation(localVariation, width, height);
            }

            for (int iteration = 0; iteration < iterations; iteration++)
            {
                // Create a copy of the height map to reference original heights
                float[,] originalHeightMap = new float[width, height];
                Array.Copy(heightMap, originalHeightMap, heightMap.Length);
                
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        if (!shouldModify(x, y)) continue;

                        // Normalize the distance to [0,1] range
                        float normalizedDistance = distanceGrid[x, y] / maxDistanceValue;
                        
                        // Calculate smoothing factor based on distance
                        float smoothingFactor;
                        
                        if (useDistanceThreshold && normalizedDistance < distanceThreshold) {
                            // Strong, nearly constant smoothing for areas closer than the threshold
                            smoothingFactor = baseSmoothing * (0.8f + 0.2f * (1 - normalizedDistance/distanceThreshold));
                        } else {
                            // Gradual falloff for distant areas
                            float effectiveDistance = useDistanceThreshold 
                                ? (normalizedDistance - distanceThreshold) / (1 - distanceThreshold)
                                : normalizedDistance;
                                
                            smoothingFactor = baseSmoothing * Mathf.Pow(1 - effectiveDistance, distanceFalloff);
                        }
                        
                        // Apply detail preservation if enabled
                        float detailFactor = 1.0f;
                        if (preserveDetail && maxVariation > 0)
                        {
                            float normalizedVariation = localVariation[x, y] / maxVariation;
                            detailFactor = 1 - (normalizedVariation * detailPreservation);
                        }
                        
                        // Apply detail factor to smoothing
                        smoothingFactor *= detailFactor;
                        
                        // Ensure minimum smoothing threshold
                        smoothingFactor = Mathf.Max(smoothingFactor, minSmoothingFactor);
                        
                        if (smoothingFactor < 0.01f) continue; // Skip if smoothing effect would be negligible

                        // Get neighboring heights and calculate weighted average
                        List<Vector2> neighbours = TerrainUtils.GenerateNeighbours(new Vector2(x, y), width, height);
                        float totalWeight = smoothingFactor;
                        float smoothedHeight = originalHeightMap[x, y] * smoothingFactor;
                        
                        foreach (Vector2 n in neighbours) 
                        {
                            int nx = (int)n.x;
                            int ny = (int)n.y;
                            
                            // Calculate neighbor weight
                            float neighborWeight = smoothingFactor;
                            
                            // Add road proximity weighting if enabled
                            if (distanceGrid[nx, ny] < distanceGrid[x, y]) {
                                // This neighbor is closer to road, give it more weight
                                neighborWeight *= roadProximityWeight;
                            }
                            
                            totalWeight += neighborWeight;
                            smoothedHeight += originalHeightMap[nx, ny] * neighborWeight;
                        }

                        // Apply weighted average
                        if (totalWeight > 0)
                        {   
                            if (useLinearBlending)
                            {
                                // Use linear blending between original and smoothed height
                                float blendFactor = Mathf.Lerp(minBlendFactor, 1f, detailFactor);
                                heightMap[x, y] = Mathf.Lerp(
                                    originalHeightMap[x, y],
                                    smoothedHeight / totalWeight,
                                    blendFactor
                                );
                            }
                            else
                            {
                                // Use direct weighted average
                                heightMap[x, y] = smoothedHeight / totalWeight;
                            }
                        }

                        // Update progress
                        smoothProgress++;
                        if (smoothProgress % 1000 == 0) // Update progress bar every 1000 pixels
                        {
                            EditorUtility.DisplayProgressBar("Enhanced Distance Smoothing",
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
            return new EnhancedDistanceSmoother
            {
                baseSmoothing = this.baseSmoothing,
                iterations = this.iterations,
                distanceFalloff = this.distanceFalloff,
                useDistanceThreshold = this.useDistanceThreshold,
                distanceThreshold = this.distanceThreshold,
                roadProximityWeight = this.roadProximityWeight,
                preserveDetail = this.preserveDetail,
                detailPreservation = this.detailPreservation,
                minSmoothingFactor = this.minSmoothingFactor,
                useLinearBlending = this.useLinearBlending,
                minBlendFactor = this.minBlendFactor
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
                    List<Vector2> neighbours = TerrainUtils.GenerateNeighbours(new Vector2(x, y), width, height);
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