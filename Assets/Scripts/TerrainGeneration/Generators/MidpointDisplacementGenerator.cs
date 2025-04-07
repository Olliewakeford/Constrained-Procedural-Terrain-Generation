using System;
using UnityEngine;
using TerrainGeneration.Core;

namespace TerrainGeneration.Generators
{
    [Serializable]
    public class MidpointDisplacementGenerator : ITerrainGenerator
    {
        // Private serialized fields
        [SerializeField] private float minHeight = 0.0f;
        [SerializeField] private float maxHeight = 1.0f;
        [SerializeField] private float roughness = 0.5f;
        [SerializeField] private float initialRandomRange = 0.5f;
        [SerializeField] private bool normalizeResult = true;
        [SerializeField] private int seed = 0;
        [SerializeField] private bool useAbsoluteRandom = false;

        // Public property accessors
        public float MinHeight
        {
            get => minHeight;
            set => minHeight = value;
        }

        public float MaxHeight
        {
            get => maxHeight;
            set => maxHeight = value;
        }

        public float Roughness
        {
            get => roughness;
            set => roughness = Mathf.Clamp(value, 0.1f, 1.0f);
        }
        
        public float InitialRandomRange
        {
            get => initialRandomRange;
            set => initialRandomRange = Mathf.Clamp(value, 0.0f, 1.0f);
        }
        
        public bool NormalizeResult
        {
            get => normalizeResult;
            set => normalizeResult = value;
        }
        
        public bool UseAbsoluteRandom
        {
            get => useAbsoluteRandom;
            set => useAbsoluteRandom = value;
        }
        
        public int Seed
        {
            get => seed;
            set => seed = value;
        }

        // Interface implementation
        public string Name => "Midpoint Displacement";

        public void Generate(float[,] heightMap, int width, int height, Func<int, int, bool> shouldModify)
        {
            // If seed is not 0, use it to initialize the random number generator
            System.Random prng = seed != 0 ? new System.Random(seed) : new System.Random();
            
            // Make sure we're working with a power of 2 plus 1 sized grid
            int size = Mathf.NextPowerOfTwo(Mathf.Min(width, height) - 1);
            
            // Store the original height range for later normalization
            float heightRange = maxHeight - minHeight;
            
            // Copy existing heights into a working buffer to avoid overwriting constrained areas
            float[,] workingMap = new float[width, height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    workingMap[x, y] = heightMap[x, y];
                }
            }
            
            // Initialize the corners of the terrain with random heights
            // Use a smaller range for initial values to prevent excessive peaks
            float cornerRandom = initialRandomRange * heightRange;
            
            // Calculate middle value within the min/max range
            float midValue = minHeight + (heightRange * 0.5f);
            
            if (shouldModify(0, 0))
                workingMap[0, 0] = midValue + RandomOffset(prng, cornerRandom);
            if (shouldModify(0, size))
                workingMap[0, size] = midValue + RandomOffset(prng, cornerRandom);
            if (shouldModify(size, 0))
                workingMap[size, 0] = midValue + RandomOffset(prng, cornerRandom);
            if (shouldModify(size, size))
                workingMap[size, size] = midValue + RandomOffset(prng, cornerRandom);
            
            // Diamond-Square algorithm
            int squareSize = size;
            float randomRange = cornerRandom;
            
            while (squareSize > 1)
            {
                int halfSize = squareSize / 2;
                
                // --- DIAMOND STEP ---
                for (int y = halfSize; y < height; y += squareSize)
                {
                    if (y >= height) continue; // Skip if out of bounds
                    
                    for (int x = halfSize; x < width; x += squareSize)
                    {
                        if (x >= width) continue; // Skip if out of bounds
                        
                        if (shouldModify(x, y))
                        {
                            // Get the four corners of the square
                            int x1 = Mathf.Max(x - halfSize, 0);
                            int y1 = Mathf.Max(y - halfSize, 0);
                            int x2 = Mathf.Min(x + halfSize, width - 1);
                            int y2 = Mathf.Min(y + halfSize, height - 1);
                            
                            // Calculate the average height of the four corners
                            float avg = (
                                workingMap[x1, y1] + // Top-left
                                workingMap[x2, y1] + // Top-right
                                workingMap[x1, y2] + // Bottom-left
                                workingMap[x2, y2]   // Bottom-right
                            ) / 4.0f;
                            
                            // Add scaled random displacement to the average
                            workingMap[x, y] = avg + RandomOffset(prng, randomRange);
                        }
                    }
                }
                
                // --- SQUARE STEP ---
                for (int y = 0; y < height; y += halfSize)
                {
                    for (int x = (y % squareSize == 0) ? halfSize : 0; x < width; x += squareSize)
                    {
                        if (x < width && y < height && shouldModify(x, y))
                        {
                            // Calculate average of the surrounding diamond points
                            float sum = 0f;
                            int count = 0;
                            
                            // North neighbor
                            if (y - halfSize >= 0)
                            {
                                sum += workingMap[x, y - halfSize];
                                count++;
                            }
                            
                            // South neighbor
                            if (y + halfSize < height)
                            {
                                sum += workingMap[x, y + halfSize];
                                count++;
                            }
                            
                            // West neighbor
                            if (x - halfSize >= 0)
                            {
                                sum += workingMap[x - halfSize, y];
                                count++;
                            }
                            
                            // East neighbor
                            if (x + halfSize < width)
                            {
                                sum += workingMap[x + halfSize, y];
                                count++;
                            }
                            
                            if (count > 0)
                            {
                                float avg = sum / count;
                                // Add scaled random displacement to the average
                                workingMap[x, y] = avg + RandomOffset(prng, randomRange);
                            }
                        }
                    }
                }
                
                // Reduce random range for next iteration - higher roughness = less reduction
                randomRange *= Mathf.Pow(2, -roughness);
                
                // Move to next smaller square size
                squareSize = halfSize;
            }
            
            // Find actual min/max values in the generated terrain
            float actualMin = float.MaxValue;
            float actualMax = float.MinValue;
            
            if (normalizeResult)
            {
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        if (shouldModify(x, y))
                        {
                            actualMin = Mathf.Min(actualMin, workingMap[x, y]);
                            actualMax = Mathf.Max(actualMax, workingMap[x, y]);
                        }
                    }
                }
            }
            
            // Apply the generated heights to the actual heightmap
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (shouldModify(x, y))
                    {
                        // Normalize the height to fit within minHeight and maxHeight if requested
                        if (normalizeResult && actualMin < actualMax)
                        {
                            float normalizedHeight = (workingMap[x, y] - actualMin) / (actualMax - actualMin);
                            heightMap[x, y] = minHeight + normalizedHeight * heightRange;
                        }
                        else
                        {
                            // Otherwise, just copy the value and clamp it to the desired range
                            heightMap[x, y] = Mathf.Clamp(workingMap[x, y], minHeight, maxHeight);
                        }
                    }
                }
            }
        }

        // Helper method for generating random displacement
        private float RandomOffset(System.Random random, float range)
        {
            if (useAbsoluteRandom)
            {
                // Just return a random value between 0 and range
                return (float)random.NextDouble() * range;
            }
            else
            {
                // Return a bipolar random value between -range/2 and +range/2
                return ((float)random.NextDouble() * 2 - 1) * (range / 2);
            }
        }

        public ITerrainGenerator Clone()
        {
            return new MidpointDisplacementGenerator
            {
                minHeight = this.minHeight,
                maxHeight = this.maxHeight,
                roughness = this.roughness,
                initialRandomRange = this.initialRandomRange,
                normalizeResult = this.normalizeResult,
                seed = this.seed,
                useAbsoluteRandom = this.useAbsoluteRandom
            };
        }
    }
}