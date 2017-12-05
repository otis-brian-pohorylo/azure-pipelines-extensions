using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace VstsServerTaskHelper
{
    public class LoggersAggregate : ILogger
    {
        private readonly IList<ILogger> loggers;
        private readonly ITaskClient taskClient;
        private readonly Guid scopeIdentifier;
        private readonly Guid planId;
        private readonly int taskLogId;
        private readonly IDictionary<string, string> baseEventProperties;
        private readonly string hubName;

        public LoggersAggregate(IList<ILogger> loggers)
        {
            this.loggers = loggers;
            this.baseEventProperties = new Dictionary<string, string>();
        }

        public Task HandleException(Exception ex, string eventName, string eventMessage, IDictionary<string, string> eventProperties, CancellationToken cancellationToken, DateTime? eventTime = null)
        {
            eventProperties = this.MergeProperties(eventProperties);
            var exceptionTypeName = ex.GetType().Name;
            eventProperties["ExceptionName"] = exceptionTypeName;
            var attempt = GetAttempt(eventProperties);
            eventTime = eventTime ?? DateTime.UtcNow;
            var logMessage = string.Format("[{0}] EXCEPTION: {1}: {2} (Attempt: {3}) Details: {4}", eventTime.Value.ToString("o"), exceptionTypeName, eventMessage, attempt, ex.ToString());

            var tasks = loggers.Select(logger => logger.HandleException(ex, eventName, logMessage, eventProperties, cancellationToken));

            return Task.WhenAll(tasks);
        }

        public Task HandleInfoEvent(string eventName, string eventMessage, IDictionary<string, string> eventProperties, CancellationToken cancellationToken, DateTime? eventTime = null)
        {
            eventProperties = this.MergeProperties(eventProperties);
            var attempt = GetAttempt(eventProperties);
            eventTime = eventTime.HasValue ? eventTime : DateTime.UtcNow;
            var logMessage = string.Format("[{0}] INFO: {1}: {2} (Attempt: {3})", eventTime.Value.ToString("o"), eventName, eventMessage, attempt);
            var tasks = loggers.Select(logger => logger.HandleInfoEvent(eventName, logMessage, eventProperties, cancellationToken));
            return Task.WhenAll(tasks);
        }

        public Task HandleTraceEvent(string eventName, string eventMessage, IDictionary<string, string> eventProperties, CancellationToken cancellationToken, DateTime? eventTime = null)
        {
            // don't log traces to VSTS, just delegate to base
            return Task.WhenAll(this.loggers.Select(logger => logger.HandleTraceEvent(eventName, eventMessage, eventProperties, cancellationToken)));
        }

        public Task HandleErrorEvent(string eventName, string eventMessage, IDictionary<string, string> eventProperties, CancellationToken cancellationToken, DateTime? eventTime = null)
        {
            eventProperties = this.MergeProperties(eventProperties);
            var attempt = GetAttempt(eventProperties);
            eventTime = eventTime.HasValue ? eventTime : DateTime.UtcNow;
            var logMessage = string.Format("[{0}] ERROR: {1}: {2} (Attempt: {3})", eventTime.Value.ToString("o"), eventName, eventMessage, attempt);
            var tasks = loggers.Select(logger => logger.HandleErrorEvent(eventName, logMessage, eventProperties, cancellationToken));

            return Task.WhenAll(tasks);
        }

        private static string GetAttempt(IDictionary<string, string> eventProperties)
        {
            string attempt;
            if (!eventProperties.TryGetValue(VstsMessageConstants.RetryAttemptPropertyName, out attempt))
            {
                attempt = "1";
            }

            return attempt;
        }

        private IDictionary<string, string> MergeProperties(IDictionary<string, string> eventProperties)
        {
            if (eventProperties == null)
            {
                return this.baseEventProperties;
            }

            var updatedProperties = new Dictionary<string, string>(this.baseEventProperties);
            foreach (var eventProperty in eventProperties)
            {
                updatedProperties[eventProperty.Key] = eventProperty.Value;
            }

            return updatedProperties;
        }
    }
}