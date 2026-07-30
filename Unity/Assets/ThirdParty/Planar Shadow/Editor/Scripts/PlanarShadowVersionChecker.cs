using System;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Supercent.Rendering.Shadow.Editor.Utility
{
    [InitializeOnLoad]
    public static class PlanarShadowVersionChecker
    {
        private const string URL = "https://raw.githubusercontent.com/Bonnate/VersionChecker/main/planarshadow.json";
        private const string CURRENT_VERSION = "1.0.1";
        private const string UPDATE_LINK = "https://www.notion.so/supercent/10a93b2d25738022a4b6f6edf615781c?pvs=4";
        private const string PREFS_KEY = "PlanarShadowVersionCheckDone";
        private static string _checkedVersion = null;

        static PlanarShadowVersionChecker()
        {
            EditorApplication.quitting += ResetVersionCheckFlag;

            if (!EditorPrefs.GetBool(PREFS_KEY, false))
            {
                CheckVersion();
            }
        }

        [MenuItem("Supercent/Planar Shadow/Check for Updates", false, int.MaxValue)]
        private static void CheckVersion()
        {
            EditorApplication.delayCall += async () =>
            {
                await RunVersionCheckAsync();
                EditorPrefs.SetBool(PREFS_KEY, true);
            };
        }

        private static async Task RunVersionCheckAsync()
        {
            PlanarShadowVersionData versionData = await FetchVersionFromJsonAsync();
            if (versionData == null || string.IsNullOrEmpty(versionData.PlanarShadow))
            {
                Debug.LogWarning("<color=yellow>[Planar Shadow] Failed to fetch version information.</color>");
                return;
            }

            _checkedVersion = versionData.PlanarShadow;
            if (_checkedVersion == CURRENT_VERSION)
            {
                Debug.Log($"<color=cyan>[Planar Shadow] You are using the latest version ({_checkedVersion}).</color>");
            }
            else
            {
                ShowUpdateDialog();
            }
        }

        private static async Task<PlanarShadowVersionData> FetchVersionFromJsonAsync()
        {
            using HttpClient client = new HttpClient();
            try
            {
                string cacheBypassUrl = $"{URL}?t={DateTime.UtcNow.Ticks}";
                string response = await client.GetStringAsync(cacheBypassUrl);

                return JsonUtility.FromJson<PlanarShadowVersionData>(response);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"<color=yellow>[Planar Shadow] Error occurred while fetching JSON data: {ex.Message}</color>");
                return null;
            }
        }

        private static void ShowUpdateDialog()
        {
            EditorApplication.delayCall += () =>
            {   
                string message = $"You are not using the latest version. Current version: {CURRENT_VERSION}. Latest version: {_checkedVersion}.";

                Debug.Log($"<color=yellow>[Planar Shadow] {message}</color>");

                if (EditorUtility.DisplayDialog(
                    "Planar Shadow Version Check",
                    message,
                    "OK",
                    "Open Notion Page"))
                {
                }
                else
                {
                    Application.OpenURL(UPDATE_LINK);
                }
            };
        }

        private static void ResetVersionCheckFlag()
        {
            EditorPrefs.DeleteKey(PREFS_KEY);
        }
    }

    [Serializable]
    public class PlanarShadowVersionData
    {
        public string PlanarShadow;
    }
}
