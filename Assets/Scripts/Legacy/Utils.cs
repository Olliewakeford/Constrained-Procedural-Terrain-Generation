using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Utils {

    // Function to generate Fractal Brownian Motion (fBM) based on Perlin noise
    // This function stacks multiple layers of Perlin noise (octaves) to create a more complex pattern
    public static float fBM(float x, float y, int oct, float persistance) {

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
}
