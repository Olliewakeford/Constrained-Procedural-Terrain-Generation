using System;
using UnityEngine;
using TerrainGeneration.Core;

namespace TerrainGeneration.SmoothingAndErosion
{
    [Serializable]
    public class DirectionalGradientSmoother : ITerrainSmoother
    {
        [SerializeField] private float searchRadius = 20.0f;
        [SerializeField] [Range(0, 1)] private float adjustmentStrength = 0.5f;
        [SerializeField] [Range(0, 1)] private float directionInfluence = 0.7f;
        [SerializeField] [Range(0, 1)] private float detailPreservation = 0.6f;
        [SerializeField] private FalloffType distanceFalloff = FalloffType.Linear;
        
        public enum FalloffType { Linear, Quadratic, Exponential }
        
        // Properties
        public float SearchRadius { get => searchRadius; set => searchRadius = Mathf.Max(1f, value); }
        public float AdjustmentStrength { get => adjustmentStrength; set => adjustmentStrength = Mathf.Clamp01(value); }
        public float DirectionInfluence { get => directionInfluence; set => directionInfluence = Mathf.Clamp01(value); }
        public float DetailPreservation { get => detailPreservation; set => detailPreservation = Mathf.Clamp01(value); }
        public FalloffType DistanceFalloff { get => distanceFalloff; set => distanceFalloff = value; }
        
        // ITerrainSmoother implementation
        public string Name => "Directional Gradient Smoother";
        public bool RequiresDistanceGrid => true;
        
        public void Smooth(float[,] heightMap, int width, int height, Func<int, int, bool> shouldModify, int[,] distanceGrid)
        {
            // Create a copy of the original heightmap for reference
            float[,] originalHeightMap = new float[width, height];
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    originalHeightMap[x, y] = heightMap[x, y];
                }
            }
            
            // Create a temporary heightmap for the modifications
            float[,] tempHeightMap = new float[width, height];
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    tempHeightMap[x, y] = heightMap[x, y];
                }
            }

            // Process each point in the terrain
            for (int x = 1; x < width - 1; x++)
            {
                for (int y = 1; y < height - 1; y++)
                {
                    // Skip points that shouldn't be modified (road areas)
                    if (!shouldModify(x, y))
                        continue;
                    
                    // Get normalized distance from nearest road (0 = at road, 1 = max distance)
                    float normalizedDistance = CalculateNormalizedDistance(distanceGrid, x, y, width, height);
                    
                    // Skip if beyond search radius (using normalized distance)
                    if (normalizedDistance > 1)
                        continue;
                        
                    // Calculate current gradient at this point
                    Vector2 currentGradient = CalculateLocalGradient(originalHeightMap, x, y, width, height);
                    
                    // Find the best road point to adjust toward
                    bool foundRoadPoint = FindBestRoadPoint(originalHeightMap, distanceGrid, x, y, width, height, 
                                                           currentGradient, out Vector2 targetDirection, out float targetHeight);
                    
                    if (foundRoadPoint)
                    {
                        // Calculate falloff based on distance
                        float falloff = CalculateFalloff(normalizedDistance);
                        
                        // Apply the adjustment
                        float currentHeight = originalHeightMap[x, y];
                        float heightDifference = targetHeight - currentHeight;
                        
                        // Calculate adjustment amount based on parameters
                        float adjustmentAmount = heightDifference * adjustmentStrength * falloff;
                        
                        // Apply detail preservation (keep some of the original variation)
                        float detailOffset = (originalHeightMap[x, y] - targetHeight) * detailPreservation;
                        
                        // Apply the final height adjustment
                        tempHeightMap[x, y] = originalHeightMap[x, y] + adjustmentAmount + detailOffset;
                    }
                }
            }
            
            // Copy the temporary height map back to the original
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    heightMap[x, y] = tempHeightMap[x, y];
                }
            }
        }
        
        private float CalculateNormalizedDistance(int[,] distanceGrid, int x, int y, int width, int height)
        {
            // Get the raw distance from the distance grid
            int rawDistance = distanceGrid[x, y];
            
            // Convert to normalized distance (0-1 range where 1 is at searchRadius)
            return rawDistance / searchRadius;
        }

        private Vector2 CalculateLocalGradient(float[,] heightMap, int x, int y, int width, int height)
        {
            // Calculate X gradient using central difference
            float dx = (heightMap[x + 1, y] - heightMap[x - 1, y]) / 2.0f;
            
            // Calculate Y gradient using central difference
            float dy = (heightMap[x, y + 1] - heightMap[x, y - 1]) / 2.0f;
            
            // Return the gradient vector
            return new Vector2(dx, dy);
        }

        private bool FindBestRoadPoint(float[,] heightMap, int[,] distanceGrid, int x, int y, int width, int height, 
                                      Vector2 currentGradient, out Vector2 targetDirection, out float targetHeight)
        {
            // Initialize output variables
            targetDirection = Vector2.zero;
            targetHeight = heightMap[x, y];
            
            // Initialize variables for best road point search
            float bestScore = float.MinValue;
            bool foundPoint = false;
            int bestX = x;
            int bestY = y;
            
            // Search area (within search radius)
            int searchSize = Mathf.CeilToInt(searchRadius);
            
            for (int offsetX = -searchSize; offsetX <= searchSize; offsetX++)
            {
                for (int offsetY = -searchSize; offsetY <= searchSize; offsetY++)
                {
                    // Skip if outside terrain bounds
                    int newX = x + offsetX;
                    int newY = y + offsetY;
                    
                    if (newX < 0 || newX >= width || newY < 0 || newY >= height)
                        continue;
                    
                    // Skip if not a road point (distance must be 0)
                    if (distanceGrid[newX, newY] != 0)
                        continue;
                    
                    // Calculate distance to this road point
                    float distance = Mathf.Sqrt(offsetX * offsetX + offsetY * offsetY);
                    if (distance > searchRadius)
                        continue;
                    
                    // Calculate direction to this road point
                    Vector2 direction = new Vector2(offsetX, offsetY).normalized;
                    
                    // Calculate alignment with current gradient direction
                    // Invert current gradient as we want to align with slope direction
                    Vector2 invertedGradient = -currentGradient.normalized;
                    float directionAlignment = Vector2.Dot(invertedGradient, direction);
                    
                    // Calculate distance score (closer is better)
                    float distanceScore = 1.0f - (distance / searchRadius);
                    
                    // Calculate height difference score
                    float heightDifference = Mathf.Abs(heightMap[newX, newY] - heightMap[x, y]);
                    float heightScore = 1.0f / (1.0f + heightDifference); // Better if heights are similar
                    
                    // Calculate combined score
                    // Higher directionInfluence means directional alignment matters more
                    float score = (directionAlignment * directionInfluence) + 
                                  (distanceScore * (1.0f - directionInfluence)) +
                                  (heightScore * 0.3f); // Small weight for height similarity
                    
                    // Update best road point if this one has a better score
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestX = newX;
                        bestY = newY;
                        foundPoint = true;
                    }
                }
            }
            
            // If we found a road point, calculate target direction and height
            if (foundPoint)
            {
                // Direction from current point to best road point
                targetDirection = new Vector2(bestX - x, bestY - y).normalized;
                
                // Target height is the road height
                targetHeight = heightMap[bestX, bestY];
                
                return true;
            }
            
            return false;
        }

        private float CalculateFalloff(float normalizedDistance)
        {
            // Apply different falloff functions based on the selected type
            switch (distanceFalloff)
            {
                case FalloffType.Linear:
                    return 1.0f - normalizedDistance;
                    
                case FalloffType.Quadratic:
                    return 1.0f - (normalizedDistance * normalizedDistance);
                    
                case FalloffType.Exponential:
                    return Mathf.Exp(-3.0f * normalizedDistance);
                    
                default:
                    return 1.0f - normalizedDistance;
            }
        }
        
        public ITerrainSmoother Clone()
        {
            return new DirectionalGradientSmoother
            {
                searchRadius = this.searchRadius,
                adjustmentStrength = this.adjustmentStrength,
                directionInfluence = this.directionInfluence,
                detailPreservation = this.detailPreservation,
                distanceFalloff = this.distanceFalloff
            };
        }
    }
}