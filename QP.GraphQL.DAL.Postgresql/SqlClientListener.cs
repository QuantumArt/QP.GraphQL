using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics.Tracing;
using System.Linq;

namespace QP.GraphQL.DAL.Postgresql
{
    public class SqlClientListener : EventListener
    {
        private const EventKeywords DataBase = (EventKeywords)2;
        private readonly ILogger<SqlClientListener> _logger;
        private readonly string[] _eventSources = new[] { "Npgsql", "Npgsql.Sql", "Microsoft.Data.SqlClient.EventSource" };

        public SqlClientListener(ILogger<SqlClientListener> logger)
        {
            _logger = logger;
        }

        protected override void OnEventSourceCreated(EventSource eventSource)
        {            
            if (_eventSources.Contains(eventSource.Name))
            {
                EnableEvents(eventSource, EventLevel.LogAlways, DataBase);
            }
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            if (eventData.Payload.Count > 0)
            {
                var dbEvent = new
                {
                    Id = eventData.EventId,
                    EventSource = eventData.EventSource.Name,
                    Query = eventData.Payload[0]
                };

                var level = eventData.Level switch
                {
                    EventLevel.Verbose => LogLevel.Trace,
                    EventLevel.Informational => LogLevel.Information,
                    EventLevel.Warning => LogLevel.Warning,
                    EventLevel.Error => LogLevel.Error,
                    EventLevel.Critical => LogLevel.Critical,
                    _ => LogLevel.None
                };

                _logger.Log(level, "{@database}", dbEvent);
            }
        }
    }
}
