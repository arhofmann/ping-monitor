// Tests for the core of Ping Monitor. Build and run: dotnet run --project tests/PingMonitor.Core.Tests
// They get by without a test framework and need nothing from Windows, so they run wherever the core runs.

using System;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace PingMonitor.Core.Tests
{
    internal static class Program
    {
        // A test is every public static method without parameters in a class whose name ends with "Tests".
        // It fails by throwing an exception.
        private static int Main()
        {
            Console.OutputEncoding = Encoding.UTF8;
            int total = 0, failed = 0;
            foreach (Type type in typeof(Program).Assembly.GetTypes())
            {
                if (!type.Name.EndsWith("Tests", StringComparison.Ordinal)) continue;
                foreach (MethodInfo test in type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    if (test.GetParameters().Length > 0) continue;
                    total++;
                    // The texts a test expects must not depend on how this machine is set up. A test that is about
                    // regional settings picks its own culture; the next one starts neutral again.
                    CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
                    try
                    {
                        test.Invoke(null, null);
                    }
                    catch (TargetInvocationException ex)
                    {
                        failed++;
                        Console.WriteLine("FAILED  " + type.Name + "." + test.Name + ": " + ex.InnerException.Message);
                    }
                }
            }

            Console.WriteLine(total + " tests, " + failed + " failed");
            // Finding not a single test counts as a failure - otherwise an empty run would look like a passed one.
            return total > 0 && failed == 0 ? 0 : 1;
        }
    }
}
