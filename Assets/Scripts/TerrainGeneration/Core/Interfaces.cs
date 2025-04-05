using System;
using System.Collections.Generic;
using UnityEngine;

namespace TerrainGeneration.Core
{
    /// <summary>
    /// Interface for all terrain generation algorithms
    /// </summary>
    public interface ITerrainGenerator
    {
        /// <summary>
        /// The name of the terrain generator
        /// </summary>
        string Name { get; }
        
        /// <summary>
        /// Applies the generation algorithm to the provided heightmap
        /// </summary>
        /// <param name="heightMap">The heightmap to modify</param>
        /// <param name="width">Width of the heightmap</param>
        /// <param name="height">Height of the heightmap</param>
        /// <param name="shouldModify">Function that determines if a point should be modified</param>
        void Generate(float[,] heightMap, int width, int height, Func<int, int, bool> shouldModify);
        
        /// <summary>
        /// Creates a copy of the generator with its current configuration
        /// </summary>
        ITerrainGenerator Clone();
    }

    /// <summary>
    /// Interface for all terrain smoothing algorithms
    /// </summary>
    public interface ITerrainSmoother
    {
        /// <summary>
        /// The name of the terrain smoother
        /// </summary>
        string Name { get; }
        
        /// <summary>
        /// Applies the smoothing algorithm to the provided heightmap
        /// </summary>
        /// <param name="heightMap">The heightmap to smooth</param>
        /// <param name="width">Width of the heightmap</param>
        /// <param name="height">Height of the heightmap</param>
        /// <param name="shouldModify">Function that determines if a point should be modified</param>
        /// <param name="distanceGrid">Optional distance grid for distance-based smoothing</param>
        void Smooth(float[,] heightMap, int width, int height, Func<int, int, bool> shouldModify, int[,] distanceGrid = null);
        
        /// <summary>
        /// Indicates whether this smoother requires a distance grid
        /// </summary>
        bool RequiresDistanceGrid { get; }
        
        /// <summary>
        /// Creates a copy of the smoother with its current configuration
        /// </summary>
        ITerrainSmoother Clone();
    }

    /// <summary>
    /// Interface for objects that can be saved to and loaded from a file
    /// </summary>
    public interface ISaveable
    {
        /// <summary>
        /// Saves the object to the specified path
        /// </summary>
        /// <param name="path">The path to save to</param>
        /// <returns>True if the save was successful, false otherwise</returns>
        bool Save(string path);
        
        /// <summary>
        /// Loads the object from the specified path
        /// </summary>
        /// <param name="path">The path to load from</param>
        /// <returns>True if the load was successful, false otherwise</returns>
        bool Load(string path);
    }
}