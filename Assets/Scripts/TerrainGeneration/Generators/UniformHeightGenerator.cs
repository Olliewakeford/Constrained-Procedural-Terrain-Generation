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
        private ITerrainGenerator _terrainGeneratorImplementation;

        public string Name => "Uniform Height";
        
        public float UniformStep
        {
            get => uniformStep;
            set => uniformStep = value;
        }
        
        public void Generate(float[,] heightMap, int width, int height, Func<int, int, bool> shouldModify)
        {
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
        
        public ITerrainGenerator Clone()
        {
            return new UniformHeightGenerator
            {
                uniformStep = this.uniformStep
            };
        }
    }
}