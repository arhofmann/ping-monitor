using System;
using PingMonitor.Core;

namespace PingMonitor.Core.Tests
{
    public static class DnsMessageTests
    {
        public static void QueryAsksForTheAddressOfTheName()
        {
            byte[] query = DnsMessage.Query("example.com", 0x1234);

            Check.Equal("12-34-01-00-00-01-00-00-00-00-00-00-07-65-78-61-6D-70-6C-65-03-63-6F-6D-00-00-01-00-01",
                        BitConverter.ToString(query));
        }

        public static void TrailingDotChangesNothing()
        {
            Check.Equal(BitConverter.ToString(DnsMessage.Query("example.com", 1)), BitConverter.ToString(DnsMessage.Query("example.com.", 1)));
        }

        public static void NameBeyondAsciiTravelsAsPunycode()
        {
            byte[] query = DnsMessage.Query("bücher.example", 1);

            // 13 = the length of "xn--bcher-kva".
            Check.Equal(13, (int)query[12]);
            Check.Equal("xn--bcher-kva", System.Text.Encoding.ASCII.GetString(query, 13, 13));
        }

        public static void NameThatIsNotValidIsRejected()
        {
            CheckRejected("");
            CheckRejected("example..com");
            CheckRejected(new string('a', 64) + ".com");
        }

        public static void ResponseWithoutErrorAnswersTheQuery()
        {
            byte[] query = DnsMessage.Query("example.com", 0x1234);

            Check.True(DnsMessage.Answers(query, Response(0x12, 0x34, 0x81, 0x80), 12), "a response without an error answers the query");
        }

        public static void OnlyTheResponseToThisQueryCounts()
        {
            byte[] query = DnsMessage.Query("example.com", 0x1234);

            Check.True(!DnsMessage.Answers(query, Response(0x12, 0x35, 0x81, 0x80), 12), "another id belongs to another query");
            Check.True(!DnsMessage.Answers(query, query, query.Length), "a query is not a response");
            Check.True(!DnsMessage.Answers(query, Response(0x12, 0x34, 0x81, 0x80), 11), "a header that is cut off says nothing");
        }

        // SERVFAIL is what a router answers with when the servers it would have to ask itself cannot be reached.
        public static void ResponseThatReportsAnErrorDoesNotAnswerTheQuery()
        {
            byte[] query = DnsMessage.Query("example.com", 0x1234);

            Check.True(!DnsMessage.Answers(query, Response(0x12, 0x34, 0x81, 0x82), 12), "SERVFAIL");
            Check.True(!DnsMessage.Answers(query, Response(0x12, 0x34, 0x81, 0x83), 12), "NXDOMAIN");
            Check.True(!DnsMessage.Answers(query, Response(0x12, 0x34, 0x81, 0x85), 12), "REFUSED");
        }

        // The header of a response: the id in two bytes, then the two bytes of flags. The counts that follow do not matter here.
        private static byte[] Response(byte idHigh, byte idLow, byte flagsHigh, byte flagsLow)
        {
            return new byte[] { idHigh, idLow, flagsHigh, flagsLow, 0, 1, 0, 1, 0, 0, 0, 0 };
        }

        private static void CheckRejected(string name)
        {
            try
            {
                DnsMessage.Query(name, 1);
            }
            catch (ArgumentException)
            {
                return;
            }
            throw new Exception("<" + name + "> should not make a query");
        }
    }
}
