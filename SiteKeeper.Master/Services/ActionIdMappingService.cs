using SiteKeeper.Master.Abstractions.Services;  
using System.Collections.Concurrent;  
using System.Collections.Generic;  
using System.Linq;

namespace SiteKeeper.Master.Services  
{  
    /// <summary>  
    /// A thread-safe, in-memory implementation of IActionIdTranslator using ConcurrentDictionaries.  
    /// Registered as a singleton.  
    /// </summary>  
    public class ActionIdMappingService : IActionIdTranslator  
    {  
        // Key: nodeActionId, Value: masterActionId  
        private readonly ConcurrentDictionary<string, string> _nodeToMasterMap = new();

        public void RegisterMapping(string nodeActionId, string masterActionId)  
        {  
            _nodeToMasterMap[nodeActionId] = masterActionId;  
        }

        public string? TranslateNodeActionIdToMasterActionId(string nodeActionId)  
        {  
            _nodeToMasterMap.TryGetValue(nodeActionId, out var masterActionId);  
            return masterActionId;  
        }

        public void UnregisterAllForMasterAction(string masterActionId)  
        {  
            var keysToRemove = _nodeToMasterMap  
                .Where(pair => pair.Value == masterActionId)  
                .Select(pair => pair.Key)  
                .ToList();

            foreach (var key in keysToRemove)  
            {  
                _nodeToMasterMap.TryRemove(key, out _);  
            }  
        }  
    }  
} 