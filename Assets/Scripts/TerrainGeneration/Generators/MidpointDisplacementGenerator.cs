using System;
using UnityEngine;
using TerrainGeneration.Core;

namespace TerrainGeneration.Generators
{
    [Serializable]
    public class MidpointDisplacementGenerator : ITerrainGenerator
    {
        // Private serialized fields
        [SerializeField] private float minHeight;
        [SerializeField] private float maxHeight = 1.0f;
        [SerializeField] private float roughness = 0.5f;
        [SerializeField] private float initialRandomRange = 0.5f;
        [SerializeField] private bool normalizeResult = true;
        [SerializeField] private int seed;
        [SerializeField] private bool useAbsoluteRandom;
        [SerializeField] private float displacementStrength = 0.5f; // Controls how much the displacement affects the existing terrain

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
        
        public float DisplacementStrength
        {
            get => displacementStrength;
            set => displacementStrength = Mathf.Clamp(value, 0.0f, 1.0f);
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
            
            // Create a displacement map to store our displacement values
            float[,] displacementMap = new float[width, height];
            
            // The corners of the terrain will receive random displacement
            float cornerRandom = initialRandomRange * heightRange;
            
            // Apply random displacement to the corners of the displacement map
            if (shouldModify(0, 0))
                displacementMap[0, 0] = RandomOffset(prng, cornerRandom);
            if (shouldModify(0, size) && size < height)
                displacementMap[0, size] = RandomOffset(prng, cornerRandom);
            if (shouldModify(size, 0) && size < width)
                displacementMap[size, 0] = RandomOffset(prng, cornerRandom);
            if (shouldModify(size, size) && size < width && size < height)
                displacementMap[size, size] = RandomOffset(prng, cornerRandom);
            
            // Diamond-Square algorithm to fill the displacement map
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
                            
                            // Calculate the average displacement of the four corners
                            float avg = (
                                displacementMap[x1, y1] + // Top-left
                                displacementMap[x2, y1] + // Top-right
                                displacementMap[x1, y2] + // Bottom-left
                                displacementMap[x2, y2]   // Bottom-right
                            ) / 4.0f;
                            
                            // Add scaled random displacement to the average
                            displacementMap[x, y] = avg + RandomOffset(prng, randomRange);
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
                                sum += displacementMap[x, y - halfSize];
                                count++;
                            }
                            
                            // South neighbor
                            if (y + halfSize < height)
                            {
                                sum += displacementMap[x, y + halfSize];
                                count++;
                            }
                            
                            // West neighbor
                            if (x - halfSize >= 0)
                            {
                                sum += displacementMap[x - halfSize, y];
                                count++;
                            }
                            
                            // East neighbor
                            if (x + halfSize < width)
                            {
                                sum += displacementMap[x + halfSize, y];
                                count++;
                            }
                            
                            if (count > 0)
                            {
                                float avg = sum / count;
                                // Add scaled random displacement to the average
                                displacementMap[x, y] = avg + RandomOffset(prng, randomRange);
                            }
                        }
                    }
                }
                
                // Reduce random range for next iteration - higher roughness = less reduction
                randomRange *= Mathf.Pow(2, -roughness);
                
                // Move to next smaller square size
                squareSize = halfSize;
            }
            
            // Find min/max values in the displacement map for normalization
            float dispMin = float.MaxValue;
            float dispMax = float.MinValue;
            
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (shouldModify(x, y))
                    {
                        dispMin = Mathf.Min(dispMin, displacementMap[x, y]);
                        dispMax = Mathf.Max(dispMax, displacementMap[x, y]);
                    }
                }
            }
            
            // Apply the displacement to the actual heightmap
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (shouldModify(x, y))
                    {
                        float displacement;
                        
                        // Normalize the displacement if requested
                        if (normalizeResult && dispMin < dispMax)
                        {
                            float normalizedDisp = (displacementMap[x, y] - dispMin) / (dispMax - dispMin);
                            displacement = minHeight + normalizedDisp * heightRange;
                        }
                        else
                        {
                            // Just clamp to the min/max range
                            displacement = Mathf.Clamp(displacementMap[x, y], minHeight, maxHeight);
                        }
                        
                        // Add the displacement to the existing height, scaled by displacement strength
                        heightMap[x, y] += displacement * displacementStrength;
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
                useAbsoluteRandom = this.useAbsoluteRandom,
                displacementStrength = this.displacementStrength
            };
        }
    }
}