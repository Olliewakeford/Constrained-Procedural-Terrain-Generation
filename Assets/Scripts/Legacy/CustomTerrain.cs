using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Random = UnityEngine.Random;

#nullable enable
[ExecuteInEditMode]  // Allows the script to run in the Unity Editor, even when the game is not playing
public class CustomTerrain : MonoBehaviour
{
    //GENERAL VARIABLES
    public Terrain terrain;
    public TerrainData terrainData;
    int Hmr => terrainData.heightmapResolution; // Heightmap resolution
    public bool restoreTerrain = true; // Reset the terrain before applying changes
    public Texture2D mask; // Mask to determine which parts of the terrain to modify
    
    // DISTANCE GRID 
    public int[,] distanceGrid; // Grid storing distances in number of pixels to nearest masked (road) area
    public bool distanceGridCalculated = false; // Flag to check if the distance grid has been calculated
    public string distanceGridSavePath
    {
        get
        {
            // Get the current scene name
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            // Get the terrain object name
            string terrainName = this.gameObject.name;
            // Create a sanitized filename (remove any invalid characters)
            string sanitizedName = string.Join("_", new[] { sceneName, terrainName }
                .Select(s => string.Join("", s.Split(Path.GetInvalidFileNameChars()))));
            
            // Construct the full path
            return Path.Combine("Assets", "Resources", "DistanceGrids", $"{sanitizedName}_distance_grid.dat");
        }
    }
    
    //UNIFORM HEIGHT
    public float uniformStep = 0.1f; // Uniform increment to apply to the terrain height

    //RANDOM HEIGHTS
    public Vector2 randomHeightLimits = new Vector2(0, 0.5f); // Random height range for the terrain
    
    // LOAD HEIGHTMAP
    public Texture2D heightMapTexture; //Greyscale image to load as heightmap
    public Vector3 mapScale = new Vector3(1, 1, 1); // Scale of the heightmap in the 3 dimensions

    //PERLIN NOISE
    // Scale of the perlin noise in the x & y direction (larger values compress the noise pattern)
    public float perlinXFrequency = 0.005f; 
    public float perlinYFrequency = 0.005f; 
    // Horizontal & vertical offset to shift the Perlin noise sample 
    public int perlinXOffset;

    public int perlinYOffset; 
    // Number of noise layers (octaves) stacked to add complexity to the noise pattern
    public int perlinOctaves = 3;
    // Controls how much each successive octave contributes to the final pattern
    public float perlinPersistence = 8; // Higher values add more influence from smaller octaves.
    // Factor to scale the generated height values
    public float perlinAmplitude = 0.3f; // Larger values will create taller terrain features.

    // MULTIPLE PERLIN NOISE
    [System.Serializable]
    public class PerlinOptions
    {
        public float mPerlinXFrequency = 0.005f;
        public float mPerlinYFrequency = 0.005f;
        public int mPerlinXOffset;
        public int mPerlinYOffset;
        public int mPerlinOctaves = 3;
        public float mPerlinPersistence = 8;
        public float mPerlinAmplitude = 0.5f;
        public bool removePerlin;
    }
    public List<PerlinOptions> perlinOptions = new List<PerlinOptions>()
    {
        new PerlinOptions() //defaults with 1 line
    };

    //VORONOI TESSELATION
    public int vtPeakCount = 6; // Number of peaks to generate in the terrain
    // Controls how steeply the terrain falls away from each peak 
    public float vtFallRate = 1.5f; //larger values create steeper slopes
    // Controls how quickly the terrain height drops off around each peak 
    public float vtDropOff = 7f;
    public float vtMinHeight = 0.3f; // The lowest possible height for a peak
    public float vtMaxHeight = 0.5f; // The highest possible height for a peak

    public enum VTType // Different types of functions
    {
        Linear,   
        Power,      
        Combined, // Combination of linear and power functions
        SinPower, // Function with a sin wave effect
        Perlin // Function using Perlin noise for randomness
    }
    public VTType vtType = VTType.Combined; // Default type of function to use

    // MIDPOINT DISPLACEMENT
    public float mdMinHeight = 0.0f; // Minimum depth of a valley 
    public float mdMaxHeight = 1.0f; // Maximum height of a peak
    // Controls how quickly the terrain height changes between peaks and valleys
    public float mdDampenHeight = 3.0f; // higher values produce more gentle terrain
    public float mdRoughnessFactor = 2.0f; // higher values produce more rough and jagged terrain

    // SMOOTHING
    public float baseSmoothing = 1f; // Base smoothing strength (1 = current smoothing level)
    public float distanceFalloff = 0.5f; // How quickly smoothing decreases with distance (higher = faster falloff)
    public bool useDistanceSmoothing = true; // Toggle between uniform and distance-based smoothing
    public int smoothingIterations = 1; // Number of smoothing passes to apply
    
    // ONE CLICK GENERATION:
    [System.Serializable]
    public class GenerationParameters
    {
        // Perlin noise for base terrain
        public float basePerlinXFreq = 0.002f;
        public float basePerlinYFreq = 0.002f;
        public int basePerlinOctaves = 4;
        public float basePerlinPersistence = 2f;
        public float basePerlinAmplitude = 0.2f;
    
        // Additional Perlin layer for detail
        public float detailPerlinXFreq = 0.01f;
        public float detailPerlinYFreq = 0.01f;
        public int detailPerlinOctaves = 2;
        public float detailPerlinPersistence = 4f;
        public float detailPerlinAmplitude = 0.1f;
    
        // Voronoi parameters for mountains/hills
        public int mountainPeakCount = 4;
        public float mountainFallRate = 2f;
        public float mountainDropOff = 5f;
        public float mountainMinHeight = 0.3f;
        public float mountainMaxHeight = 0.5f;
    
        // Smoothing to blend everything
        public int finalSmoothingPasses = 2;
    }
    
    [SerializeField]
    private GenerationParameters genParams = new GenerationParameters();

    //PROCEDURAL TERRAIN GENERATION METHODS--------------------------------------------

    // Method to change the height of the terrain uniformly
    public void ChangeHeightUniformly()
    {
        float[,] heightMap = terrainData.GetHeights(0, 0, Hmr, Hmr); // never reset before
        for (int x = 0; x < Hmr; x++)
        {
            for (int z = 0; z < Hmr; z++)
            {
                if (ShouldModifyTerrain(x, z))
                {
                    heightMap[x, z] += uniformStep;
                }
            }
        }
        terrainData.SetHeights(0, 0, heightMap);
    }

    // Method that applies random heights to the terrain 
    public void RandomTerrain()
    {
        float[,] heightMap = GetHeightMap();
        for (int x = 0; x < Hmr; x++)
        {
            for (int z = 0; z < Hmr; z++)
            {
                if (ShouldModifyTerrain(x, z))
                {
                    heightMap[x, z] += Random.Range(randomHeightLimits.x, randomHeightLimits.y);
                }
            }
                
        }
        terrainData.SetHeights(0, 0, heightMap);
    }

    // Method to load a heightmap image and apply it to the terrain
    public void LoadTexture() 
    {
        float[,] heightMap = GetHeightMap();

        for (int x = 0; x < Hmr; x++)
        {
            for (int z = 0; z < Hmr; z++)
            {
                if (ShouldModifyTerrain(x, z))
                {
                    heightMap[x, z] += heightMapTexture.GetPixel((int)(x * mapScale.x), (int)(z * mapScale.z)).grayscale * mapScale.y;
                }
            }
        }
        terrainData.SetHeights(0, 0, heightMap);
    }

    // Method to generate single Perlin noise and apply it to the terrain
    public void SinglePerlinLayer()
    {
        float[,] heightMap = GetHeightMap();

        for (int y = 0; y < Hmr; y++)
        {
            for (int x = 0; x < Hmr; x++)
            {
                if (ShouldModifyTerrain(x, y)) //only modify the terrain where the mask is black
                {
                    // using fractional Brownian motion to generate Perlin noise
                    heightMap[x, y] += Utils.fBM((x + perlinXOffset) * perlinXFrequency,
                                                (y + perlinYOffset) * perlinYFrequency,
                                                perlinOctaves,
                                                perlinPersistence) * perlinAmplitude;
                }
            }
        }

        terrainData.SetHeights(0, 0, heightMap);

    }

    // Method to generate multiple Perlin noise layers and apply them to the terrain
    public void MultiplePerlinTerrain()
    {
        float[,] heightMap = GetHeightMap();
        int totalIterations = Hmr * Hmr * perlinOptions.Count;
        float perlinProgress = 0;
        //shows a progress bar in the editor
        EditorUtility.DisplayProgressBar("Multiple Perlin Terrain", "Progress", perlinProgress);

        for (int y = 0; y < Hmr; y++)
        {
            for (int x = 0; x < Hmr; x++)
            {
                foreach (PerlinOptions p in perlinOptions)
                {
                    if (ShouldModifyTerrain(x, y))
                    {
                        heightMap[x, y] += Utils.fBM((x + p.mPerlinXOffset) * p.mPerlinXFrequency,
                                                    (y + p.mPerlinYOffset) * p.mPerlinYFrequency,
                                                    p.mPerlinOctaves,
                                                    p.mPerlinPersistence) * p.mPerlinAmplitude;
                    }
                    perlinProgress++;
                    EditorUtility.DisplayProgressBar("Multiple Perlin Terrain", "Progress", perlinProgress / totalIterations);
                }
            }
        }

        terrainData.SetHeights(0, 0, heightMap);
        EditorUtility.ClearProgressBar();

    }

    public void AddNewPerlin() //adds a new perlin noise to the list
    {
        perlinOptions.Add(new PerlinOptions());
    }

    public void RemovePerlin() // removes the selected perlin noise from the list
    {
        List<PerlinOptions> keptPerlinOptions = new List<PerlinOptions>();
        foreach (var t in perlinOptions)
        {
            if (!t.removePerlin)
            {
                keptPerlinOptions.Add(t);
            }
        }
        if (keptPerlinOptions.Count == 0)
        {
            keptPerlinOptions.Add(perlinOptions[0]); //have to have at least 1
        }
        perlinOptions = keptPerlinOptions;
    }

    public void RidgeNoise()  //creates a ridge-like terrain with the right parameters
    {
        RestoreTerrain();
        MultiplePerlinTerrain();
        float[,] heightMap = GetHeightMap();

        for (int y = 0; y < Hmr; y++)
        {
            for (int x = 0; x < Hmr; x++)
            {
                float originalValue = heightMap[x, y];
                float ridgeValue = 1 - Mathf.Abs(originalValue - 0.5f); // Creates a ridge-like structure
                //ridgeValue *= ridgeValue; // Optionally square to sharpen the ridges
                if (ShouldModifyTerrain(x, y))
                {
                    heightMap[x, y] = ridgeValue; // Assign the modified value back to the heightMap
                }
            }
        }

        terrainData.SetHeights(0, 0, heightMap);
    }

    // Method to generate Voronoi tesselation and apply it to the terrain
    public void Voronoi()
    {
        float[,] heightMap = GetHeightMap();
        float voronoiProgress = 0;

        // Progress bar initialization
        EditorUtility.DisplayProgressBar("Voronoi Tesselation", "Progress", voronoiProgress);

        for (int i = 0; i < vtPeakCount; ++i)
        {
            //Choose a random point:
            Vector3 peak = new Vector3(Random.Range(0, Hmr), Random.Range(vtMinHeight, vtMaxHeight), Random.Range(0, Hmr));

            if (heightMap[(int)peak.x, (int)peak.z] < peak.y)
            {
                if (ShouldModifyTerrain((int)peak.x, (int)peak.z))
                {
                    heightMap[(int)peak.x, (int)peak.z] = peak.y; //assign the peak height
                }
            }
            else //avoid creating divots when peak is lower than the surrounding terrain
            {
                continue;
            }

            Vector2 peakLocation = new Vector2(peak.x, peak.z);
            float maxDistance = Vector2.Distance(new Vector2(0, 0), new Vector2(Hmr, Hmr));
            for (int y = 0; y < Hmr; y++)
            {
                for (int x = 0; x < Hmr; x++)
                {

                    // Approximately is used because we are comparing floats and they can have rounding errors
                    if (Mathf.Approximately(peak.x, x) && Mathf.Approximately(peak.z, y)) continue; //the peak
                    
                    float distanceToPeak = Vector2.Distance(peakLocation, new Vector2(x, y)) / maxDistance;
                    float h;
                    if (vtType == VTType.Combined)
                    {
                        float distanceFactor = distanceToPeak * vtFallRate;
                        float dropOffFactor = Mathf.Pow(distanceToPeak, vtDropOff);
                        h = peak.y - distanceFactor - dropOffFactor;
                    }

                    else if (vtType == VTType.Power)
                    {
                        float powerTerm = Mathf.Pow(distanceToPeak, vtDropOff);
                        h = peak.y - powerTerm * vtFallRate;
                    }

                    else if (vtType == VTType.SinPower)
                    {
                        float scaledDistance = distanceToPeak * 3.0f;
                        float powerTerm = Mathf.Pow(scaledDistance, vtFallRate);
                        float sinTerm = Mathf.Sin(distanceToPeak * 2.0f * Mathf.PI);
                        h = peak.y - powerTerm - sinTerm / vtDropOff;
                    }

                    else if (vtType == VTType.Perlin)
                    {
                        float perlinX = (x + perlinXOffset) * perlinXFrequency;
                        float perlinY = (y + perlinYOffset) * perlinYFrequency;
                        float fbmValue = Utils.fBM(perlinX, perlinY, perlinOctaves, perlinPersistence);
                        float perlinContribution = distanceToPeak * vtFallRate * fbmValue;
                        h = peak.y - perlinContribution * perlinAmplitude;
                    }

                    else // Linear
                    {
                        float heightDifference = peak.y - distanceToPeak;
                        h = heightDifference * vtFallRate;
                    }

                    if (heightMap[x, y] < h && ShouldModifyTerrain(x, y))
                    {
                        heightMap[x, y] = h;
                    }

                }
            }
            voronoiProgress++;
            EditorUtility.DisplayProgressBar("Voronoi Tesselation", "Progress", voronoiProgress / vtPeakCount);
        }

        terrainData.SetHeights(0, 0, heightMap);
        EditorUtility.ClearProgressBar();
    }
    
    // Method to apply the Midpoint Displacement algorithm to the terrain
    public void MidpointDisplacement()
    {
        float[,] heightMap = GetHeightMap();
        int width = Hmr - 1; //-1 to make square size a power of 2 (needed for the algorithm)
        int squareSize = width;

        
        // Estimate the total number of iterations based on how many times squareSize is halved
        int totalIterations = (int)(Mathf.Log(squareSize, 2)) + 1; // Total number of halving steps
        int mdProgress = 0; // Track current iteration progress

        // Progress bar initialization
        EditorUtility.DisplayProgressBar("Midpoint Displacement", "Progress", 0);


        // local variables to avoid changing the globally displayed variable
        float minHeight = mdMinHeight; 
        float maxHeight = mdMaxHeight; 
        float heightDampener = Mathf.Pow(mdDampenHeight, -1 * mdRoughnessFactor);
        

        /*COORDINATE SYSTEM:
        (x, y) is the coordinate of the bottom left corner
        (cornerX, y) is the bottom right corner
        (x, cornerY) is the top left corner
        (cornerX, cornerY) is the top right corner
        (midX, midY) is the middle point of the square
        (pmidXL, midY) is one square length to the left of the middle point
        (pmidXR, midY) is one square length to the right of the middle point
        (midX, pmidYU) is one square length above the middle point
        (midX, pmidYD) is one square length below the middle point */

        int cornerX, cornerY;
        int midX, midY;
        int pmidXL, pmidXR, pmidYU, pmidYD;
        
        while (squareSize > 0)
        {
            int halfSquareSize = (int)(squareSize / 2.0f);
            // Square step of the algorithm
            
            for (int x = 0; x < width; x += squareSize)
            {
                for (int y = 0; y < width; y += squareSize)
                {
                    cornerX = x + squareSize;
                    cornerY = y + squareSize;

                    // Calculate the center of the square based on x and y coordinates
                    midX = (x + halfSquareSize);
                    midY = (y + halfSquareSize);

                    if (!ShouldModifyTerrain(midX, midY)) 
                    {
                        continue;
                    }
                    
                    // Set the height of the middle point of the square 
                    // to the average of the corner heights plus a random value
                    heightMap[midX, midY] = ((heightMap[x, y] + 
                                              heightMap[cornerX, y] +
                                              heightMap[x, cornerY] +
                                              heightMap[cornerX, cornerY]) / 4.0f +
                                              Random.Range(minHeight, maxHeight));
                }
            }

            // Diamond step of the algorithm
            for (int x = 0; x < width; x += squareSize)
            {
                for (int y = 0; y < width; y += squareSize)
                {
                    cornerX = x + squareSize;
                    cornerY = y + squareSize;

                    midX = (x + halfSquareSize);
                    midY = (y + halfSquareSize);

                    pmidYU = midY + squareSize; 
                    pmidXR = midX + squareSize; 
                    pmidYD = midY - squareSize;
                    pmidXL = midX - squareSize;

                    // Don't change height of out of bounds areas
                    if (pmidXL <= 0 || pmidYD <= 0 || pmidXR >= width - 1 || pmidYU >= width - 1) 
                    {
                        continue;
                    }

                    // Compute the height of the midpoints on the edges of the square
                    // Average the heights of neighboring points, then add some random variation

                    if (ShouldModifyTerrain(midX, cornerY))
                    {
                        float randomVariationTop = Random.Range(minHeight, maxHeight);
                        // Set the height of the midpoint on the top edge of the square
                        heightMap[midX, cornerY] = (heightMap[x, cornerY] +
                                                    heightMap[cornerX, cornerY] +
                                                    heightMap[midX, midY] +
                                                    heightMap[midX, pmidYU]) / 4.0f +
                                                    randomVariationTop;
                                                            
                    }

                    if (ShouldModifyTerrain(cornerX, midY))
                    {
                        float randomVariationRight = Random.Range(minHeight, maxHeight);
                        // Set the height of the midpoint on the right edge of the square
                        heightMap[cornerX, midY] = (heightMap[cornerX, y] +
                                                    heightMap[cornerX, cornerY] +
                                                    heightMap[midX, midY] +
                                                    heightMap[pmidXR, midY]) / 4.0f +
                                                    randomVariationRight;
                    }
                    if (ShouldModifyTerrain(midX, y))
                    {
                        float randomVariationBottom = Random.Range(minHeight, maxHeight);
                        // Set the height of the midpoint on the bottom edge of the square
                        heightMap[midX, y] = (heightMap[x, y] +
                                                heightMap[cornerX, y] +
                                                heightMap[midX, midY] +
                                                heightMap[midX, pmidYD]) / 4.0f +
                                                randomVariationBottom;
                    }

                    if (ShouldModifyTerrain(x, midY))
                    {
                        float randomVariationLeft = Random.Range(minHeight, maxHeight);
                        // Set the height of the midpoint on the left edge of the square
                        heightMap[x, midY] = (heightMap[x, y] +
                                                heightMap[x, cornerY] +
                                                heightMap[midX, midY] +
                                                heightMap[pmidXL, midY]) / 4.0f +
                                                randomVariationLeft;
                    }

                }

            }

            minHeight *= heightDampener;
            maxHeight *= heightDampener;
            squareSize = (int)(squareSize / 2.0f);

            // Increment the iteration counter and update the progress bar
            mdProgress++;
            EditorUtility.DisplayProgressBar("Midpoint Displacement", "Progress", (float)mdProgress / totalIterations);
        }

        terrainData.SetHeights(0, 0, heightMap);
        EditorUtility.ClearProgressBar();
    }


    //ADDITIONAL METHODS--------------------------------------------
    
    // Method to calculate distances to nearest masked area
    public void BasicSmooth() 
    {
        float[,] heightMap = terrainData.GetHeights(0, 0, Hmr, Hmr); // never reset before smoothing
        float smoothProgress = 0;
        //shows a progress bar in the editor
        EditorUtility.DisplayProgressBar("Smoothing Terrain", "Progress", smoothProgress); 
        for (int i = 0; i < smoothingIterations; i++)
        {
            for (int y = 0; y < Hmr; y++)
            {
                for (int x = 0; x < Hmr; x++)
                {
                    float avgHeight = heightMap[x, y];
                    List<Vector2> neighbours = GenerateNeighbours(new Vector2(x, y), Hmr, Hmr);
                    foreach (Vector2 n in neighbours)
                    {
                        avgHeight += heightMap[(int)n.x, (int)n.y];
                    }
                
                    // Set the height of the current point to the average height of itself and its neighbours
                    if (ShouldModifyTerrain(x, y)){
                        heightMap[x, y] = avgHeight / ((float)neighbours.Count + 1);
                    }
                }
            }
            smoothProgress++;
            EditorUtility.DisplayProgressBar("Smoothing Terrain", "Progress", smoothProgress / smoothingIterations);
        }
        terrainData.SetHeights(0, 0, heightMap);
        EditorUtility.ClearProgressBar();
    }
    
    // Method to apply smoothing to the terrain based on distance from the road
   public void DistanceBasedSmooth()
   {
       // Ensure we have a valid distance grid
       if (distanceGrid == null)
       {
           if (!TryLoadDistanceGrid())
           {
               Debug.LogError("Distance grid not found. Please calculate the distance grid first.");
               return;
           }
       }
   
       // Find the maximum distance value for normalization
       float maxDistanceValue = 0;
       for (int x = 0; x < Hmr; x++)
       {
           for (int y = 0; y < Hmr; y++)
           {
               if (distanceGrid[x, y] != int.MaxValue && distanceGrid[x, y] > maxDistanceValue)
               {
                   maxDistanceValue = distanceGrid[x, y];
               }
           }
       }
   
       if (maxDistanceValue == 0)
       {
           Debug.LogError("Invalid distance grid: no valid distances found");
           return;
       }
   
       float[,] heightMap = terrainData.GetHeights(0, 0, Hmr, Hmr);
       float smoothProgress = 0;
       int totalIterations = smoothingIterations * Hmr * Hmr;
   
       // Show progress bar in Unity Editor
       EditorUtility.DisplayProgressBar("Distance-Based Smoothing", "Progress", smoothProgress);
   
       for (int iteration = 0; iteration < smoothingIterations; iteration++)
       {
           // Create a copy of the height map to store the smoothed values
           float[,] smoothedHeightMap = new float[Hmr, Hmr];
           System.Array.Copy(heightMap, smoothedHeightMap, heightMap.Length);
   
           for (int y = 0; y < Hmr; y++)
           {
               for (int x = 0; x < Hmr; x++)
               {
                   if (!ShouldModifyTerrain(x, y)) continue;
   
                   // Normalize the distance to [0,1] range
                   float normalizedDistance = distanceGrid[x, y] / maxDistanceValue;
                   
                   // Calculate smoothing strength based on normalized distance
                   // distanceFalloff now controls both the steepness and the cutoff point
                   float smoothingFactor = baseSmoothing * Mathf.Pow(1 - normalizedDistance, distanceFalloff);
   
                   if (smoothingFactor < 0.01f) continue; // Skip if smoothing effect would be negligible
   
                   // Get neighboring heights and calculate weighted average
                   List<Vector2> neighbours = GenerateNeighbours(new Vector2(x, y), Hmr, Hmr);
                   float totalWeight = smoothingFactor;
                   float smoothedHeight = heightMap[x, y] * smoothingFactor;
   
                   foreach (Vector2 n in neighbours)
                   {
                       int nx = (int)n.x;
                       int ny = (int)n.y;
                       
                       // Calculate normalized distance for neighbor
                       float neighborNormalizedDistance = distanceGrid[nx, ny] / maxDistanceValue;
                       float neighborWeight = smoothingFactor * Mathf.Pow(1 - neighborNormalizedDistance, distanceFalloff);
                       
                       totalWeight += neighborWeight;
                       smoothedHeight += heightMap[nx, ny] * neighborWeight;
                   }
   
                   // Apply weighted average
                   if (totalWeight > 0)
                   {
                       smoothedHeightMap[x, y] = smoothedHeight / totalWeight;
                   }
   
                   // Update progress
                   smoothProgress++;
                   if (smoothProgress % 1000 == 0) // Update progress bar every 1000 pixels
                   {
                       EditorUtility.DisplayProgressBar("Distance-Based Smoothing", 
                           $"Iteration {iteration + 1}/{smoothingIterations}", 
                           smoothProgress / totalIterations);
                   }
               }
           }
   
           // Update height map for next iteration
           heightMap = smoothedHeightMap;
       }
   
       // Apply the final smoothed heights to the terrain
       terrainData.SetHeights(0, 0, heightMap);
       EditorUtility.ClearProgressBar();
   }
    
    // Method to set the distance grid of the terrain, where each cell is the minimum distance to nearest masked pixel
    public void CalculateDistanceGrid()
    {
        // Check if we can load from saved file
        if (TryLoadDistanceGrid())
        {
            Debug.Log("Loaded pre-calculated distance grid from file");
            return;
        }

        distanceGrid = new int[Hmr, Hmr];
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        int totalPixels = Hmr * Hmr;
        int processedPixels = 0;
        
        EditorUtility.DisplayProgressBar("Calculating Distance Grid", "Initializing grid...", 0f);
        
        // First pass: Initialize and find road pixels
        for (int x = 0; x < Hmr; x++)
        {
            for (int z = 0; z < Hmr; z++)
            {
                if (!ShouldModifyTerrain(x, z))
                {
                    distanceGrid[x, z] = 0;
                    queue.Enqueue(new Vector2Int(x, z));
                }
                else
                {
                    distanceGrid[x, z] = int.MaxValue;
                }
                processedPixels++;
                if (processedPixels % 1000 == 0) // Update progress less frequently
                {
                    EditorUtility.DisplayProgressBar("Calculating Distance Grid", 
                        "Initializing grid...", 
                        processedPixels / (float)totalPixels * 0.2f); // First 20% of progress
                }
            }
        }
        
        // Second pass: BFS
        int initialQueueSize = queue.Count;
        int processed = 0;
        
        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            int currentDist = distanceGrid[current.x, current.y];
            
            foreach (var offset in neighbors)
            {
                int newX = current.x + offset.x;
                int newY = current.y + offset.y;
                
                if (newX < 0 || newX >= Hmr || newY < 0 || newY >= Hmr) continue;
                
                if (distanceGrid[newX, newY] <= currentDist + 1) continue;
                distanceGrid[newX, newY] = currentDist + 1;
                queue.Enqueue(new Vector2Int(newX, newY));
            }
            
            processed++;
            if (processed % 1000 == 0) // Update progress less frequently
            {
                float progress = 0.2f + (processed / (float)initialQueueSize * 0.8f); // Remaining 80% of progress
                EditorUtility.DisplayProgressBar("Calculating Distance Grid", 
                    $"Processing pixels... ({processed}/{initialQueueSize})", 
                    progress);
            }
        }
        
        EditorUtility.DisplayProgressBar("Calculating Distance Grid", "Saving results...", 1f);
        SaveDistanceGrid();
        distanceGridCalculated = true;
        
        EditorUtility.ClearProgressBar();
        Debug.Log("Distance grid calculation completed and saved");
    }
    
    
    private static readonly Vector2Int[] neighbors = new Vector2Int[]
    {
        new Vector2Int(-1, -1), new Vector2Int(-1, 0), new Vector2Int(-1, 1),
        new Vector2Int(0, -1),                         new Vector2Int(0, 1),
        new Vector2Int(1, -1),  new Vector2Int(1, 0),  new Vector2Int(1, 1)
    };

    private bool TryLoadDistanceGrid()
    {
        if (!File.Exists(distanceGridSavePath)) return false;
        
        try
        {
            using (BinaryReader reader = new BinaryReader(File.Open(distanceGridSavePath, FileMode.Open)))
            {
                // Read and verify dimensions
                int savedWidth = reader.ReadInt32();
                int savedHeight = reader.ReadInt32();
                
                if (savedWidth != Hmr || savedHeight != Hmr)
                {
                    Debug.LogWarning("Saved distance grid dimensions don't match current terrain");
                    return false;
                }
                
                // Read the grid data
                distanceGrid = new int[Hmr, Hmr];
                for (int x = 0; x < Hmr; x++)
                {
                    for (int z = 0; z < Hmr; z++)
                    {
                        distanceGrid[x, z] = reader.ReadInt32();
                    }
                }
                
                distanceGridCalculated = true;
                return true;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error loading distance grid: {e.Message}");
            return false;
        }
    }

    private void SaveDistanceGrid()
    {
        try
        {
            // Create directory if it doesn't exist
            EnsureDirectoryExists();
        
            using (BinaryWriter writer = new BinaryWriter(File.Open(distanceGridSavePath, FileMode.Create)))
            {
                // Write the grid data
                for (int x = 0; x < Hmr; x++)
                {
                    for (int z = 0; z < Hmr; z++)
                    {
                        writer.Write(distanceGrid[x, z]);
                    }
                }
            }
        
            Debug.Log($"Distance grid saved to: {distanceGridSavePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Error saving distance grid: {e.Message}");
        }
    }
    
    private void EnsureDirectoryExists()
    {
        string directory = Path.GetDirectoryName(distanceGridSavePath);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
    
    public void VisualizeDistanceGrid()
    {
        if (distanceGrid == null)
        {
            if (!TryLoadDistanceGrid())
            {
                Debug.LogError("No distance grid data available to visualize.");
                return;
            }
        }

        string visualizationPath = Path.Combine(
            Path.GetDirectoryName(distanceGridSavePath),
            Path.GetFileNameWithoutExtension(distanceGridSavePath) + "_visualization.png"
        );

        DistanceGridVisualizer.CreateVisualization(distanceGrid, visualizationPath);
    }

    // Function to generate a list of neighbouring points around a given position
    List<Vector2> GenerateNeighbours(Vector2 pos, int width, int height)
    {
        List<Vector2> neighbours = new List<Vector2>();
        
        // Loop through a 3x3 grid centered on the given position (pos)
        // y = -1, 0, 1 and x = -1, 0, 1 represent relative positions around the central point
        for (int y = -1; y < 2; y++)
        {
            for (int x = -1; x < 2; x++)
            {
                if (!(x == 0 && y == 0))
                {   
                    Vector2 neighbourPos = new Vector2(
                        Mathf.Clamp(pos.x + x, 0, width - 1),  // Clamp x to be within [0, width - 1]
                        Mathf.Clamp(pos.y + y, 0, height - 1)); // Clamp y to be within [0, height - 1]
                
                    if (!neighbours.Contains(neighbourPos))
                    {
                        neighbours.Add(neighbourPos);
                    }
                }
            }
        }
        return neighbours;
    }

    // Function to check if a terrain point should be modified based on the mask
    bool ShouldModifyTerrain(int x, int y)
    {
        // Normalize coordinates to map to mask resolution
        float normX = y / (float)Hmr; // Swap y to x for rotation
        float normY = x / (float)Hmr; // Swap x to y for rotation

        // Get the corresponding pixel color in the mask
        Color maskColor = mask.GetPixelBilinear(normX, normY);

        // Only modify terrain where the mask is black (or a threshold of darkness)
        return maskColor is { r: < 0.1f, g: < 0.1f, b: < 0.1f };
    }
    

    // Function to get the current or reset heightmap
    private float[,] GetHeightMap()
    {
        // Get the current heightmap
        float[,] currentHeightMap = terrainData.GetHeights(0, 0, Hmr, Hmr);

        if (!restoreTerrain)
        {
            // Return the current heightmap if not resetting
            return currentHeightMap;
        }
        else
        {
            // Create a new heightmap to modify
            float[,] modifiedHeightMap = new float[Hmr, Hmr];

            // Iterate over each point in the heightmap
            for (int y = 0; y < Hmr; y++)
            {
                for (int x = 0; x < Hmr; x++)
                {
                    // Check if this point should be modified based on the mask
                    if (!ShouldModifyTerrain(x, y))
                    {
                        // If it should be modified, retain the original height
                        modifiedHeightMap[x, y] = currentHeightMap[x, y];
                    }
                    else
                    {
                        // Otherwise, set the height to 0
                        modifiedHeightMap[x, y] = 0;
                    }
                }
            }

            return modifiedHeightMap;
        }
    }

    // Method to reset the terrain so roads stay original height and modifiable areas are set to 0
    public void RestoreTerrain()
    {
        // Create a new heightmap with zero heights
        float[,] resetHeightMap = new float[Hmr, Hmr];

        // Get the current heightmap
        float[,] currentHeightMap = terrainData.GetHeights(0, 0, Hmr, Hmr);

        // Iterate over each point in the heightmap
        for (int y = 0; y < Hmr; y++)
        {
            for (int x = 0; x < Hmr; x++)
            {
                // Check if this point should be modified based on the mask
                if (ShouldModifyTerrain(x, y))
                {
                    // If it should be modified, set height to 0
                    resetHeightMap[x, y] = 0;
                }
                else
                {
                    // Otherwise, keep the current height
                    resetHeightMap[x, y] = currentHeightMap[x, y];
                }
            }
        }

        // Apply the reset heightmap to the terrain
        terrainData.SetHeights(0, 0, resetHeightMap);
    }
    
    public void Generate()
    {
        // Start with a clean slate
        RestoreTerrain();
    
        // Apply base Perlin noise for overall terrain shape
        perlinXFrequency = genParams.basePerlinXFreq;
        perlinYFrequency = genParams.basePerlinYFreq;
        perlinOctaves = genParams.basePerlinOctaves;
        perlinPersistence = genParams.basePerlinPersistence;
        perlinAmplitude = genParams.basePerlinAmplitude;
        SinglePerlinLayer();
    
        // Add detail layer with higher frequency Perlin noise
        perlinXFrequency = genParams.detailPerlinXFreq;
        perlinYFrequency = genParams.detailPerlinYFreq;
        perlinOctaves = genParams.detailPerlinOctaves;
        perlinPersistence = genParams.detailPerlinPersistence;
        perlinAmplitude = genParams.detailPerlinAmplitude;
        restoreTerrain = false; // Don't reset terrain between operations
        SinglePerlinLayer();
    
        // Add mountains/hills using Voronoi
        vtPeakCount = genParams.mountainPeakCount;
        vtFallRate = genParams.mountainFallRate;
        vtDropOff = genParams.mountainDropOff;
        vtMinHeight = genParams.mountainMinHeight;
        vtMaxHeight = genParams.mountainMaxHeight;
        vtType = VTType.Combined;
        Voronoi();
    
        // Final smoothing pass to blend everything together
        smoothingIterations = genParams.finalSmoothingPasses;
        BasicSmooth();
    
        // Reset restore terrain flag
        restoreTerrain = true;
    }

    private void OnEnable()
    {
        Debug.Log("Initializing Terrain Data");
        if (terrain == null)
        {
            terrain = GetComponent<Terrain>();
        }
        if (terrain != null)
        {
            terrainData = terrain.terrainData;
        }
        else
        {
            Debug.LogError("Terrain component not found on this GameObject!");
        }
    }

    void Start()
    {
        Debug.Log("Start called");
        
        // Access the Tag Manager asset to modify or add new tags programmatically
        SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty tagsProp = tagManager.FindProperty("tags");

        // Add custom tags to the project (if they don't already exist)
        AddTag(tagsProp, "Terrain");

        // Apply the changes made to the Tag Manager
        tagManager.ApplyModifiedProperties();

        // Assign the "Terrain" tag to the GameObject that this script is attached to
        this.gameObject.tag = "Terrain";

    }
    
    

    // Helper method to add a tag to the Tag Manager if it doesn't already exist
    void AddTag(SerializedProperty tagsProp, string newTag)
    {
        // Check if the tag already exists in the Tag Manager
        bool found = false;
        for (int i = 0; i < tagsProp.arraySize; i++)
        {
            SerializedProperty t = tagsProp.GetArrayElementAtIndex(i);
            if (t.stringValue.Equals(newTag))
            {
                found = true;
                break;
            }
        }

        // If the tag doesn't exist, add it to the Tag Manager
        if (!found)
        {
            // Insert a new element in the array and set its value to the new tag
            tagsProp.InsertArrayElementAtIndex(0);
            SerializedProperty newTagProp = tagsProp.GetArrayElementAtIndex(0);
            newTagProp.stringValue = newTag;
        }
    }
}
