using System.Collections.Generic;
using SCJam.Common;

namespace SCJam.PassengerSystem
{
    /// <summary>
    /// Resolves the passenger prefab to spawn for a given PuzzleColor within a single level.
    /// Built once per level load from that level's prefab mappings; does not talk to any MonoBehaviour
    /// or ScriptableObject directly so it can be constructed and validated outside of the LevelController.
    /// </summary>
    public sealed class PassengerPrefabLookup
    {
        private readonly Dictionary<PuzzleColor, PassengerController> _prefabsByColor;


        private PassengerPrefabLookup(Dictionary<PuzzleColor, PassengerController> prefabsByColor)
        {
            _prefabsByColor = prefabsByColor;
        }


        public bool TryGetPrefab(PuzzleColor color, out PassengerController prefab)
        {
            return _prefabsByColor.TryGetValue(color, out prefab);
        }

        /// <summary>
        /// Builds a color-to-prefab lookup from mappings, appending one message per problem found to errors:
        /// a null prefab, two mappings for the same color, or a color present in requiredColors with no
        /// mapping. A color with a duplicate mapping is left out of the resulting lookup entirely rather
        /// than silently resolving to one of the duplicates.
        /// </summary>
        public static PassengerPrefabLookup Build(
            IReadOnlyList<PassengerPrefabMapping> mappings,
            IReadOnlyList<PuzzleColor> requiredColors,
            List<string> errors)
        {
            Dictionary<PuzzleColor, PassengerController> prefabsByColor = new();
            HashSet<PuzzleColor> seenColors = new();
            HashSet<PuzzleColor> duplicateColors = new();

            if (mappings != null)
            {
                foreach (PassengerPrefabMapping mapping in mappings)
                {
                    if (mapping.Prefab == null)
                    {
                        errors?.Add($"passenger prefab mapping for color {mapping.Color} has a null prefab.");
                        continue;
                    }

                    if (!seenColors.Add(mapping.Color))
                    {
                        if (duplicateColors.Add(mapping.Color))
                            errors?.Add($"duplicate passenger prefab mapping for color {mapping.Color}.");

                        continue;
                    }

                    prefabsByColor[mapping.Color] = mapping.Prefab;
                }

                foreach (PuzzleColor duplicateColor in duplicateColors)
                    prefabsByColor.Remove(duplicateColor);
            }

            if (requiredColors != null)
            {
                HashSet<PuzzleColor> reportedMissingColors = new();

                foreach (PuzzleColor color in requiredColors)
                {
                    if (prefabsByColor.ContainsKey(color) || !reportedMissingColors.Add(color))
                        continue;

                    errors?.Add($"no passenger prefab mapping found for color {color}.");
                }
            }

            return new PassengerPrefabLookup(prefabsByColor);
        }
    }
}
