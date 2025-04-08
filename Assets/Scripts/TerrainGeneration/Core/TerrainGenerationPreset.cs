using System;
using System.Collections.Generic;
using UnityEngine;

namespace TerrainGeneration.Core
{
    /// <summary>
    /// Represents a saved configuration for terrain generation including generators and smoothers
    /// </summary>
    [Serializable]
    public class TerrainGenerationPreset
    {
        /// <summary>
        /// Name of the preset
        /// </summary>
        [SerializeField] public string name;

        /// <summary>
        /// List of generators to apply in sequence
        /// </summary>
        public List<ITerrainGenerator> Generators = new();

        /// <summary>
        /// List of smoothers to apply after all generators
        /// </summary>
        public List<ITerrainSmoother> Smoothers = new();

        /// <summary>
        /// Creates a new preset with the specified Name
        /// </summary>
        public TerrainGenerationPreset(string name)
        {
            this.name = name;
            // Always ensure Generators is initialized
            Generators ??= new List<ITerrainGenerator>();

            // Always ensure Smoothers is initialized
            Smoothers ??= new List<ITerrainSmoother>();
        }

        /// <summary>
        /// Creates a deep copy of this preset
        /// </summary>
        public TerrainGenerationPreset Clone()
        {
            TerrainGenerationPreset clone = new TerrainGenerationPreset(name);

            // Clone each generator
            foreach (var generator in Generators)
            {
                clone.Generators.Add(generator.Clone());
            }

            // Clone each smoother
            foreach (var smoother in Smoothers)
            {
                clone.Smoothers.Add(smoother.Clone());
            }

            return clone;
        }
    }
}