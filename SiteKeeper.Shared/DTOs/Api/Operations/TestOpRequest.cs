using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.DTOs.API.Operations
{
    /// <summary>
    /// Enum to control how the Master-side IMasterActionHandler should behave during a test.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum MasterFailureMode
    {
        /// <summary>
        /// The master-side handler executes normally without any simulated failures.
        /// </summary>
        None,
        /// <summary>
        /// The handler will throw an exception immediately upon execution, before starting any stages.
        /// </summary>
        ThrowBeforeFirstStage,
        /// <summary>
        /// The handler will execute the first stage and then throw an exception.
        /// </summary>
        ThrowAfterFirstStage
    }

    /// <summary>
    /// Enum to control how the slave-side IExecutiveCodeExecutor should behave during a test.
    /// This was previously named OrchestrationTestMode.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum SlaveBehaviorMode
    {
        /// <summary>
        /// The slave task will simulate successful execution.
        /// </summary>
        Succeed,
        /// <summary>
        /// The slave will report that it is not ready for the task during the readiness check.
        /// </summary>
        FailOnPrepare,
        /// <summary>
        /// The slave will report ready, but then fail during the main execution phase.
        /// </summary>
        FailOnExecute,
        /// <summary>
        /// The slave will not respond to the "Prepare for Task" instruction, forcing a timeout on the master.
        /// </summary>
        TimeoutOnPrepare,
        /// <summary>
        /// The slave will start execution but will delay longer than the master's timeout period.
        /// </summary>
        TimeoutOnExecute,
        /// <summary>
        /// The slave will enter a long-running state, allowing the master to test the cancellation flow.
        /// </summary>
        CancelDuringExecute
    }

    /// <summary>
    /// Defines the parameters for the test-op endpoint to simulate various orchestration scenarios.
    /// This DTO has been enhanced to support both master-side and slave-side failure simulations.
    /// </summary>
    public class TestOpRequest
    {
        /// <summary>
        /// Dictates how the Master-side IMasterActionHandler should behave.
        /// Defaults to None for normal execution.
        /// </summary>
        [JsonPropertyName("masterFailure")]
        public MasterFailureMode MasterFailure { get; set; } = MasterFailureMode.None;

        /// <summary>
        /// Dictates how the Slave-side IExecutiveCodeExecutor should behave.
        /// This is a required parameter.
        /// </summary>
        [Required]
        [JsonPropertyName("slaveBehavior")]
        public SlaveBehaviorMode SlaveBehavior { get; set; }

        /// <summary>
        /// The name of the single slave node to target for this test.
        /// Should be "InternalTestSlave" for these integration tests.
        /// </summary>
        [Required]
        [JsonPropertyName("targetNodeName")]
        public string TargetNodeName { get; set; } = "InternalTestSlave";

        /// <summary>
        /// Delay in seconds for the task execution simulation. 
        /// Used for testing timeouts and providing a window for cancellation requests.
        /// </summary>
        [JsonPropertyName("executionDelaySeconds")]
        public int ExecutionDelaySeconds { get; set; } = 1;

        /// <summary>
        /// A custom message used for different purposes depending on the test:
        /// - For failure simulations, this is the error message that will be returned.
        /// - For success simulations, this message will be logged on the master or slave, allowing log verification tests.
        /// This property replaces the previous 'FailureReason' for more flexibility.
        /// </summary>
        [JsonPropertyName("customMessage")]
        public string? CustomMessage { get; set; }
    }
}
