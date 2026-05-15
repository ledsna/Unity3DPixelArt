using UnityEditor;
using Grass.Core;
using System.IO;

namespace Grass.Editor
{
    public class GrassDataMenu
    {
        [MenuItem("Assets/Create/Grass Data File", false)]
        private static void CreateGrassDataFile()
        {
            string path = "Assets";
            if (Selection.activeObject != null)
            {
                path = AssetDatabase.GetAssetPath(Selection.activeObject);
                if (!Directory.Exists(path))
                {
                    path = Path.GetDirectoryName(path);
                }
            }

            GrassDataManager.CreateGrassDataAsset(path);
            AssetDatabase.Refresh();
        }
    }
}