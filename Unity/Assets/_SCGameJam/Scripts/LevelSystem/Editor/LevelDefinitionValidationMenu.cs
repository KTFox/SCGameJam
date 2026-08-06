#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace SCJam.LevelSystem
{
    /// <summary>
    /// Editor commands for validating <see cref="LevelSO"/> assets.
    /// </summary>
    public static class LevelDefinitionValidationMenu
    {
        private const string VALIDATE_SELECTED_MENU = "Assets/SCJam/Validate Level Definition";

        /// <summary>
        /// Validates the currently selected level definition asset.
        /// </summary>
        [MenuItem(VALIDATE_SELECTED_MENU, false, 2000)]
        private static void ValidateSelectedLevelDefinition()
        {
            LevelSO definition = Selection.activeObject as LevelSO;
            if (definition == null)
            {
                Debug.LogWarning("[LevelValidation] Select a LevelDefinition asset first.");
                return;
            }

            LevelDefinitionValidator.ValidateAndLog(definition);
        }

        [MenuItem(VALIDATE_SELECTED_MENU, true)]
        private static bool ValidateSelectedLevelDefinitionValidate()
        {
            return Selection.activeObject is LevelSO;
        }
    }
}
#endif
