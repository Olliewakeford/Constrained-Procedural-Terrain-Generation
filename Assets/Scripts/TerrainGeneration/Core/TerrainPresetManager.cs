using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using TerrainGeneration.Generators;
using TerrainGeneration.SmoothingAndErosion;

namespace TerrainGeneration.Core
{
    /// <summary>
    /// Manages saving and loading terrain generation presets to/from the project directory
    /// </summary>
    public static class TerrainPresetManager
    {
        private static string PresetsDirectory => Path.Combine(Application.dataPath, "TerrainPresets");
        
        private static void EnsureDirectoryExists()
        {
            if (Directory.Exists(PresetsDirectory)) return;
            
            Directory.CreateDirectory(PresetsDirectory);
            #if UNITY_EDITOR
            UnityEditor.AssetDatabase.Refresh();
            #endif
        }
        
        /// <summary>
        /// Saves a preset to the project directory
        /// </summary>
        public static void SavePresetToProject(TerrainGenerationPreset preset, bool overwrite = false)
        {
            EnsureDirectoryExists();
            
            // Clean filename
            string fileName = preset.name.Replace(" ", "_").Replace("/", "_").Replace("\\", "_");
            string filePath = Path.Combine(PresetsDirectory, $"{fileName}.json");
            
            // Check if file exists and we're not overwriting
            if (File.Exists(filePath) && !overwrite)
            {
                Debug.LogError($"Preset file {filePath} already exists. Use overwrite=true to replace it.");
                return;
            }
            
            try
            {
                // Convert preset to serializable form
                PresetSaveData saveData = ConvertPresetToSaveData(preset);
                string json = JsonUtility.ToJson(saveData, true);
                File.WriteAllText(filePath, json);
                
                #if UNITY_EDITOR
                UnityEditor.AssetDatabase.Refresh();
                #endif
                
                Debug.Log($"Preset saved to project at {filePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to save preset: {e.Message}");
            }
        }
        
        /// <summary>
        /// Loads all presets from the project directory
        /// </summary>
        public static List<TerrainGenerationPreset> LoadAllPresetsFromProject()
        {
            EnsureDirectoryExists();
            
            List<TerrainGenerationPreset> presets = new List<TerrainGenerationPreset>();
            
            try
            {
                string[] files = Directory.GetFiles(PresetsDirectory, "*.json");
                Debug.Log($"Found {files.Length} preset files in {PresetsDirectory}");
                
                foreach (string file in files)
                {
                    try
                    {
                        Debug.Log($"Loading preset from: {file}");
                        string json = File.ReadAllText(file);
                        PresetSaveData saveData = JsonUtility.FromJson<PresetSaveData>(json);
                        
                        if (saveData != null)
                        {
                            TerrainGenerationPreset preset = ConvertSaveDataToPreset(saveData);
                            
                            // Ensure collections aren't null
                            preset.Generators ??= new List<ITerrainGenerator>();
                            
                            preset.Smoothers ??= new List<ITerrainSmoother>();
                            
                            Debug.Log($"Loaded preset: {preset.name} with {preset.Generators.Count} generators and {preset.Smoothers.Count} smoothers");
                            presets.Add(preset);
                        }
                        else
                        {
                            Debug.LogError($"Failed to parse JSON from file: {file}");
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Failed to load preset from {file}: {e.Message}");
                    }
                }
                
                Debug.Log($"Loaded {presets.Count} presets from project directory");
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to load presets: {e.Message}");
            }
            
            return presets;
        }
        
        /// <summary>
        /// Converts a preset to serializable data
        /// </summary>
        private static PresetSaveData ConvertPresetToSaveData(TerrainGenerationPreset preset)
        {
            PresetSaveData saveData = new PresetSaveData
            {
                presetName = preset.name,
                generators = new List<GeneratorSaveData>(),
                smoothers = new List<SmootherSaveData>()
            };
            
            foreach (var generator in preset.Generators)
            {
                saveData.generators.Add(ConvertGeneratorToSaveData(generator));
            }
            
            foreach (var smoother in preset.Smoothers)
            {
                saveData.smoothers.Add(ConvertSmootherToSaveData(smoother));
            }
            
            return saveData;
        }
        
        /// <summary>
        /// Converts save data back to a preset
        /// </summary>
        private static TerrainGenerationPreset ConvertSaveDataToPreset(PresetSaveData saveData)
        {
            TerrainGenerationPreset preset = new TerrainGenerationPreset(saveData.presetName);
            
            // Process generators
            foreach (var generatorData in saveData.generators)
            {
                ITerrainGenerator generator = CreateGeneratorFromSaveData(generatorData);
                if (generator != null)
                {
                    preset.Generators.Add(generator);
                }
            }
            
            // Process smoothers
            foreach (var smootherData in saveData.smoothers)
            {
                ITerrainSmoother smoother = CreateSmootherFromSaveData(smootherData);
                if (smoother != null)
                {
                    preset.Smoothers.Add(smoother);
                }
            }
            
            return preset;
        }
        
        /// <summary>
        /// Converts a generator to serializable data
        /// </summary>
        private static GeneratorSaveData ConvertGeneratorToSaveData(ITerrainGenerator generator)
        {
            return new GeneratorSaveData
            {
                typeName = generator.GetType().FullName,
                jsonData = JsonUtility.ToJson(generator)
            };
        }
        
        /// <summary>
        /// Converts a smoother to serializable data
        /// </summary>
        private static SmootherSaveData ConvertSmootherToSaveData(ITerrainSmoother smoother)
        {
            return new SmootherSaveData
            {
                typeName = smoother.GetType().FullName,
                jsonData = JsonUtility.ToJson(smoother)
            };
        }
        
        /// <summary>
        /// Creates a generator instance from save data using explicit type mapping
        /// </summary>
        private static ITerrainGenerator CreateGeneratorFromSaveData(GeneratorSaveData data)
        {
            try
            {
                ITerrainGenerator generator;
                
                // Use explicit type mapping for generators
                switch (data.typeName)
                {
                    case "TerrainGeneration.Generators.UniformHeightGenerator":
                        generator = new UniformHeightGenerator();
                        break;
                    case "TerrainGeneration.Generators.PerlinNoiseGenerator":
                        generator = new PerlinNoiseGenerator();
                        break;
                    case "TerrainGeneration.Generators.VoronoiGenerator":
                        generator = new VoronoiGenerator();
                        break;
                    case "TerrainGeneration.Generators.MidpointDisplacementGenerator":
                        generator = new MidpointDisplacementGenerator();
                        break;
                    default:
                        Debug.LogError($"Unknown generator type: {data.typeName}");
                        return null;
                }
                
                // Deserialize properties
                JsonUtility.FromJsonOverwrite(data.jsonData, generator);
                Debug.Log($"Created generator: {generator.Name}");
                return generator;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to create generator: {e.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Creates a smoother instance from save data using explicit type mapping
        /// </summary>
        private static ITerrainSmoother CreateSmootherFromSaveData(SmootherSaveData data)
        {
            try
            {
                ITerrainSmoother smoother;
                
                // Use explicit type mapping for smoothers
                switch (data.typeName)
                {
                    case "TerrainGeneration.SmoothingAndErosion.BasicSmoother":
                        smoother = new BasicSmoother();
                        break;
                    case "TerrainGeneration.SmoothingAndErosion.EnhancedDistanceSmoother":
                        smoother = new EnhancedDistanceSmoother();
                        break;
                    case "TerrainGeneration.SmoothingAndErosion.HydraulicErosion":
                        smoother = new HydraulicErosion();
                        break;
                    case "TerrainGeneration.SmoothingAndErosion.ThermalErosion":
                        smoother = new ThermalErosion();
                        break;
                    default:
                        Debug.LogError($"Unknown smoother type: {data.typeName}");
                        return null;
                }
                
                // Deserialize properties
                JsonUtility.FromJsonOverwrite(data.jsonData, smoother);
                Debug.Log($"Created smoother: {smoother.Name}");
                return smoother;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to create smoother: {e.Message}");
                return null;
            }
        }
    }
    
    /// <summary>
    /// Serializable structure to save preset data
    /// </summary>
    [Serializable]
    public class PresetSaveData
    {
        public string presetName;
        public List<GeneratorSaveData> generators = new();
        public List<SmootherSaveData> smoothers = new();
    }
    
    /// <summary>
    /// Serializable structure to save generator data
    /// </summary>
    [Serializable]
    public class GeneratorSaveData
    {
        public string typeName;
        public string jsonData;
    }
    
    /// <summary>
    /// Serializable structure to save smoother data
    /// </summary>
    [Serializable]
    public class SmootherSaveData
    {
        public string typeName;
        public string jsonData;
    }
}