using System.Threading.Tasks;

namespace SiteKeeper.Master.Abstractions.Services  
{  
    /// <summary>  
    /// Defines a contract for a service that maps short-lived NodeAction IDs  
    /// to their parent MasterAction ID for the duration of a workflow.  
    /// </summary>  
    public interface IActionIdTranslator  
    {  
        /// <summary>  
        /// Registers a mapping between a NodeAction ID and its parent MasterAction ID.  
        /// </summary>  
        /// <param name="nodeActionId">The unique ID of the NodeAction (a stage's sub-action).</param>  
        /// <param name="masterActionId">The unique ID of the parent MasterAction.</param>  
        void RegisterMapping(string nodeActionId, string masterActionId);

        /// <summary>  
        /// Removes all mappings associated with a given parent MasterAction ID.  
        /// This is called when a workflow completes to prevent memory leaks.  
        /// </summary>  
        /// <param name="masterActionId">The unique ID of the parent MasterAction whose mappings should be cleared.</param>  
        void UnregisterAllForMasterAction(string masterActionId);

        /// <summary>  
        /// Translates a NodeAction ID back to its parent MasterAction ID.  
        /// </summary>  
        /// <param name="nodeActionId">The NodeAction ID to translate.</param>  
        /// <returns>The parent MasterAction ID, or null if no mapping is found.</returns>  
        string? TranslateNodeActionIdToMasterActionId(string nodeActionId);  
    }  
} 