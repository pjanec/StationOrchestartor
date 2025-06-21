using Microsoft.Extensions.Logging;
using SiteKeeper.Master.Abstractions.Workflow;
using System;
using System.Collections.Generic;

namespace SiteKeeper.Master.Workflow
{
    /// <summary>
    /// A scoped logger implementation that holds workflow context (Action ID, Stage)
    /// and applies it to every log message using logging scopes.
    /// </summary>
    public class WorkflowLogger : IWorkflowLogger
    {
        private readonly ILogger _innerLogger;

        // Context properties
        private string _masterActionId = "uninitialized";
        private int _stageIndex = 0;
        private string _stageName = "_init";

        public WorkflowLogger(ILoggerFactory loggerFactory)
        {
            // Creates a standard logger instance that this class will wrap.
            _innerLogger = loggerFactory.CreateLogger("MasterWorkflow");
        }

        public void SetContext(string masterActionId)
        {
            _masterActionId = masterActionId;
        }

        public void SetStage(int stageIndex, string stageName)
        {
            _stageIndex = stageIndex;
            _stageName = stageName;
        }

        // Standard ILogger implementation
        public IDisposable BeginScope<TState>(TState state) => _innerLogger.BeginScope(state);

        public bool IsEnabled(LogLevel logLevel) => _innerLogger.IsEnabled(logLevel);

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            // Create a dictionary with our context properties.
            var contextScope = new Dictionary<string, object>
            {
                { "SK-MasterActionId", _masterActionId },
                { "SK-StageIndex", _stageIndex },
                { "SK-StageName", _stageName }
            };

            // Wrap the actual log call in a scope containing our context.
            // The NLog provider will automatically add these properties to the LogEvent.
            using (_innerLogger.BeginScope(contextScope))
            {
                _innerLogger.Log(logLevel, eventId, state, exception, formatter);
            }
        }
    }
}
