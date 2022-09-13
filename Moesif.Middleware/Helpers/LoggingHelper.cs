using System;
namespace Moesif.Middleware.Helpers
{
    // Async Logging
    public class LoggingHelper  
    {
        public async static void LogDebugMessage(bool debug, String msg)
        {
            if (debug)
            {
                await Console.Out.WriteLineAsync(msg);
            }
        }

        public async static void LogMessage(String msg)
        {
            await Console.Out.WriteLineAsync(msg);
        }
    }
}

