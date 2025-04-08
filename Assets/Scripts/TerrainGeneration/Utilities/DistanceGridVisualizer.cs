using UnityEngine;
using System.IO;

namespace TerrainGeneration.Utilities
{
    public static class DistanceGridVisualizer
    {
        public static void CreateVisualization(int[,] distanceGrid, string outputPath)
        {
            int width = distanceGrid.GetLength(0);
            int height = distanceGrid.GetLength(1);
            
            // Create a new texture
            Texture2D texture = new Texture2D(width, height);
            
            // Find the maximum distance value for normalization
            int maxDistance = 0;
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (distanceGrid[x, y] != int.MaxValue && distanceGrid[x, y] > maxDistance)
                    {
                        maxDistance = distanceGrid[x, y];
                    }
                }
            }

            // Create color gradient
            Color[] colorGradient = new Color[]
            {
                Color.black,      // Road pixels (distance 0)
                Color.blue,       // Close to road
                Color.cyan,       // Getting further
                Color.magenta,    // Middle distances
                Color.red,        // Further
                Color.green,      // Even further
                Color.gray,       // Getting far
                Color.white,      // Very far
                Color.yellow      // Furthest points
            };

            // Set pixel colors based on distance values
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    float normalizedValue = distanceGrid[x, y] == int.MaxValue ? 
                        1f : (float)distanceGrid[x, y] / maxDistance;
                    
                    // Get color from gradient
                    int colorIndex = Mathf.Clamp(
                        Mathf.FloorToInt(normalizedValue * (colorGradient.Length - 1)), 
                        0, 
                        colorGradient.Length - 2
                    );
                    float t = (normalizedValue * (colorGradient.Length - 1)) - colorIndex;
                    Color color = Color.Lerp(colorGradient[colorIndex], colorGradient[colorIndex + 1], t);
                    
                    texture.SetPixel(x, y, color);
                }
            }
            
            texture.Apply();

            // Save the texture as PNG
            byte[] bytes = texture.EncodeToPNG();
            File.WriteAllBytes(outputPath, bytes);
            
            // Clean up
            Object.DestroyImmediate(texture);
            
            Debug.Log($"Distance grid visualization saved to: {outputPath}");
        }
    }
}