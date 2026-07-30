using UnityEditor;
using UnityEngine;

namespace Supercent.Rendering.Shadow.Editor
{
    [InitializeOnLoad]
    public static class PlanarShadowInitializerExtension
    {
        private static Texture2D _customIcon = null;
        private static Texture2D _folderIcon = null;

        static PlanarShadowInitializerExtension()
        {
            LoadIcons();
            EditorApplication.projectWindowItemOnGUI += OnProjectWindowItemGUI;
        }

        private static void LoadIcons()
        {
            if (_customIcon == null)
            {
                _customIcon = LoadIcon("planar_shadow_main_icon.png", "t:Texture2D");
            }

            if (_folderIcon == null)
            {
                _folderIcon = LoadIcon("planar_shadow_color_icon.png", "t:Texture2D");
            }

            SetScriptIcon();
        }

        private static Texture2D LoadIcon(string fileName, string type)
        {
            string path = FindFilePath(fileName, type);
            if (!string.IsNullOrEmpty(path))
            {
                return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            return null;
        }

        private static void SetScriptIcon()
        {
            const string SCRIPT_FILE_NAME = "PlanarShadow.cs";
            string scriptPath = FindFilePath(SCRIPT_FILE_NAME, "t:MonoScript");

            if (!string.IsNullOrEmpty(scriptPath))
            {
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);
                if (script != null && _customIcon != null)
                {
                    EditorGUIUtility.SetIconForObject(script, _customIcon);
                }
            }
        }

        private static void OnProjectWindowItemGUI(string guid, Rect selectionRect)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);

            if (assetPath == "Assets/Planar Shadow" && _folderIcon != null)
            {
                // 폴더 아이콘 크기 계산 (기본 폴더 아이콘은 정사각형)
                float folderIconSize = selectionRect.height * 0.8f; // 기본 폴더 아이콘 크기 조정
                float folderIconOffset = (selectionRect.height - folderIconSize) * 0.5f; // 중앙 정렬

                // 아이콘이 표시될 폴더 아이콘의 영역
                Rect folderRect = new Rect(selectionRect.x + folderIconOffset, selectionRect.y + folderIconOffset, folderIconSize, folderIconSize);
                
                // 우측 하단에 배치할 아이콘 크기
                float iconSize = folderIconSize * 0.5f; // 폴더 아이콘의 35% 크기
                float offsetX = folderRect.xMax - iconSize - 1; // 폴더 아이콘 우측 하단
                float offsetY = folderRect.yMax - iconSize - 1; // 폴더 아이콘 우측 하단
                
                // 폴더 아이콘 자체를 덮지 않고 표시
                Rect iconRect = new Rect(offsetX, offsetY, iconSize, iconSize);
                GUI.DrawTexture(iconRect, _folderIcon, ScaleMode.ScaleToFit);
            }
        }

        private static string FindFilePath(string fileName, string type)
        {
            string[] guids = AssetDatabase.FindAssets(type);
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileName(path) == fileName)
                {
                    return path;
                }
            }
            return string.Empty;
        }
    }
}
