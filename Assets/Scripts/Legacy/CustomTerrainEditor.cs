using System.IO;
using UnityEditor;
using UnityEngine;
using EditorGUITable;  // A custom library for creating tables in Unity Editor 

// Suppress warning to make private fields start with "_"

// Specifies that this custom editor is for the CustomTerrain class
[CustomEditor(typeof(CustomTerrain))]
public class CustomTerrainEditor : Editor
{
    //properties to be synced with the CustomTerrain class
    SerializedProperty terrainProperty;
    SerializedProperty maskProperty;
    SerializedProperty distanceGridCalculated;
    SerializedProperty uniformStep;
    SerializedProperty randomHeightLimits;
    SerializedProperty restoreTerrain;
    SerializedProperty mapScale;
    SerializedProperty heightMapTexture;
    SerializedProperty perlinXFrequency;
    SerializedProperty perlinYFrequency;
    SerializedProperty perlinXOffset;
    SerializedProperty perlinYOffset;
    SerializedProperty perlinOctaves;
    SerializedProperty perlinPersistence;
    SerializedProperty perlinAmplitude;
    GUITableState perlinOptionsTable;
    SerializedProperty perlinOptions;
    SerializedProperty vtPeakCount;
    SerializedProperty vtFallRate;
    SerializedProperty vtDropOff;
    SerializedProperty vtMinHeight;
    SerializedProperty vtMaxHeight;
    SerializedProperty vtType;
    SerializedProperty mdMinHeight;
    SerializedProperty mdMaxHeight;
    SerializedProperty mdRoughnessFactor;
    SerializedProperty mdDampenHeight;
    SerializedProperty genParams;
    SerializedProperty baseSmoothing;
    SerializedProperty distanceFalloff;
    SerializedProperty useDistanceSmoothing;
    SerializedProperty smoothingIterations;


    // Booleans to control the visibility of the foldout sections in the inspector
    bool showGenerate;
    bool showUniform;
    bool showRandom;
    bool showLoadHeights;
    bool showPerlinNoise;
    bool showMultiplePerlin;
    bool showVoronoi;
    bool showMidpointDisplacement;
    bool showSmooth;
    bool showDistanceGrid;

    private void OnEnable()
    {
        // Initialize the serialized properties to link them with the CustomTerrain class properties
        terrainProperty = serializedObject.FindProperty("terrain");
        maskProperty = serializedObject.FindProperty("mask");
        distanceGridCalculated = serializedObject.FindProperty("distanceGridCalculated");
        uniformStep = serializedObject.FindProperty("uniformStep");
        randomHeightLimits = serializedObject.FindProperty("randomHeightLimits");
        restoreTerrain = serializedObject.FindProperty("restoreTerrain");
        mapScale = serializedObject.FindProperty("mapScale");
        heightMapTexture = serializedObject.FindProperty("heightMapTexture");
        perlinXFrequency = serializedObject.FindProperty("perlinXFrequency");
        perlinYFrequency = serializedObject.FindProperty("perlinYFrequency");
        perlinXOffset = serializedObject.FindProperty("perlinXOffset");
        perlinYOffset = serializedObject.FindProperty("perlinYOffset");
        perlinOctaves = serializedObject.FindProperty("perlinOctaves");
        perlinPersistence = serializedObject.FindProperty("perlinPersistence");
        perlinAmplitude = serializedObject.FindProperty("perlinAmplitude");
        perlinOptionsTable = new GUITableState("perlinOptionsTable");
        perlinOptions = serializedObject.FindProperty("perlinOptions");
        vtPeakCount = serializedObject.FindProperty("vtPeakCount");
        vtFallRate = serializedObject.FindProperty("vtFallRate");
        vtDropOff = serializedObject.FindProperty("vtDropOff");
        vtMinHeight = serializedObject.FindProperty("vtMinHeight");
        vtMaxHeight = serializedObject.FindProperty("vtMaxHeight");
        vtType = serializedObject.FindProperty("vtType");
        mdMinHeight = serializedObject.FindProperty("mdMinHeight");
        mdMaxHeight = serializedObject.FindProperty("mdMaxHeight");
        mdRoughnessFactor = serializedObject.FindProperty("mdRoughnessFactor");
        mdDampenHeight = serializedObject.FindProperty("mdDampenHeight");
        baseSmoothing = serializedObject.FindProperty("baseSmoothing");
        distanceFalloff = serializedObject.FindProperty("distanceFalloff");
        useDistanceSmoothing = serializedObject.FindProperty("useDistanceSmoothing");
        smoothingIterations = serializedObject.FindProperty("smoothingIterations");
        genParams = serializedObject.FindProperty("genParams"); 
    }

    // This method overrides the default inspector GUI with a custom one
    public override void OnInspectorGUI()
    {
        // Update the serialized object's representation to ensure its in sync with the actual object
        serializedObject.Update();

        // Cast the target object to the CustomTerrain type to access its methods and fields
        CustomTerrain terrain = (CustomTerrain)target;

        EditorGUILayout.PropertyField(terrainProperty, new GUIContent("Terrain"));
        EditorGUILayout.PropertyField(maskProperty, new GUIContent("Mask Texture"));

        if (EditorGUILayout.PropertyField(restoreTerrain))
        {
            terrain.RestoreTerrain();
        }
        
        
        
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

        showGenerate = EditorGUILayout.Foldout(showGenerate, "One-Click Generation");
        if (showGenerate)
        {
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            EditorGUILayout.PropertyField(genParams, true);

            EditorGUILayout.Space(10);
            if (GUILayout.Button("Generate Terrain"))
            {
                terrain.Generate();
            }
        }


        // Create a foldout section in the inspector to group uniform height-related settings
        showUniform = EditorGUILayout.Foldout(showUniform, "Uniform Change");
        if (showUniform)
        {
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider); // Add a horizontal line separator
            EditorGUILayout.Slider(uniformStep, -1.0f, 1.0f, new GUIContent("Uniform Increment"));
            if (GUILayout.Button("Change Height Uniformly"))
            {
                terrain.ChangeHeightUniformly();
            }
        }

        // Create a foldout section in the inspector to group random height-related settings
        showRandom = EditorGUILayout.Foldout(showRandom, "Random Change");
        if (showRandom)
        {
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            // Display a bold label as a header for the random height settings
            GUILayout.Label("Set Heights Between Random Values", EditorStyles.boldLabel);
            // Draw a field in the inspector for the 'randomHeightLimits' property
            EditorGUILayout.PropertyField(randomHeightLimits);
            // Add a button to trigger the 'RandomTerrain' method in CustomTerrain
            if (GUILayout.Button("Random Heights"))
            {
                // Call the RandomTerrain method when the button is clicked
                terrain.RandomTerrain();
            }
        }

        // Create a foldout section in the inspector to group load height-related settings 
        showLoadHeights = EditorGUILayout.Foldout(showLoadHeights, "Load Heights From Texture");
        if (showLoadHeights)
        {
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            GUILayout.Label("Load Heights From Texture", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(heightMapTexture);
            EditorGUILayout.PropertyField(mapScale);
            if (GUILayout.Button("Load Texture"))
            {
                terrain.LoadTexture();
            }

        }

        // Create a foldout section in the inspector to group single Perlin Noise settings
        showPerlinNoise = EditorGUILayout.Foldout(showPerlinNoise, "Single Layer of Perlin Noise");
        if (showPerlinNoise)
        {
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            GUILayout.Label("Perlin Noise", EditorStyles.boldLabel);
            EditorGUILayout.Slider(perlinXFrequency, 0, 0.1f, new GUIContent("X Scale")); //0 is min and 1 is max
            EditorGUILayout.Slider(perlinYFrequency, 0, 0.1f, new GUIContent("Y Scale"));
            EditorGUILayout.IntSlider(perlinXOffset, 0, 10000, new GUIContent("X Offset"));
            EditorGUILayout.IntSlider(perlinYOffset, 0, 10000, new GUIContent("Y Offset"));
            EditorGUILayout.IntSlider(perlinOctaves, 1, 10, new GUIContent("Octaves"));
            EditorGUILayout.Slider(perlinPersistence, 0.1f, 10, new GUIContent("Persistence"));
            EditorGUILayout.Slider(perlinAmplitude, 0, 1, new GUIContent("Amplitude"));
            if (GUILayout.Button("Perlin Noise"))
            {
                terrain.SinglePerlinLayer();
            }
        }

        // Create a foldout section in the inspector to group multiple Perlin Noise settings
        showMultiplePerlin = EditorGUILayout.Foldout(showMultiplePerlin, "Multiple Layers of Perlin Noise");
        if (showMultiplePerlin)
        {
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            GUILayout.Label("Multiple Perlin Noise", EditorStyles.boldLabel);
            perlinOptionsTable = GUITableLayout.DrawTable(perlinOptionsTable, perlinOptions);

            GUILayout.Space(20);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+"))
            {
                terrain.AddNewPerlin();
            }

            if (GUILayout.Button("-"))
            {
                terrain.RemovePerlin();
            }

            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Apply Multiple Perlin"))
            {
                terrain.MultiplePerlinTerrain();
            }

            GUILayout.Space(10);
            if (GUILayout.Button("Apply Ridge Noise"))
            {
                terrain.RidgeNoise();
            }

        }

        // Create a foldout section in the inspector to group Voronoi Tessellation settings
        showVoronoi = EditorGUILayout.Foldout(showVoronoi, "Voronoi Tessellation");
        if (showVoronoi)
        {
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            EditorGUILayout.IntSlider(vtPeakCount, 0, 10, new GUIContent("Peak Count"));
            EditorGUILayout.Slider(vtFallRate, 0, 10, new GUIContent("Fall Off"));
            EditorGUILayout.Slider(vtDropOff, 0, 10, new GUIContent("Drop Off"));
            EditorGUILayout.Slider(vtMinHeight, 0, 1, new GUIContent("Min Height"));
            EditorGUILayout.Slider(vtMaxHeight, 0, 1, new GUIContent("Max Height"));
            EditorGUILayout.PropertyField(vtType);
            if (GUILayout.Button("Voronoi"))
            {
                terrain.Voronoi();
            }
        }

        // Create a foldout section in the inspector to group Midpoint Displacement settings
        showMidpointDisplacement = EditorGUILayout.Foldout(showMidpointDisplacement, "Midpoint Displacement");
        if (showMidpointDisplacement)
        {
            EditorGUILayout.PropertyField(mdMinHeight, new GUIContent("Min Height"));
            EditorGUILayout.PropertyField(mdMaxHeight, new GUIContent("Max Height"));
            EditorGUILayout.PropertyField(mdDampenHeight, new GUIContent("Height Dampener Power"));
            EditorGUILayout.PropertyField(mdRoughnessFactor, new GUIContent("Roughness"));
            if (GUILayout.Button("Perform MPD"))
            {
                terrain.MidpointDisplacement();
            }
        }

        // Create a foldout section in the inspector to group terrain smoothing settings
        showSmooth = EditorGUILayout.Foldout(showSmooth, "Smooth Terrain");
        if (showSmooth)
        {
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            EditorGUILayout.PropertyField(useDistanceSmoothing, new GUIContent("Use Distance-Based Smoothing"));
            EditorGUILayout.IntSlider(smoothingIterations, 1, 100, new GUIContent("Iterations"));
            EditorGUILayout.Slider(baseSmoothing, 0f, 10f, new GUIContent("Smoothing Strength"));
            EditorGUILayout.Slider(distanceFalloff, 0.1f, 25f, new GUIContent("Falloff Rate"));
            
            if (GUILayout.Button("Smooth"))
            {
                terrain.DistanceBasedSmooth();
            }
        }
        
        showDistanceGrid = EditorGUILayout.Foldout(showDistanceGrid, "Distance Grid");
        if (showDistanceGrid)
        {
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            GUILayout.Label("Distance Grid Generation", EditorStyles.boldLabel);

            EditorGUI.BeginDisabledGroup(distanceGridCalculated.boolValue);
            if (GUILayout.Button("Calculate Distance Grid"))
            {
                terrain.CalculateDistanceGrid();
                serializedObject.Update();
            }
            EditorGUI.EndDisabledGroup();

            if (distanceGridCalculated.boolValue)
            {
                EditorGUILayout.HelpBox("Distance grid has been calculated and saved.", MessageType.Info);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Force Recalculate"))
                {
                    if (EditorUtility.DisplayDialog("Confirm Recalculation",
                            "Are you sure you want to recalculate the distance grid? This will overwrite the existing data.",
                            "Yes, Recalculate", "Cancel"))
                    {
                        distanceGridCalculated.boolValue = false;
                        terrain.CalculateDistanceGrid();
                        serializedObject.Update();
                    }
                }
                if (GUILayout.Button("Visualize Distance Grid"))
                {
                    terrain.VisualizeDistanceGrid();
                    string visualizationPath = Path.Combine(
                        Path.GetDirectoryName(terrain.distanceGridSavePath),
                        Path.GetFileNameWithoutExtension(terrain.distanceGridSavePath) + "_visualization.png"
                    );
                    if (File.Exists(visualizationPath))
                    {
                        EditorUtility.RevealInFinder(visualizationPath);
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.HelpBox("Distance grid has not been calculated yet. Click the button above to generate it.", MessageType.Warning);
            }
        }

        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        if (GUILayout.Button("Restore Terrain")) // Add a button to restore the terrain
        {
            terrain.RestoreTerrain();
        }
        
        serializedObject.ApplyModifiedProperties(); // Commits the changes made in the inspector
    }


}
