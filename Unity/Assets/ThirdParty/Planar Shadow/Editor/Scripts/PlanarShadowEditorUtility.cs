using UnityEditor;
using UnityEngine;

namespace Supercent.Rendering.Shadow.Editor
{
    public static class PlanarShadowEditorUtility
    {
        public static void LoadAllPrefabs(System.Action<GameObject> onPrefabLoaded)
        {
            string[] assetPaths = AssetDatabase.FindAssets("t:Prefab");
            int totalAssets = assetPaths.Length;

            for (int i = 0; i < totalAssets; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(assetPaths[i]);

                GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);

                if (asset == null)
                {
                    Debug.LogWarning($"<color=yellow>[Planar Shadow] Failed to load the asset at path: {assetPath}</color>");
                    continue;
                }

                onPrefabLoaded?.Invoke(asset);
            }
        }

        public static Material GetPlanarShadowOriginalMat()
        {
            string path = "PlanarShadowMat";
            Material planarShadowOriginalMat = Resources.Load<Material>(path);

            if (planarShadowOriginalMat == null)
                Debug.LogError($"<color=red>[Planar Shadow] Shadow material is missing at Assets/Resources/{path}.mat!</color>");

            return planarShadowOriginalMat;
        }

        public static Material GetPlanarShadowBakedMat()
        {
            string path = "PlanarShadowBakedMat";
            Material planarShadowOriginalMat = Resources.Load<Material>(path);

            if (planarShadowOriginalMat == null)
                Debug.LogError($"<color=red>[Planar Shadow] Shadow material is missing at Assets/Resources/{path}.mat!</color>");

            return planarShadowOriginalMat;
        }

        public static bool HasMissingScripts(GameObject target)
        {
            Component[] components = target.GetComponentsInChildren<Component>(true);
            foreach (var component in components)
            {
                if (component == null)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
