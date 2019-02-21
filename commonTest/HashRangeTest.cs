using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Fetcho.Common.Tests
{

    [TestClass]
    public class HashRangeTest
    {
        [TestMethod]
        public void ToStringTest()
        {
            var hash = MD5Hash.MinValue;

            Assert.IsTrue(hash.ToString().Length == MD5Hash.ExpectedStringLength);
            Assert.IsTrue(hash.ToString() == "00000000000000000000000000000000", hash.ToString());
        }

        [TestMethod]
        public void GetBigIntegerFromBytesTest()
        {
            var a = MD5Hash.MaxValue.Values;
            var b = MD5Hash.MinValue.Values;
            var c = new MD5Hash("A0000000000000A00000000000000000").Values;

            Assert.IsTrue(a.SequenceEqual(HashRange.GetBytesFromBigInteger(HashRange.GetBigIntegerFromBytes(a),
                                                                             MD5Hash.ExpectedByteLength)));
            Assert.IsTrue(b.SequenceEqual(HashRange.GetBytesFromBigInteger(HashRange.GetBigIntegerFromBytes(b),
                                                                             MD5Hash.ExpectedByteLength)));
            Assert.IsTrue(c.SequenceEqual(HashRange.GetBytesFromBigInteger(HashRange.GetBigIntegerFromBytes(c),
                                                                             MD5Hash.ExpectedByteLength)));

        }

        [TestMethod]
        public void SegmentRangeTest()
        {
            var range = new HashRange(MD5Hash.MinValue, MD5Hash.MaxValue);

            var ranges = HashRange.SegmentRange(range, new decimal[] { 0.50m, 0.50m });

            Assert.IsTrue(ranges[0].MinHash == new MD5Hash("00000000000000000000000000000000"),
                          ranges[0].MinHash + " " + ranges[0].MaxHash);
            Assert.IsTrue(ranges[0].MaxHash == new MD5Hash("7FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF"),
                          ranges[0].MinHash + " " + ranges[0].MaxHash);

            Assert.IsTrue(ranges[1].MinHash == new MD5Hash("80000000000000000000000000000000"),
                          ranges[1].MinHash + " " + ranges[1].MaxHash);
            Assert.IsTrue(ranges[1].MaxHash == new MD5Hash("FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF"),
                          ranges[1].MinHash + " " + ranges[1].MaxHash);

        }
    }
}
