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
        [SerializeField] public string Name;

        /// <summary>
        /// List of generators to apply in sequence
        /// </summary>
        [SerializeField] public List<ITerrainGenerator> Generators = new List<ITerrainGenerator>();

        /// <summary>
        /// List of smoothers to apply after all generators
        /// </summary>
        [SerializeField] public List<ITerrainSmoother> Smoothers = new List<ITerrainSmoother>();

        /// <summary>
        /// Creates a new preset with the specified name
        /// </summary>
        public TerrainGenerationPreset(string name)
        {
            Name = name;
            // Always ensure Generators is initialized
            if (Generators == null)
            {
                Generators = new List<ITerrainGenerator>();
            }

            // Always ensure Smoothers is initialized
            if (Smoothers == null)
            {
                Smoothers = new List<ITerrainSmoother>();
            }
        }

        /// <summary>
        /// Creates a deep copy of this preset
        /// </summary>
        public TerrainGenerationPreset Clone()
        {
            TerrainGenerationPreset clone = new TerrainGenerationPreset(Name);

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