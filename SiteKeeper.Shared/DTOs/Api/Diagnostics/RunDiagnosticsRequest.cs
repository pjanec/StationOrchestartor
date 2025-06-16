using System.Collections.Generic;

namespace SiteKeeper.Shared.DTOs.API.Diagnostics
{
    /// <summary>
    /// Represents the request body for initiating a diagnostic operation on specified target nodes.
    /// </summary>
    /// <remarks>
    /// This DTO is used to trigger an operation like <c>OperationType.RunStandardDiagnostics</c>.
    /// It allows specifying a list of target nodes for the diagnostics. If the list is null or empty,
    /// the operation may apply to all nodes in the environment, depending on server-side implementation.
    /// Based on the RunDiagnosticsRequest schema in `web api swagger.yaml` (though it might be a general purpose operation
    /// request using <see cref="Operations.OperationInitiateRequest"/> with specific parameters) and detailed in
    /// "SiteKeeper - API - Detailed DTO Definitions.md" which suggests a more specific DTO for clarity.
    /// </remarks>
    public class RunDiagnosticsRequest
    {
        /// <summary>
        /// Optional list of specific node names on which to run diagnostic checks.
        /// If null or empty, diagnostics may run on all nodes in the environment or a predefined set,
        /// depending on the specific diagnostic operation type and server logic.
        /// </summary>
        /// <example>["AppServer01", "DatabaseNode01"]</example>
        public List<string>? NodeNames { get; set; }

        /// <summary>
        /// Optional identifier for a specific diagnostic profile or set of checks to run.
        /// If null or empty, a default or standard set of diagnostics is typically performed.
        /// </summary>
        /// <example>"FullSystemCheck"</example>
        public string? DiagnosticProfileId { get; set; }
    }
} 