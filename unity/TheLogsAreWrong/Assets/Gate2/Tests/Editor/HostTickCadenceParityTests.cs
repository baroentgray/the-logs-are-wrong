using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using TheLogsAreWrong.Domain.Runtime;

namespace TheLogsAreWrong.Gate2.Tests
{
    /// <summary>
    /// TLAW-068 parity proof. It exercises the imported PortableAuthority cadence only; this file owns no cadence
    /// implementation and does not execute a host session.
    /// </summary>
    public sealed class HostTickCadenceParityTests
    {
        private const string CanonicalCadenceProjectionSha = "A3CFED2906266153792A1B9FFFB2CBE6EE48F450342EF933B9DAD515DD0BADA0";

        [Test]
        public void Imported_integer_cadence_matches_the_canonical_net10_projection()
        {
            var first = Projection();
            var second = Projection();

            Assert.AreEqual(first, second);
            Assert.AreEqual(CanonicalCadenceProjectionSha, Sha256(first));
        }

        private static string Projection()
        {
            var cadence = new HostTickCadence();
            var projection = new StringBuilder();
            foreach (var elapsed in new long[] { 400, 599, 1, 2000, 2500, 0, 1000 })
            {
                cadence.Accumulate(AuthoritativeElapsedMilliseconds.FromMilliseconds(elapsed));
                var due = cadence.GetDueTickRange();
                projection.Append(elapsed.ToString(CultureInfo.InvariantCulture));
                projection.Append('|');
                projection.Append(cadence.RemainderMilliseconds.Value.ToString(CultureInfo.InvariantCulture));
                projection.Append('|');
                projection.Append(cadence.DueTickCount.ToString(CultureInfo.InvariantCulture));
                projection.Append('|');
                projection.Append(due is null
                    ? "-"
                    : string.Format(
                        CultureInfo.InvariantCulture,
                        "{0}-{1}",
                        due.Value.First.Value,
                        due.Value.Last.Value));
                projection.Append('\n');
            }

            return projection.ToString();
        }

        private static string Sha256(string value)
        {
            using (var algorithm = SHA256.Create())
            {
                return BitConverter.ToString(algorithm.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", string.Empty);
            }
        }
    }
}
