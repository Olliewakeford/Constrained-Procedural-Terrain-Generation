using System;
using UnityEngine;
using TerrainGeneration.Core;

namespace TerrainGeneration.Generators
{
    /// <summary>
    /// Generator that applies a uniform height change to the terrain
    /// </summary>
    [Serializable]
    public class UniformHeightGenerator : ITerrainGenerator
    {
        [SerializeField] private float uniformStep = 0.1f;
        [SerializeField] private bool normalizeToMinimum = false;
        private ITerrainGenerator _terrainGeneratorImplementation;

        public string Name => "Uniform Height";
        
        public float UniformStep
        {
            get => uniformStep;
            set => uniformStep = value;
        }
        
        public bool NormalizeToMinimum
        {
            get => normalizeToMinimum;
            set => normalizeToMinimum = value;
        }
        
        public void Generate(float[,] heightMap, int width, int height, Func<int, int, bool> shouldModify)
        {
            if (normalizeToMinimum)
            {
                // Find the minimum height of modifiable areas
                float minHeight = float.MaxValue;
        
                for (int x = 0; x < width; x++)
                {
                    for (int z = 0; z < height; z++)
                    {
                        if (shouldModify(x, z) && heightMap[x, z] < minHeight)
                        {
                            minHeight = heightMap[x, z];
                        }
                    }
                }
        
                // Only proceed if we found valid heights to normalize
                if (minHeight != float.MaxValue)
                {
                    // Apply the negative offset to bring the minimum to zero
                    for (int x = 0; x < width; x++)
                    {
                        for (int z = 0; z < height; z++)
                        {
                            if (shouldModify(x, z))
                            {
                                heightMap[x, z] -= minHeight;
                            }
                        }
                    }
                }
            }
            else
            {
                // Apply uniform height change
                for (int x = 0; x < width; x++)
                {
                    for (int z = 0; z < height; z++)
                    {
                        if (shouldModify(x, z))
                        {
                            heightMap[x, z] += uniformStep;
                        }
                    }
                }
            }
        }
        
        public ITerrainGenerator Clone()
        {
            return new UniformHeightGenerator
            {
                uniformStep = this.uniformStep,
                normalizeToMinimum = this.normalizeToMinimum
            };
        }
    }
}