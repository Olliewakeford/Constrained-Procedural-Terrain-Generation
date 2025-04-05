using System;
using UnityEngine;
using TerrainGeneration.Core;
using Random = UnityEngine.Random;

namespace TerrainGeneration.Generators
{
    /// <summary>
    /// Generator that applies random heights to the terrain
    /// </summary>
    [Serializable]
    public class RandomHeightGenerator : ITerrainGenerator
    {
        [SerializeField] private Vector2 heightLimits = new Vector2(0, 0.5f);
        
        public string Name => "Random Height";
        
        public Vector2 HeightLimits
        {
            get => heightLimits;
            set => heightLimits = value;
        }
        
        public void Generate(float[,] heightMap, int width, int height, Func<int, int, bool> shouldModify)
        {
            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < height; z++)
                {
                    if (shouldModify(x, z))
                    {
                        heightMap[x, z] += Random.Range(heightLimits.x, heightLimits.y);
                    }
                }
            }
        }
        
        public ITerrainGenerator Clone()
        {
            return new RandomHeightGenerator
            {
                heightLimits = this.heightLimits
            };
        }
    }
}