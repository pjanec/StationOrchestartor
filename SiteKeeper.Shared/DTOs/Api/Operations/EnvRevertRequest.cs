using System.ComponentModel.DataAnnotations;

namespace SiteKeeper.Shared.DTOs.API.Operations
{
    /// <summary>
    /// Represents the request to initiate an environment revert operation.
    /// </summary>
    /// <remarks>
    /// This DTO is used to specify the target state to which the environment should be reverted,
    /// typically sent to an endpoint like POST /operations/env-revert.
    /// The revert is based on a journal record, which represents a previously recorded "pure" state of the environment,
    /// typically captured after a successful update or other major change.
    /// This operation is powerful and potentially disruptive, requiring appropriate administrative privileges.
    /// </remarks>
    public class EnvRevertRequest
    {
        /// <summary>
        /// Gets or sets the unique identifier of the journal record to revert to.
        /// </summary>
        /// <remarks>
        /// This ID must correspond to an existing journal entry that represents a valid, revertible environment state.
        /// The backend service will validate this ID before proceeding with the revert operation.
        /// </remarks>
        [Required(ErrorMessage = "A target journal record ID is required to initiate a revert.")]
        public string TargetJournalRecordId { get; set; } = string.Empty;
    }
} 