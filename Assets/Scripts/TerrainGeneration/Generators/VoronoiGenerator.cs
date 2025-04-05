using System;
using UnityEngine;
using TerrainGeneration.Core;
using TerrainGeneration.Utilities;
using Random = UnityEngine.Random;

namespace TerrainGeneration.Generators
{
    /// <summary>
    /// Generator that creates Voronoi tessellation for mountain/hill generation
    /// </summary>
    [Serializable]
    public class VoronoiGenerator : ITerrainGenerator
    {
        public enum VoronoiType
        {
            Linear,
            Power,
            Combined,
            SinPower,
            Perlin
        }
        
        [SerializeField] private int peakCount = 6;
        [SerializeField] private float fallRate = 1.5f;
        [SerializeField] private float dropOff = 7f;
        [SerializeField] private float minHeight = 0.3f;
        [SerializeField] private float maxHeight = 0.5f;
        [SerializeField] private VoronoiType type = VoronoiType.Combined;
        
        // For Perlin-based Voronoi
        [SerializeField] private float perlinXFrequency = 0.005f;
        [SerializeField] private float perlinYFrequency = 0.005f;
        [SerializeField] private int perlinXOffset = 0;
        [SerializeField] private int perlinYOffset = 0;
        [SerializeField] private int perlinOctaves = 3;
        [SerializeField] private float perlinPersistence = 8f;
        [SerializeField] private float perlinAmplitude = 0.3f;
        
        public string Name => "Voronoi";
        
        public int PeakCount
        {
            get => peakCount;
            set => peakCount = value;
        }
        
        public float FallRate
        {
            get => fallRate;
            set => fallRate = value;
        }
        
        public float DropOff
        {
            get => dropOff;
            set => dropOff = value;
        }
        
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
        
        public VoronoiType Type
        {
            get => type;
            set => type = value;
        }
        
        public void Generate(float[,] heightMap, int width, int height, Func<int, int, bool> shouldModify)
        {
            float voronoiProgress = 0;
            UnityEditor.EditorUtility.DisplayProgressBar("Voronoi Tessellation", "Progress", voronoiProgress);
            
            for (int i = 0; i < peakCount; ++i)
            {
                // Choose a random point for peak
                Vector3 peak = new Vector3(
                    Random.Range(0, width),
                    Random.Range(minHeight, maxHeight),
                    Random.Range(0, height)
                );
                
                if (heightMap[(int)peak.x, (int)peak.z] < peak.y)
                {
                    if (shouldModify((int)peak.x, (int)peak.z))
                    {
                        heightMap[(int)peak.x, (int)peak.z] = peak.y; // Assign the peak height
                    }
                }
                else // Avoid creating divots when peak is lower than surrounding terrain
                {
                    continue;
                }
                
                Vector2 peakLocation = new Vector2(peak.x, peak.z);
                float maxDistance = Vector2.Distance(new Vector2(0, 0), new Vector2(width, height));
                
                for (int z = 0; z < height; z++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        // Skip the peak point itself
                        if (Mathf.Approximately(peak.x, x) && Mathf.Approximately(peak.z, z)) continue;
                        
                        float distanceToPeak = Vector2.Distance(peakLocation, new Vector2(x, z)) / maxDistance;
                        float h;
                        
                        switch (type)
                        {
                            case VoronoiType.Combined:
                                float distanceFactor = distanceToPeak * fallRate;
                                float dropOffFactor = Mathf.Pow(distanceToPeak, dropOff);
                                h = peak.y - distanceFactor - dropOffFactor;
                                break;
                                
                            case VoronoiType.Power:
                                float powerTerm = Mathf.Pow(distanceToPeak, dropOff);
                                h = peak.y - powerTerm * fallRate;
                                break;
                                
                            case VoronoiType.SinPower:
                                float scaledDistance = distanceToPeak * 3.0f;
                                float powTerm = Mathf.Pow(scaledDistance, fallRate);
                                float sinTerm = Mathf.Sin(distanceToPeak * 2.0f * Mathf.PI);
                                h = peak.y - powTerm - sinTerm / dropOff;
                                break;
                                
                            case VoronoiType.Perlin:
                                float perlinX = (x + perlinXOffset) * perlinXFrequency;
                                float perlinY = (z + perlinYOffset) * perlinYFrequency;
                                float fbmValue = TerrainUtils.fBM(perlinX, perlinY, perlinOctaves, perlinPersistence);
                                float perlinContribution = distanceToPeak * fallRate * fbmValue;
                                h = peak.y - perlinContribution * perlinAmplitude;
                                break;
                                
                            default: // Linear
                                float heightDifference = peak.y - distanceToPeak;
                                h = heightDifference * fallRate;
                                break;
                        }
                        
                        if (heightMap[x, z] < h && shouldModify(x, z))
                        {
                            heightMap[x, z] = h;
                        }
                    }
                }
                
                voronoiProgress++;
                UnityEditor.EditorUtility.DisplayProgressBar("Voronoi Tessellation", "Progress", voronoiProgress / peakCount);
            }
            
            UnityEditor.EditorUtility.ClearProgressBar();
        }
        
        public ITerrainGenerator Clone()
        {
            return new VoronoiGenerator
            {
                peakCount = this.peakCount,
                fallRate = this.fallRate,
                dropOff = this.dropOff,
                minHeight = this.minHeight,
                maxHeight = this.maxHeight,
                type = this.type,
                perlinXFrequency = this.perlinXFrequency,
                perlinYFrequency = this.perlinYFrequency,
                perlinXOffset = this.perlinXOffset,
                perlinYOffset = this.perlinYOffset,
                perlinOctaves = this.perlinOctaves,
                perlinPersistence = this.perlinPersistence,
                perlinAmplitude = this.perlinAmplitude
            };
        }
    }
}