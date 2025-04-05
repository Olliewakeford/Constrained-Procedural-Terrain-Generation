using System;
using System.Collections.Generic;
using TerrainGeneration.Core;
using UnityEngine;
using UnityEditor;


namespace TerrainGeneration.SmoothingAndErosion
{
    /// <summary>
    /// Basic smoother that uniformly smooths the terrain
    /// </summary>
    [Serializable]
    public class BasicSmoother : ITerrainSmoother
    {
        [SerializeField] private int iterations = 1;
        
        public string Name => "Basic Smoother";
        
        public bool RequiresDistanceGrid => false;
        
        public int Iterations
        {
            get => iterations;
            set => iterations = Mathf.Max(1, value);
        }
        
        public void Smooth(float[,] heightMap, int width, int height, Func<int, int, bool> shouldModify, int[,] distanceGrid = null)
        {
            float smoothProgress = 0;
            EditorUtility.DisplayProgressBar("Smoothing Terrain", "Progress", smoothProgress);
            
            for (int i = 0; i < iterations; i++)
            {
                // Create a copy of the height map to reference original heights
                float[,] originalHeightMap = new float[width, height];
                Array.Copy(heightMap, originalHeightMap, heightMap.Length);
                
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        if (!shouldModify(x, y)) continue;
                        
                        float avgHeight = originalHeightMap[x, y];
                        List<Vector2> neighbours = TerrainManager.GenerateNeighbours(new Vector2(x, y), width, height);
                        
                        foreach (Vector2 n in neighbours)
                        {
                            avgHeight += originalHeightMap[(int)n.x, (int)n.y];
                        }
                        
                        // Set the height of the current point to the average height
                        heightMap[x, y] = avgHeight / ((float)neighbours.Count + 1);
                    }
                }
                
                smoothProgress++;
                EditorUtility.DisplayProgressBar("Smoothing Terrain", "Progress", smoothProgress / iterations);
            }
            
            EditorUtility.ClearProgressBar();
        }
        
        public ITerrainSmoother Clone()
        {
            return new BasicSmoother
            {
                iterations = this.iterations
            };
        }
    }
    
}