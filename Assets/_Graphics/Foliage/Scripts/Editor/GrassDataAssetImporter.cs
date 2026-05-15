using System.IO;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;
using Grass.Core;

namespace Grass.Editor
{
    [ScriptedImporter(2, "grassdata")]
    public sealed class GrassDataAssetImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            byte[] fileBytes = File.ReadAllBytes(ctx.assetPath);

            var grassDataAsset = ScriptableObject.CreateInstance<GrassDataAsset>();
            grassDataAsset.Data = fileBytes;

            Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>("./Icons/grass_icon.png");
            ctx.AddObjectToAsset("Grass Data", grassDataAsset, icon);
            ctx.SetMainObject(grassDataAsset);
        }
    }
}
