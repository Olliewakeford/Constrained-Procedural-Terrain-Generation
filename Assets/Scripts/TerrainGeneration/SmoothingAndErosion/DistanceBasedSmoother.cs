using System;
using System.Collections.Generic;
using System.Linq;
using TerrainGeneration.Core;
using TerrainGeneration.Utilities;
using UnityEngine;
using UnityEditor;

namespace TerrainGeneration.SmoothingAndErosion
{
    /// <summary>
    /// Smoother that applies variable smoothing based on distance from protected areas
    /// </summary>
    [Serializable]
    public class DistanceBasedSmoother : ITerrainSmoother
    {
        [SerializeField] private float baseSmoothing = 1f;
        [SerializeField] private float distanceFalloff = 0.5f;
        [SerializeField] private int iterations = 1;

        public string Name => "Distance-Based Smoother";

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

        public int Iterations
        {
            get => iterations;
            set => iterations = Mathf.Max(1, value);
        }

        public void Smooth(float[,] heightMap, int width, int height, Func<int, int, bool> shouldModify,
            int[,] distanceGrid = null)
        {
            if (distanceGrid == null)
            {
                Debug.LogError("Distance grid is required for distance-based smoothing");
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
            EditorUtility.DisplayProgressBar("Distance-Based Smoothing", "Progress", smoothProgress);

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
                        
                        float smoothingFactor;
                        float distanceThreshold = 0.4f; // Adjust this to control how far the strong effect extends (40% of max distance)

                        if (normalizedDistance < distanceThreshold) {
                            // Strong, nearly constant smoothing for areas closer than the threshold
                            // This creates a more uniform smoothing zone around roads
                            smoothingFactor = baseSmoothing * (0.8f + 0.2f * (1 - normalizedDistance/distanceThreshold));
                        } else {
                            // Gradual falloff for distant areas
                            // This creates a gentle transition from the strongly smoothed area to natural terrain
                            float falloffFactor = (normalizedDistance - distanceThreshold) / (1 - distanceThreshold);
                            smoothingFactor = baseSmoothing * 0.8f * (1 - falloffFactor * falloffFactor);
                        }

                        // Ensure smoothing doesn't drop below this minimum threshold
                        smoothingFactor = Mathf.Max(smoothingFactor, 0.05f);

                        // Get neighboring heights and calculate weighted average
                        List<Vector2> neighbours = TerrainUtils.GenerateNeighbours(new Vector2(x, y), width, height);
                        float totalWeight = smoothingFactor;
                        float smoothedHeight = originalHeightMap[x, y] * smoothingFactor;
                        
                        foreach (Vector2 n in neighbours) {
                            int nx = (int)n.x;
                            int ny = (int)n.y;
    
                            // Give higher weight to neighbors that are closer to roads
                            // This "pulls" the terrain toward road height
                            float neighborWeight = smoothingFactor;
                            if (distanceGrid[nx, ny] < distanceGrid[x, y]) {
                                // This neighbor is closer to road, give it 2-3x weight
                                neighborWeight *= 2.5f;
                            }
    
                            totalWeight += neighborWeight;
                            smoothedHeight += originalHeightMap[nx, ny] * neighborWeight;
                        }

                        // Apply weighted average
                        if (totalWeight > 0)
                        {   
                            heightMap[x, y] = smoothedHeight / totalWeight;
                        }

                        // Update progress
                        smoothProgress++;
                        if (smoothProgress % 1000 == 0) // Update progress bar every 1000 pixels
                        {
                            EditorUtility.DisplayProgressBar("Distance-Based Smoothing",
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
            return new DistanceBasedSmoother
            {
                baseSmoothing = this.baseSmoothing,
                distanceFalloff = this.distanceFalloff,
                iterations = this.iterations
            };
        }
    }
}