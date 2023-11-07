using System;
using System.Linq;
using System.Collections.Generic;
using Moesif.Api;
using Moesif.Api.Models;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Moesif.Middleware.Models;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Moesif.Middleware.Helpers
{
    public class Tasks
    {
        //TODO  use Channel
        static List<EventModel> QueueGetAll(ConcurrentQueue<EventModel> moesifQueue, int batchSize)
        {
            List<EventModel> events = new List<EventModel>();
            foreach (var eventsRetrieved in Enumerable.Range(0, batchSize))
            {
                try
                {
                    if (eventsRetrieved == batchSize || moesifQueue.Count == 0)
                    {
                        break;
                    }
                    EventModel localEventModel;
                    moesifQueue.TryDequeue(out localEventModel);
                    if (localEventModel != null)
                    {
                        events.Add(localEventModel);
                    }
                }
                catch (Exception)
                {
                    break;
                }
            }
            
            return events;
        }

        public static async Task AsyncClientCreateEvent(MoesifApiClient client, ConcurrentQueue<EventModel> MoesifQueue,
            AppConfig config, Governance governance, AutoResetEvent configEvent, AutoResetEvent governanceEvent,int batchSize, bool debug, ILogger logger)
        {
            List<EventModel> batchEvents = new List<EventModel>();

            while (MoesifQueue.Count > 0)
            {
                batchEvents = QueueGetAll(MoesifQueue, batchSize);

                if ((batchEvents.Any()))
                {
                    try
                    {
                        // Send Batch Request
                        var createBatchEventResponse = await client.Api.CreateEventsBatchAsync(batchEvents);
                        createBatchEventResponse = createBatchEventResponse.ToDictionary(k => k.Key.ToLower(), k => k.Value);

                        logger.LogDebug("Events sent successfully to Moesif at {time}", DateTime.UtcNow);
                    }
                    catch (Exception e)
                    {
                        if(logger != null)
                            logger.LogError(e, "Could not connect to Moesif server at {time}" ,DateTime.UtcNow);
                    }
                }
                else
                {
                    if(logger != null) 
                        logger.LogDebug("No events in the queue");
                }
            }
            if(logger != null) logger.LogDebug("No events in the queue");
        }
    }
}
