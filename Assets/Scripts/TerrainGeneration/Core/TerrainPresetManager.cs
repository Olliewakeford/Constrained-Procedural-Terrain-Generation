using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

namespace TerrainGeneration.Core
{
    /// <summary>
    /// Manages saving and loading terrain generation presets to/from the project directory
    /// </summary>
    public class TerrainPresetManager
    {
        /// <summary>
        /// Path to the presets directory within the project
        /// </summary>
        public static string PresetsDirectory => Path.Combine(Application.dataPath, "TerrainPresets");
        
        /// <summary>
        /// Ensures the presets directory exists
        /// </summary>
        public static void EnsureDirectoryExists()
        {
            if (!Directory.Exists(PresetsDirectory))
            {
                Directory.CreateDirectory(PresetsDirectory);
                #if UNITY_EDITOR
                UnityEditor.AssetDatabase.Refresh();
                #endif
            }
        }
        
        /// <summary>
        /// Saves a preset to the project directory
        /// </summary>
        public static bool SavePresetToProject(TerrainGenerationPreset preset, bool overwrite = false)
        {
            EnsureDirectoryExists();
            
            // Sanitize filename
            string fileName = preset.Name.Replace(" ", "_").Replace("/", "_").Replace("\\", "_");
            string filePath = Path.Combine(PresetsDirectory, $"{fileName}.json");
            
            // Check if file exists and we're not overwriting
            if (File.Exists(filePath) && !overwrite)
            {
                Debug.LogError($"Preset file {filePath} already exists. Use overwrite=true to replace it.");
                return false;
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
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to save preset: {e.Message}");
                return false;
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
                
                foreach (string file in files)
                {
                    try
                    {
                        string json = File.ReadAllText(file);
                        PresetSaveData saveData = JsonUtility.FromJson<PresetSaveData>(json);
                        
                        if (saveData != null)
                        {
                            TerrainGenerationPreset preset = ConvertSaveDataToPreset(saveData);
                            // Ensure Generators is never null
                            if (preset.Generators == null)
                            {
                                preset.Generators = new List<ITerrainGenerator>();
                            }
                            presets.Add(preset);
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"Failed to load preset from {file}: {e.Message}");
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
                presetName = preset.Name,
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
    
            foreach (var generatorData in saveData.generators)
            {
                ITerrainGenerator generator = CreateGeneratorFromSaveData(generatorData);
                if (generator != null)
                {
                    preset.Generators.Add(generator);
                }
            }
    
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
        /// Creates a generator instance from save data
        /// </summary>
        private static ITerrainGenerator CreateGeneratorFromSaveData(GeneratorSaveData data)
        {
            try
            {
                Type type = Type.GetType(data.typeName);
                if (type == null)
                {
                    // Try with the assembly-qualified name
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        type = assembly.GetType(data.typeName);
                        if (type != null) break;
                    }
                }
                
                if (type != null)
                {
                    ITerrainGenerator generator = (ITerrainGenerator)Activator.CreateInstance(type);
                    JsonUtility.FromJsonOverwrite(data.jsonData, generator);
                    return generator;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to create generator from save data: {e.Message}");
            }
            
            return null;
        }
        
        /// <summary>
        /// Creates a smoother instance from save data
        /// </summary>
        private static ITerrainSmoother CreateSmootherFromSaveData(SmootherSaveData data)
        {
            try
            {
                Type type = Type.GetType(data.typeName);
                if (type == null)
                {
                    // Try with the assembly-qualified name
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        type = assembly.GetType(data.typeName);
                        if (type != null) break;
                    }
                }
                
                if (type != null)
                {
                    ITerrainSmoother smoother = (ITerrainSmoother)Activator.CreateInstance(type);
                    JsonUtility.FromJsonOverwrite(data.jsonData, smoother);
                    return smoother;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to create smoother from save data: {e.Message}");
            }
            
            return null;
        }
    }
    
    /// <summary>
    /// Serializable structure to save preset data
    /// </summary>
    [Serializable]
    public class PresetSaveData
    {
        public string presetName;
        public List<GeneratorSaveData> generators = new List<GeneratorSaveData>();
        public List<SmootherSaveData> smoothers = new List<SmootherSaveData>();
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