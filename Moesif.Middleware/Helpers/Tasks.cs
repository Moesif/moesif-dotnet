﻿using System;
using System.Linq;
using System.Collections.Generic;
using Moesif.Api;
using Moesif.Api.Models;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Moesif.Middleware.Models;

namespace Moesif.Middleware.Helpers
{
    public class Tasks
    {
        public List<EventModel> QueueGetAll(ConcurrentQueue<EventModel> moesifQueue, int batchSize)
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

        public async Task<AppConfig> AsyncClientCreateEvent(MoesifApiClient client, ConcurrentQueue<EventModel> MoesifQueue,
            AppConfig prevConfig, int batchSize, bool debug)
        {
            List<EventModel> batchEvents = new List<EventModel>();
            var appConfig = prevConfig;

            while (MoesifQueue.Count > 0)
            {
                batchEvents = QueueGetAll(MoesifQueue, batchSize);

                if ((batchEvents.Any()))
                {
                    try
                    {
                        // Send Batch Request
                        var createBatchEventResponse = await client.Api.CreateEventsBatchAsync(batchEvents);
                        var batchEventResponseConfigETag = createBatchEventResponse.ToDictionary(k => k.Key.ToLower(), k => k.Value)["x-moesif-config-etag"];
                        if (!(string.IsNullOrEmpty(batchEventResponseConfigETag)) &&
                            !(string.IsNullOrEmpty(prevConfig.etag)) &&
                            prevConfig.etag != batchEventResponseConfigETag &&
                            DateTime.UtcNow > prevConfig.lastUpdatedTime.AddMinutes(5))
                        {
                            try
                            {
                                // Get Application config
                                appConfig = await AppConfigHelper.getConfig(client, prevConfig, debug);
                              
                            }
                            catch (Exception)
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
                    catch (Exception)
                    {
                        if (debug)
                        {
                            Console.WriteLine("Could not connect to Moesif server.");
                        }
                    }
                }
                else
                {
                    if (debug)
                    {
                        Console.WriteLine("No events in the queue");
                    }
                }
            }
            if (debug)
            {
                Console.WriteLine("No events in the queue");
            }
            return appConfig;
        }
    }
}
