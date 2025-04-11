using UnityEngine;

namespace TerrainGeneration.Utilities
{
    public static class TerrainUtils
    {
        /// <summary>
        /// Function to generate Fractal Brownian Motion (fBM) based on Perlin noise
        /// This function stacks multiple layers of Perlin noise (octaves) to create a more complex pattern
        /// </summary>
        public static float fBM(float x, float y, int oct, float persistance)
        {
            float total = 0.0f;    // Total accumulated value from all octaves
            float frequency = 1.0f; // Starting frequency for the first octave
            float amplitude = 1.0f; // Starting amplitude for the first octave
            float maxValue = 0.0f;  // Used to normalize the final value between 0 and 1

            for (int i = 0; i < oct; ++i)
            {
                // Add the current octave's Perlin noise value, scaled by amplitude
                total += Mathf.PerlinNoise(x * frequency, y * frequency) * amplitude;

                // Keep track of the total possible amplitude for normalization later
                maxValue += amplitude;

                amplitude *= persistance; // Persistence controls how much the amplitude decreases with each octave
                frequency *= 2.0f; // Frequency change with each octave, (<1 adds detail)
            }

            // Return the normalized total value (0 to 1 range) after applying all octaves
            return total / maxValue;
        }
        
        /// <summary>
        /// Generates a list of neighboring points around a given position, excluding the position itself
        /// and any points that would fall outside the specified bounds.
        /// </summary>
        public static System.Collections.Generic.List<Vector2> GenerateNeighbours(Vector2 pos, int width, int height)
        {
            System.Collections.Generic.List<Vector2> neighbours = new System.Collections.Generic.List<Vector2>();
    
            // Loop through a 3x3 grid centered on the given position
            for (int y = -1; y < 2; y++)
            {
                for (int x = -1; x < 2; x++)
                {
                    // Skip the center point (current position)
                    if (x == 0 && y == 0)
                        continue;
            
                    // Calculate the neighbor position
                    int neighborX = (int)pos.x + x;
                    int neighborY = (int)pos.y + y;
            
                    // Skip neighbors that are out of bounds
                    if (neighborX < 0 || neighborX >= width || neighborY < 0 || neighborY >= height)
                        continue;
            
                    // Add valid neighbor
                    Vector2 neighbourPos = new Vector2(neighborX, neighborY);
                    neighbours.Add(neighbourPos);
                }
            }
    
            return neighbours;
        }
    }
}