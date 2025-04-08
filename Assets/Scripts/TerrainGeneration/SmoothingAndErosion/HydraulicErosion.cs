using System;
using System.Collections.Generic;
using TerrainGeneration.Core;
using TerrainGeneration.Utilities;
using UnityEngine;
using UnityEditor;
using Random = UnityEngine.Random;

namespace TerrainGeneration.SmoothingAndErosion
{
    /// <summary>
    /// Simulates hydraulic erosion on terrain while respecting road constraints
    /// </summary>
    [Serializable]
    public class HydraulicErosion : ITerrainSmoother
    {
        #region Parameters
        
        // Basic parameters
        [SerializeField] private int dropletCount = 50000;  // Number of droplets to simulate
        [SerializeField] private int maxDropletLifetime = 30;  // Maximum steps each droplet can take
        [SerializeField] private float initialWaterVolume = 1.0f;  // Initial water volume of each droplet
        [SerializeField] private float initialSpeed = 1.0f;  // Initial speed of each droplet
        
        // Physics parameters
        [SerializeField] private float inertia = 0.05f;  // How much previous direction influences flow (0-1)
        [SerializeField] private float gravity = 4.0f;  // Strength of gravity effect
        [SerializeField] private float evaporationRate = 0.01f;  // How quickly water evaporates
        
        // Erosion parameters
        [SerializeField] private float sedimentCapacityFactor = 4.0f;  // How much sediment a droplet can carry
        [SerializeField] private float minSedimentCapacity = 0.01f;  // Minimum sediment capacity
        [SerializeField] private float erodeSpeed = 0.3f;  // How quickly terrain is eroded
        [SerializeField] private float depositSpeed = 0.3f;  // How quickly sediment is deposited
        
        // Erosion brush parameters
        [SerializeField] private int erosionRadius = 3;  // Radius of erosion/deposit effect
        [SerializeField] private float erosionFalloff = 0.5f;  // Falloff of erosion brush (higher = steeper falloff)
        
        // Road integration parameters
        [SerializeField] private float maxErosionDepth = 0.1f;  // Maximum erosion depth as fraction of terrain height
        [SerializeField] private float roadInfluenceMultiplier = 0.8f;  // How much the roads reduce erosion (0-1, higher = less erosion)
        [SerializeField] private float roadInfluenceDistance = 0.3f;  // Distance (normalized 0-1) at which roads influence erosion

        #endregion
        
        #region Interface Implementation
        
        public string Name => "Hydraulic Erosion";
        
        public bool RequiresDistanceGrid => true;
        
        public void Smooth(float[,] heightMap, int width, int height, Func<int, int, bool> shouldModify, int[,] distanceGrid = null)
        {
            if (distanceGrid == null)
            {
                Debug.LogError("Distance grid is required for hydraulic erosion");
                return;
            }
            
            // Find the maximum distance value for normalization
            float maxDistanceValue = FindMaxDistanceValue(distanceGrid, width, height);
            if (maxDistanceValue <= 0)
            {
                Debug.LogError("Invalid distance grid: no valid distances found");
                return;
            }
            
            // Create a copy of the height map to work with
            float[,] originalHeightMap = new float[width, height];
            Array.Copy(heightMap, originalHeightMap, heightMap.Length);
            
            // Precompute erosion brush kernels for performance
            float[][] erosionBrushWeights = PrecomputeErosionBrush(erosionRadius, erosionFalloff);
            
            // Simulate water droplets
            int progress;
            int totalDroplets = dropletCount;
            
            EditorUtility.DisplayProgressBar("Hydraulic Erosion", "Initializing...", 0f);
            
            for (int i = 0; i < dropletCount; i++)
            {
                // Update progress bar every 100 droplets
                if (i % 100 == 0)
                {
                    progress = i;
                    float progressPercent = progress / (float)totalDroplets;
                    EditorUtility.DisplayProgressBar("Hydraulic Erosion", 
                        $"Simulating droplet {progress}/{totalDroplets}", 
                        progressPercent);
                }
                
                // Create and simulate a single water droplet
                SimulateDroplet(heightMap, width, height, shouldModify, distanceGrid, maxDistanceValue, erosionBrushWeights);
            }
            
            EditorUtility.ClearProgressBar();
        }
        
        public ITerrainSmoother Clone()
        {
            return new HydraulicErosion
            {
                dropletCount = this.dropletCount,
                maxDropletLifetime = this.maxDropletLifetime,
                initialWaterVolume = this.initialWaterVolume,
                initialSpeed = this.initialSpeed,
                inertia = this.inertia,
                gravity = this.gravity,
                evaporationRate = this.evaporationRate,
                sedimentCapacityFactor = this.sedimentCapacityFactor,
                minSedimentCapacity = this.minSedimentCapacity,
                erodeSpeed = this.erodeSpeed,
                depositSpeed = this.depositSpeed,
                erosionRadius = this.erosionRadius,
                erosionFalloff = this.erosionFalloff,
                maxErosionDepth = this.maxErosionDepth,
                roadInfluenceMultiplier = this.roadInfluenceMultiplier,
                roadInfluenceDistance = this.roadInfluenceDistance
            };
        }
        
        #endregion
        
        #region Property Accessors
        
        public int DropletCount
        {
            get => dropletCount;
            set => dropletCount = Mathf.Max(1, value);
        }
        
        public int MaxDropletLifetime
        {
            get => maxDropletLifetime;
            set => maxDropletLifetime = Mathf.Max(1, value);
        }
        
        public float InitialWaterVolume
        {
            get => initialWaterVolume;
            set => initialWaterVolume = Mathf.Max(0.1f, value);
        }
        
        public float InitialSpeed
        {
            get => initialSpeed;
            set => initialSpeed = Mathf.Max(0.1f, value);
        }
        
        public float Inertia
        {
            get => inertia;
            set => inertia = Mathf.Clamp01(value);
        }
        
        public float Gravity
        {
            get => gravity;
            set => gravity = Mathf.Max(0.1f, value);
        }
        
        public float EvaporationRate
        {
            get => evaporationRate;
            set => evaporationRate = Mathf.Clamp01(value);
        }
        
        public float SedimentCapacityFactor
        {
            get => sedimentCapacityFactor;
            set => sedimentCapacityFactor = Mathf.Max(0.1f, value);
        }
        
        public float MinSedimentCapacity
        {
            get => minSedimentCapacity;
            set => minSedimentCapacity = Mathf.Max(0.001f, value);
        }
        
        public float ErodeSpeed
        {
            get => erodeSpeed;
            set => erodeSpeed = Mathf.Clamp01(value);
        }
        
        public float DepositSpeed
        {
            get => depositSpeed;
            set => depositSpeed = Mathf.Clamp01(value);
        }
        
        public int ErosionRadius
        {
            get => erosionRadius;
            set => erosionRadius = Mathf.Max(1, value);
        }
        
        public float ErosionFalloff
        {
            get => erosionFalloff;
            set => erosionFalloff = Mathf.Max(0.01f, value);
        }
        
        public float MaxErosionDepth
        {
            get => maxErosionDepth;
            set => maxErosionDepth = Mathf.Clamp01(value);
        }
        
        public float RoadInfluenceMultiplier
        {
            get => roadInfluenceMultiplier;
            set => roadInfluenceMultiplier = Mathf.Clamp01(value);
        }
        
        public float RoadInfluenceDistance
        {
            get => roadInfluenceDistance;
            set => roadInfluenceDistance = Mathf.Clamp01(value);
        }
        
        #endregion
        
        #region Private Methods
        
        private float FindMaxDistanceValue(int[,] distanceGrid, int width, int height)
        {
            float max = 0;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (distanceGrid[x, y] != int.MaxValue && distanceGrid[x, y] > max)
                    {
                        max = distanceGrid[x, y];
                    }
                }
            }
            return max;
        }
        
        private float[][] PrecomputeErosionBrush(int radius, float falloff)
        {
            float[][] brushWeights = new float[radius * 2 + 1][];
            for (int i = 0; i < brushWeights.Length; i++)
            {
                brushWeights[i] = new float[radius * 2 + 1];
            }
            
            float weightSum = 0;
            
            for (int y = -radius; y <= radius; y++)
            {
                for (int x = -radius; x <= radius; x++)
                {
                    float distSq = x * x + y * y;
                    if (distSq > radius * radius) continue;
                    
                    float weight = 1 - Mathf.Pow(Mathf.Sqrt(distSq) / radius, falloff);
                    brushWeights[y + radius][x + radius] = weight;
                    weightSum += weight;
                }
            }
            
            // Normalize weights
            for (int y = 0; y < brushWeights.Length; y++)
            {
                for (int x = 0; x < brushWeights[y].Length; x++)
                {
                    brushWeights[y][x] /= weightSum;
                }
            }
            
            return brushWeights;
        }
        
        private void SimulateDroplet(float[,] heightMap, int width, int height, Func<int, int, bool> shouldModify, 
            int[,] distanceGrid, float maxDistanceValue, float[][] erosionBrushWeights)
        {
            // Initialize droplet at random position (only on modifiable terrain)
            int attempts = 0;
            int posX, posY;
            do
            {
                posX = Random.Range(0, width);
                posY = Random.Range(0, height);
                attempts++;
                
                // Prevent infinite loop if there are no valid positions
                if (attempts > 1000)
                {
                    Debug.LogWarning("Failed to find valid droplet start position after 1000 attempts");
                    return;
                }
            } while (!shouldModify(posX, posY));
            
            float posXf = posX;
            float posYf = posY;
            
            // Initialize droplet properties
            float dirX = 0;
            float dirY = 0;
            float speed = initialSpeed;
            float water = initialWaterVolume;
            float sediment = 0;
            
            // Droplet lifetime simulation
            for (int lifetime = 0; lifetime < maxDropletLifetime; lifetime++)
            {
                // Get droplet position (integer)
                int dropletX = Mathf.FloorToInt(posXf);
                int dropletY = Mathf.FloorToInt(posYf);
                
                // Stop if out of bounds
                if (dropletX < 0 || dropletX >= width - 1 || dropletY < 0 || dropletY >= height - 1)
                {
                    break;
                }
                
                // Calculate droplet offset within this cell (0-1)
                float cellOffsetX = posXf - dropletX;
                float cellOffsetY = posYf - dropletY;
                
                // Calculate heights at corners of this cell
                float heightNW = heightMap[dropletX, dropletY];
                float heightNE = heightMap[dropletX + 1, dropletY];
                float heightSW = heightMap[dropletX, dropletY + 1];
                float heightSE = heightMap[dropletX + 1, dropletY + 1];
                
                // Calculate droplet's current height and gradient with bilinear interpolation
                float gradientX = (heightNE - heightNW) * (1 - cellOffsetY) + (heightSE - heightSW) * cellOffsetY;
                float gradientY = (heightSW - heightNW) * (1 - cellOffsetX) + (heightSE - heightNE) * cellOffsetX;
                
                // Update droplet direction (blend previous direction with gradient)
                dirX = dirX * inertia - gradientX * (1 - inertia);
                dirY = dirY * inertia - gradientY * (1 - inertia);
                
                // Normalize direction
                float len = Mathf.Sqrt(dirX * dirX + dirY * dirY);
                if (len != 0)
                {
                    dirX /= len;
                    dirY /= len;
                }
                
                // Update position (move in water flow direction)
                posXf += dirX;
                posYf += dirY;
                
                // Stop if out of bounds
                if (posXf < 0 || posXf >= width - 1 || posYf < 0 || posYf >= height - 1)
                {
                    break;
                }
                
                // Find the new position's cell
                int newDropletX = Mathf.FloorToInt(posXf);
                int newDropletY = Mathf.FloorToInt(posYf);
                
                // Skip if we can't modify the new position (protected road area)
                // This prevents erosion from affecting roads
                if (!shouldModify(newDropletX, newDropletY))
                {
                    break;
                }
                
                // Calculate new heights at the new position
                float newHeight = CalculateHeightAt(heightMap, width, height, posXf, posYf);
                float oldHeight = CalculateHeightAt(heightMap, width, height, posXf - dirX, posYf - dirY);
                float heightDifference = newHeight - oldHeight;
                
                // Calculate sediment capacity (function of speed, water volume, and slope)
                float sedimentCapacity = Mathf.Max(
                    minSedimentCapacity, 
                    sedimentCapacityFactor * speed * water * Mathf.Abs(heightDifference)
                );
                
                // If moving uphill, deposit a fraction of carried sediment
                if (heightDifference > 0)
                {
                    float depositAmount = Mathf.Min(heightDifference, sediment);
                    sediment -= depositAmount;
                    
                    // Apply deposit
                    ApplyHeightChange(heightMap, width, height, posXf - dirX, posYf - dirY, 
                        depositAmount, shouldModify, distanceGrid, maxDistanceValue, erosionBrushWeights);
                }
                // If carrying more sediment than capacity, deposit the excess
                else if (sediment > sedimentCapacity)
                {
                    float depositAmount = (sediment - sedimentCapacity) * depositSpeed;
                    sediment -= depositAmount;
                    
                    // Apply deposit
                    ApplyHeightChange(heightMap, width, height, posXf, posYf, 
                        depositAmount, shouldModify, distanceGrid, maxDistanceValue, erosionBrushWeights);
                }
                // If carrying less sediment than capacity, erode the surface
                else
                {
                    // Calculate amount to erode
                    float erosionAmount = Mathf.Min(
                        (sedimentCapacity - sediment) * erodeSpeed,
                        -heightDifference // Don't erode more than the height difference
                    );
                    
                    // Apply road influence - reduce erosion near roads
                    float normalizedDistance = distanceGrid[newDropletX, newDropletY] / maxDistanceValue;
                    if (normalizedDistance < roadInfluenceDistance)
                    {
                        // Linear falloff of erosion influence
                        float roadInfluence = normalizedDistance / roadInfluenceDistance;
                        erosionAmount *= roadInfluence * (1 - roadInfluenceMultiplier) + roadInfluenceMultiplier;
                    }
                    
                    // Apply the erosion (negative height change)
                    ApplyHeightChange(heightMap, width, height, posXf, posYf, 
                        -erosionAmount, shouldModify, distanceGrid, maxDistanceValue, erosionBrushWeights);
                    
                    // Add eroded material to carried sediment
                    sediment += erosionAmount;
                }
                
                // Update droplet momentum
                speed = Mathf.Sqrt(speed * speed + heightDifference * gravity);
                
                // Evaporate water
                water *= (1 - evaporationRate);
                
                // Stop simulation if water drops below threshold
                if (water < 0.01f)
                {
                    break;
                }
            }
        }
        
        private float CalculateHeightAt(float[,] heightMap, int width, int height, float x, float y)
        {
            // Bilinear interpolation to get height at exact position
            int x0 = Mathf.FloorToInt(x);
            int y0 = Mathf.FloorToInt(y);
            int x1 = x0 + 1;
            int y1 = y0 + 1;
            
            // Clamp coordinates
            x0 = Mathf.Clamp(x0, 0, width - 1);
            y0 = Mathf.Clamp(y0, 0, height - 1);
            x1 = Mathf.Clamp(x1, 0, width - 1);
            y1 = Mathf.Clamp(y1, 0, height - 1);
            
            // Get fractional parts
            float fx = x - x0;
            float fy = y - y0;
            
            // Get heights at corners
            float h00 = heightMap[x0, y0];
            float h10 = heightMap[x1, y0];
            float h01 = heightMap[x0, y1];
            float h11 = heightMap[x1, y1];
            
            // Bilinear interpolation
            float h0 = h00 * (1 - fx) + h10 * fx;
            float h1 = h01 * (1 - fx) + h11 * fx;
            
            return h0 * (1 - fy) + h1 * fy;
        }
        
        private void ApplyHeightChange(float[,] heightMap, int width, int height, float posX, float posY, 
            float amount, Func<int, int, bool> shouldModify, int[,] distanceGrid, float maxDistanceValue, 
            float[][] erosionBrushWeights)
        {
            int centerX = Mathf.FloorToInt(posX);
            int centerY = Mathf.FloorToInt(posY);
            
            int brushWidth = erosionBrushWeights.Length;
            int radius = brushWidth / 2;
            
            // Apply height change in radius around droplet position
            for (int brushY = 0; brushY < brushWidth; brushY++)
            {
                for (int brushX = 0; brushX < brushWidth; brushX++)
                {
                    int terrainX = centerX + brushX - radius;
                    int terrainY = centerY + brushY - radius;
                    
                    // Skip if out of bounds
                    if (terrainX < 0 || terrainX >= width || terrainY < 0 || terrainY >= height)
                    {
                        continue;
                    }
                    
                    // Skip if we can't modify this point (protected road area)
                    if (!shouldModify(terrainX, terrainY))
                    {
                        continue;
                    }
                    
                    // Apply road influence - reduce changes near roads
                    float brushWeight = erosionBrushWeights[brushY][brushX];
                    float normalizedDistance = distanceGrid[terrainX, terrainY] / maxDistanceValue;
                    if (normalizedDistance < roadInfluenceDistance)
                    {
                        // Linear falloff of change influence
                        float roadInfluence = normalizedDistance / roadInfluenceDistance;
                        brushWeight *= roadInfluence * (1 - roadInfluenceMultiplier) + roadInfluenceMultiplier;
                    }
                    
                    // Apply height change weighted by brush
                    heightMap[terrainX, terrainY] += amount * brushWeight;
                    
                    // Enforce maximum erosion depth if eroding (negative amount)
                    if (amount < 0)
                    {
                        float minHeight = 0; // Minimum height allowed
                        
                        // Find the nearest fixed point (road) to determine minimum height
                        if (normalizedDistance < 0.5f)
                        {
                            // Find neighboring fixed points
                            List<Vector2> neighbours = TerrainUtils.GenerateNeighbours(new Vector2(terrainX, terrainY), width, height);
                            foreach (Vector2 neighbor in neighbours)
                            {
                                int nx = (int)neighbor.x;
                                int ny = (int)neighbor.y;
                                if (!shouldModify(nx, ny))
                                {
                                    // This is a fixed point (road), use its height to constrain erosion
                                    minHeight = Mathf.Max(minHeight, heightMap[nx, ny] - maxErosionDepth);
                                }
                            }
                        }
                        
                        // Enforce minimum height
                        heightMap[terrainX, terrainY] = Mathf.Max(heightMap[terrainX, terrainY], minHeight);
                    }
                }
            }
        }
        
        #endregion
    }
}