using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.DTOs.API.Nodes
{
    /// <summary>
    /// Represents the request body for actions targeting one or more nodes, such as restart or shutdown.
    /// </summary>
    /// <remarks>
    /// This DTO is used for operations like POST /nodes/restart and POST /nodes/shutdown.
    /// It allows specifying target nodes either by a list of names or by indicating all nodes.
    /// Corresponds to the 'NodeActionRequest' schema in `web api swagger.yaml`.
    /// </remarks>
    public class NodeActionRequest
    {
        /// <summary>
        /// Gets or sets a list of specific node names to target for the action.
        /// </summary>
        /// <remarks>
        /// If <see cref="AllNodes"/> is true, this list might be ignored or used as a supplemental filter depending on the specific operation's logic.
        /// If <see cref="AllNodes"/> is false, this list must contain the target node names.
        /// </remarks>
        /// <example>["IOS1", "Trainee1"]</example>
        [JsonPropertyName("nodeNames")]
        public List<string>? NodeNames { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets a value indicating whether the action should target all applicable nodes in the environment.
        /// </summary>
        /// <remarks>
        /// If true, the operation will be applied to all nodes deemed relevant by the backend service.
        /// The <see cref="NodeNames"/> list might be ignored or used as an exclusion list in some implementations if this is true.
        /// If false, the action targets only the nodes specified in the <see cref="NodeNames"/> list.
        /// </remarks>
        /// <example>false</example>
        [JsonPropertyName("allNodes")]
        public bool? AllNodes { get; set; } = false;
    }
} 