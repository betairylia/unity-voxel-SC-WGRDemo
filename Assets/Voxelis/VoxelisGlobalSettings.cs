using System.Collections.Generic;
using TypeReferences;
//using UnityEditor;
using UnityEngine;
using Voxelis.Rendering;
using WorldGen.WorldSketch;

// https://docs.unity3d.com/2018.3/Documentation/ScriptReference/SettingsProvider.html
namespace Voxelis
{
    [CreateAssetMenu(fileName = "VoxelisSettings", menuName = "Voxelis/GlobalSetting", order = 101)]
    public class VoxelisGlobalSettings : ScriptableObject
    {
        public static string k_SettingsPath = "Assets/Voxelis/GlobalSettings.asset";

        [Inherits(typeof(ChunkRenderableBase))]
        public TypeReference rendererType;

        //public static VoxelisGlobalSettings GetOrCreateSettings()
        //{
        //    var settings = AssetDatabase.LoadAssetAtPath<VoxelisGlobalSettings>(k_SettingsPath);

        //    if(settings == null)
        //    {
        //        settings = ScriptableObject.CreateInstance<VoxelisGlobalSettings>();
        //        settings.generatorType = typeof(SimplePerlin);
        //        settings.sketcherType = typeof(SillyRiverPlains);
        //        settings.rendererType = typeof(ChunkRenderer);

        //        AssetDatabase.CreateAsset(settings, k_SettingsPath);
        //        AssetDatabase.SaveAssets();
        //    }

        //    return settings;
        //}
    }

    //static class VoxelisGlobalSettingsIMGUIRegister
    //{
    //    [SettingsProvider]
    //    public static SettingsProvider CreateVoxelisGlobalSettingsProvider()
    //    {
    //        var provider = new SettingsProvider("Project/Voxelis", SettingsScope.Project)
    //        {
    //            label = "Voxelis",

    //            guiHandler = (searchContext) =>
    //            {
    //                var settings = new SerializedObject(VoxelisGlobalSettings.GetOrCreateSettings());
    //                EditorGUILayout.PropertyField(settings.FindProperty("generatorType"));
    //                EditorGUILayout.PropertyField(settings.FindProperty("sketcherType"));
    //                EditorGUILayout.PropertyField(settings.FindProperty("rendererType"));
    //            },

    //            keywords = new HashSet<string>(new[] { "Generator Type", "Sketcher Type", "Renderer Type" })
    //        };

    //        return provider;
    //    }
    //}
}