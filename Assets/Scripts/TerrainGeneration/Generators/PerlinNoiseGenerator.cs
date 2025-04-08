using System;
using UnityEngine;
using TerrainGeneration.Core;
using TerrainGeneration.Utilities;

namespace TerrainGeneration.Generators
{
    /// <summary>
    /// Generator that applies Perlin noise to the terrain
    /// </summary>
    [Serializable]
    public class PerlinNoiseGenerator : ITerrainGenerator
    {
        [SerializeField] private float xFrequency = 0.005f;
        [SerializeField] private float yFrequency = 0.005f;
        [SerializeField] private int xOffset;
        [SerializeField] private int yOffset;
        [SerializeField] private int octaves = 3;
        [SerializeField] private float persistence = 8f;
        [SerializeField] private float amplitude = 0.3f;
        
        public string Name => "Perlin Noise";
        
        public float XFrequency
        {
            get => xFrequency;
            set => xFrequency = value;
        }
        
        public float YFrequency
        {
            get => yFrequency;
            set => yFrequency = value;
        }
        
        public int XOffset
        {
            get => xOffset;
            set => xOffset = value;
        }
        
        public int YOffset
        {
            get => yOffset;
            set => yOffset = value;
        }
        
        public int Octaves
        {
            get => octaves;
            set => octaves = value;
        }
        
        public float Persistence
        {
            get => persistence;
            set => persistence = value;
        }
        
        public float Amplitude
        {
            get => amplitude;
            set => amplitude = value;
        }
        
        public void Generate(float[,] heightMap, int width, int height, Func<int, int, bool> shouldModify)
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (shouldModify(x, y))
                    {
                        // Use fractional Brownian motion for Perlin noise
                        heightMap[x, y] += TerrainUtils.fBM(
                            (x + xOffset) * xFrequency,
                            (y + yOffset) * yFrequency,
                            octaves,
                            persistence
                        ) * amplitude;
                    }
                }
            }
        }
        
        public ITerrainGenerator Clone()
        {
            return new PerlinNoiseGenerator
            {
                xFrequency = this.xFrequency,
                yFrequency = this.yFrequency,
                xOffset = this.xOffset,
                yOffset = this.yOffset,
                octaves = this.octaves,
                persistence = this.persistence,
                amplitude = this.amplitude
            };
        }
    }
}