using System.Collections.Generic;

namespace SCJam.LevelSystem
{
    /// <summary>
    /// Immutable collection of validation findings for a level definition.
    /// </summary>
    public sealed class LevelValidationResult
    {
        private readonly List<LevelValidationIssue> _issues;

        /// <summary>
        /// Creates a validation result from the provided issues.
        /// </summary>
        /// <param name="issues">Collected issues.</param>
        public LevelValidationResult(IReadOnlyList<LevelValidationIssue> issues)
        {
            _issues = issues != null
                ? new List<LevelValidationIssue>(issues)
                : new List<LevelValidationIssue>();
        }

        /// <summary>
        /// Gets all validation issues.
        /// </summary>
        public IReadOnlyList<LevelValidationIssue> Issues => _issues;

        /// <summary>
        /// Gets whether any error-severity issues were found.
        /// </summary>
        public bool HasErrors
        {
            get
            {
                for (int i = 0; i < _issues.Count; i++)
                {
                    if (_issues[i].Severity == ValidationSeverity.Error)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>
        /// Gets whether the level is considered valid for runtime initialization.
        /// </summary>
        public bool IsValid => !HasErrors;

        /// <summary>
        /// Gets the number of issues.
        /// </summary>
        public int IssueCount => _issues.Count;
    }
}
