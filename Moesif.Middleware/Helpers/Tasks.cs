using System;
using System.Linq;
using System.Collections.Generic;
using Moesif.Api;
using Moesif.Api.Models;
using System.Threading.Tasks;

namespace Moesif.Middleware.Helpers
{
    public class Tasks
    {
        public List<EventModel> QueueGetAll(Queue<EventModel> moesifQueue, int batchSize)
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
                    events.Add(moesifQueue.Dequeue());
                }
                catch (Exception ex)
                {
                    break;
                }
            }
            
            return events;
        }

        public async Task<(Api.Http.Response.HttpStringResponse, String, int, DateTime)> AsyncClientCreateEvent(MoesifApiClient client, Queue<EventModel> MoesifQueue,
            int batchSize, bool debug, Api.Http.Response.HttpStringResponse defaultConfig, string configETag, int samplingPercentage, DateTime lastUpdatedTime,
            AppConfig appConfig)
        {
            List<EventModel> batchEvents = new List<EventModel>();

            while (MoesifQueue.Count > 0)
            {
                batchEvents = QueueGetAll(MoesifQueue, batchSize);

                if ((batchEvents.Any()))
                {
                    // Send Batch Request
                    var createBatchEventResponse = await client.Api.CreateEventsBatchAsync(batchEvents);
                    var batchEventResponseConfigETag = createBatchEventResponse.ToDictionary(k => k.Key.ToLower(), k => k.Value)["x-moesif-config-etag"];

                    if (!(string.IsNullOrEmpty(batchEventResponseConfigETag)) &&
                        !(string.IsNullOrEmpty(configETag)) &&
                        configETag != batchEventResponseConfigETag &&
                        DateTime.UtcNow > lastUpdatedTime.AddMinutes(5))
                    {
                        try
                        {
                            Api.Http.Response.HttpStringResponse config;
                            // Get Application config
                            config = await appConfig.getConfig(client, debug);
                            if (!string.IsNullOrEmpty(config.ToString()))
                            {
                                (configETag, samplingPercentage, lastUpdatedTime) = appConfig.parseConfiguration(config, debug);
                                return (config, configETag, samplingPercentage, lastUpdatedTime);
                            }
                        }
                        catch (Exception ex)
                        {
                            if (debug)
                            {
                                Console.WriteLine("Error while updating the application configuration");
                            }
                        }
                    }
                    if (debug)
                    {
                        Console.WriteLine("Events sent successfully to Moesif");
                    }
                }
                else
                {
                    if (debug)
                    {
                        Console.WriteLine("No events in the queue");
                    }
                    return (defaultConfig, configETag, samplingPercentage, lastUpdatedTime);
                }
            }
            if (debug)
            {
                Console.WriteLine("No events in the queue");
            }
            return (defaultConfig, configETag, samplingPercentage, lastUpdatedTime);
        }
    }
}
