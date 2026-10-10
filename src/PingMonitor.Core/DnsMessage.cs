using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace PingMonitor.Core
{
    // The few bytes of the DNS protocol (RFC 1035) that it takes to ask for a name and to tell whether it was resolved.
    internal static class DnsMessage
    {
        private const int HeaderLength = 12;

        // A query for the IPv4 address of "name". "id" comes back in the response and tells which query it belongs to.
        public static byte[] Query(string name, ushort id)
        {
            if (name == null) throw new ArgumentNullException("name");

            // Names with characters beyond ASCII travel in their Punycode form; a name that is not valid makes this throw.
            string ascii = new IdnMapping().GetAscii(name.TrimEnd('.'));

            List<byte> message = new List<byte>();
            message.Add((byte)(id >> 8));
            message.Add((byte)id);
            // Flags: an ordinary query that asks the server to do the whole lookup itself ("recursion desired").
            message.Add(0x01);
            message.Add(0x00);
            // One question; no answers, no authority and no additional records.
            message.AddRange(new byte[] { 0, 1, 0, 0, 0, 0, 0, 0 });

            // The name, label by label, each preceded by its length; an empty label ends it.
            foreach (string label in ascii.Split('.'))
            {
                message.Add((byte)label.Length);
                message.AddRange(Encoding.ASCII.GetBytes(label));
            }
            message.Add(0);
            // Type A (an IPv4 address), class IN (internet).
            message.AddRange(new byte[] { 0, 1, 0, 1 });
            return message.ToArray();
        }

        // True if the first "length" bytes of "response" are a server's response to "query" and report no error.
        // A server that cannot reach the servers it would have to ask itself answers too - with an error.
        public static bool Answers(byte[] query, byte[] response, int length)
        {
            if (length < HeaderLength) return false;
            if (response[0] != query[0] || response[1] != query[1]) return false;

            bool isResponse = (response[2] & 0x80) != 0;
            int responseCode = response[3] & 0x0F;
            return isResponse && responseCode == 0;
        }
    }
}
