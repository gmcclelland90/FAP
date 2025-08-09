using System.Threading;

namespace FAP.Shared
{
    public static class FapMetrics
    {
        public static long ChatReceived;          // Server: CHAT received
        public static long ChatForwarded;         // Server: CHAT forwarded (sum of recipients)
        public static long ChatFailures;          // Server: forwarding failures
        public static long ClientChatReceived;    // Client: CHAT received and displayed

        public static long ConversationSent;      // Client: 1:1 sent
        public static long ConversationDelivered; // Client: 1:1 delivered/handled

        public static void Inc(ref long counter) => Interlocked.Increment(ref counter);
        public static void Add(ref long counter, long value) => Interlocked.Add(ref counter, value);
        public static long Read(ref long counter) => Interlocked.Read(ref counter);
    }
}


