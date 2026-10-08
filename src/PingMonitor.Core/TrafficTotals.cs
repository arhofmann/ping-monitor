namespace PingMonitor.Core
{
    // A counter reading of a network interface: how many bytes it has received and sent so far.
    public struct TrafficTotals
    {
        // Identifier of the interface - counter readings of different interfaces cannot be compared.
        public readonly string InterfaceId;
        public readonly long BytesReceived;
        public readonly long BytesSent;

        public TrafficTotals(string interfaceId, long bytesReceived, long bytesSent)
        {
            InterfaceId = interfaceId;
            BytesReceived = bytesReceived;
            BytesSent = bytesSent;
        }
    }
}
