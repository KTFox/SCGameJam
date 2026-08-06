using UnityEngine;

namespace SCJam.LevelSystem
{
    /// <summary>
    /// A single validation finding for authored level data.
    /// </summary>
    public sealed class LevelValidationIssue
    {
        /// <summary>
        /// Creates a validation issue.
        /// </summary>
        /// <param name="severity">Issue severity.</param>
        /// <param name="message">Human-readable description.</param>
        /// <param name="vehicleId">Optional related vehicle ID.</param>
        /// <param name="cell">Optional related grid cell.</param>
        /// <param name="context">Optional related context object.</param>
        public LevelValidationIssue(
            ValidationSeverity severity,
            string message,
            string vehicleId = null,
            Vector2Int? cell = null,
            Object context = null)
        {
            Severity = severity;
            Message = message ?? string.Empty;
            VehicleId = vehicleId;
            Cell = cell;
            Context = context;
        }

        /// <summary>
        /// Gets the issue severity.
        /// </summary>
        public ValidationSeverity Severity { get; }

        /// <summary>
        /// Gets the human-readable message.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Gets the optional related vehicle ID.
        /// </summary>
        public string VehicleId { get; }

        /// <summary>
        /// Gets the optional related grid cell.
        /// </summary>
        public Vector2Int? Cell { get; }

        /// <summary>
        /// Gets the optional Unity context object for pinging in the editor.
        /// </summary>
        public Object Context { get; }

        /// <summary>
        /// Formats the issue for console logging.
        /// </summary>
        /// <returns>Log-friendly string.</returns>
        public override string ToString()
        {
            string vehiclePart = string.IsNullOrEmpty(VehicleId) ? string.Empty : $" vehicleId={VehicleId}";
            string cellPart = Cell.HasValue ? $" cell={Cell.Value}" : string.Empty;
            return $"[{Severity}]{vehiclePart}{cellPart} {Message}";
        }
    }
}
