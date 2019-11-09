using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ASI.Barista.Plugins.Imaging.Similarity
{
    internal class ImageDhash
    {
        public BigInteger ComputeHash(Bitmap image, int size = 8)
        {
            var width = size + 1;
            var grays = image.ToGrayScale().Resize(width, width).ToArray();

            var rowHash = new BigInteger(0);
            var colHash = new BigInteger(0);

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var offset = y * width + x;
                    var rowBit = grays[offset] < grays[offset + 1] ? (byte)1 : (byte)0;
                    rowHash = (rowHash << 1) | rowBit;

                    var colBit = grays[offset] < grays[offset + width] ? (byte)1 : (byte)0;
                    colHash = (colHash << 1) | colBit;
                }
            }

            //return (RowHash: rowHash, ColumnHash: colHash);
            return rowHash << (size * size) | colHash;
        }

        public static int HammingDistance(BigInteger first, BigInteger second)
        {
            var diff = first ^ second;
            var bits = new BitArray(diff.ToByteArray());

            var distance = bits.Cast<bool>().Count(x => x);

            return distance;
        }

        public static int LevenshteinDistance(BigInteger first, BigInteger second)
        {
            var data1 = first.ToByteArray();
            var data2 = second.ToByteArray();

            var lenFirst = data1.Length;
            var lenSecond = data2.Length;

            var d = new int[lenFirst + 1, lenSecond + 1];

            for (var i = 0; i <= lenFirst; i++)
                d[i, 0] = i;

            for (var i = 0; i <= lenSecond; i++)
                d[0, i] = i;

            for (var i = 1; i <= lenFirst; i++)
            {
                for (var j = 1; j <= lenSecond; j++)
                {
                    var match = (data1[i - 1] == data2[j - 1]) ? 0 : 1;

                    d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + match);
                }
            }

            return d[lenFirst, lenSecond];
        }

        public static double Score(BigInteger first, BigInteger second, Func<BigInteger, BigInteger, int> findDistance, out int distance, int size = 8)
        {
            distance = findDistance(first, second);
            var bits = size * size * 2.0;
            if (size <= 0)
            {
                bits = Math.Min(first.ToByteArray().Length, second.ToByteArray().Length);
            }
            return 1.0 - distance / bits;
        }
    }
}
