using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eduard.Compression
{
    public class LempelZiv
    {
        const int RingBufferSize = 65536;
        const int UpperMatchLength = 259;

        const int LowerMatchLength = 3;
        const int None = RingBufferSize;

        static readonly int[] Parent = new int[RingBufferSize + 1];
        static readonly int[] LeftChild = new int[RingBufferSize + 1];

        static readonly int[] RightChild = new int[RingBufferSize + 257];
        static readonly ushort[] Buffer = new ushort[RingBufferSize + UpperMatchLength - 1];
        static int matchPosition, matchLength;

        /// <summary>
        ///     Size of the compressed data during and after compression.
        /// </summary>
        public static int CompressedSize { get; set; }
        /// <summary>
        ///     Size of the original data after extraction.
        /// </summary>
        public static int OriginalSize { get; set; }
        public static double CompressionRatio => (double)CompressedSize / OriginalSize * 100.0;

        public static byte[] Extract(byte[] data)
        {
            if (data == null) throw new Exception("Input buffer is null.");
            if (data.Length == 0) throw new Exception("Input buffer is empty.");

            var outa = new MemoryStream();
            var ina = new MemoryStream(data);

            byte[] len = new byte[4];
            CompressedSize = 0;

            ina.Read(len, 0, 4);
            OriginalSize = BitConverter.ToInt32(len, 0);

            for (var i = 0; i < RingBufferSize - UpperMatchLength; i++)
                Buffer[i] = 0;

            var r = RingBufferSize - UpperMatchLength;
            uint flags = 7;
            var z = 7;

            while (true)
            {
                flags <<= 1;
                z++;

                if (z == 8)
                {
                    if (ina.Position == ina.Length) break;
                    flags = (uint)ina.ReadByte();
                    z = 0;
                }

                if ((flags & 0x80) == 0)
                {
                    if (ina.Position == ina.Length) break;
                    int c = ina.ReadByte();

                    if (CompressedSize < OriginalSize)
                        outa.WriteByte((byte)c);

                    Buffer[r++] = (ushort)c;
                    r &= RingBufferSize - 1;
                    CompressedSize++;
                }
                else
                {
                    if (ina.Position == ina.Length) break;
                    int i = ina.ReadByte();

                    if (ina.Position == ina.Length) break;
                    int j = ina.ReadByte();

                    if (ina.Position == ina.Length) break;
                    int size = ina.ReadByte();

                    i = (i << 8) | j;
                    size += LowerMatchLength;

                    for (int k = 0; k <= size; k++)
                    {
                        var c = Buffer[(r - i - 1) & (RingBufferSize - 1)];
                        if (CompressedSize < OriginalSize) outa.WriteByte((byte)c);
                        Buffer[r++] = (byte)c;

                        r &= RingBufferSize - 1;
                        CompressedSize++;
                    }
                }
            }

            return outa.ToArray();
        }

        public static byte[] Compress(byte[] data)
        {
            if (data == null) throw new Exception("Input buffer is null.");
            if (data.Length == 0) throw new Exception("Input buffer is empty.");

            matchLength = 0;
            matchPosition = 0;

            CompressedSize = 0;
            OriginalSize = data.Length;
            int length;

            var stream = new MemoryStream();
            var codeBuffer = new int[UpperMatchLength - 1];
            var outa = new BinaryStream(stream);

            var ina = new MemoryStream(data);
            byte[] array = BitConverter.GetBytes(OriginalSize);

            for (int i = 0; i < array.Length; i++)
                outa.WriteByte(array[i]);

            InitTree();
            codeBuffer[0] = 0;

            var codeBufferPointer = 1;
            var mask = 0x80;
            var s = 0;

            var r = RingBufferSize - UpperMatchLength;
            for (var i = s; i < r; i++) Buffer[i] = 0xFFFF;

            for (length = 0; length < UpperMatchLength && ina.Position != ina.Length; length++)
                Buffer[r + length] = (ushort)ina.ReadByte();

            if (length == 0) throw new Exception("No Data to Compress.");
            for (var i = 1; i <= UpperMatchLength; i++) InsertNode(r - i);
            InsertNode(r);

            do
            {
                if (matchLength > length)
                    matchLength = length;

                if (matchLength <= LowerMatchLength)
                {
                    matchLength = 1;
                    codeBuffer[codeBufferPointer++] = Buffer[r];
                }
                else
                {
                    codeBuffer[0] |= mask;
                    codeBuffer[codeBufferPointer++] = ((r - matchPosition - 1) >> 8) & 0xFF;
                    codeBuffer[codeBufferPointer++] = (r - matchPosition - 1) & 0xFF;
                    codeBuffer[codeBufferPointer++] = matchLength - LowerMatchLength - 1;
                }

                if ((mask >>= 1) == 0)
                {
                    for (var i = 0; i < codeBufferPointer; i++)
                        outa.WriteByte((byte)codeBuffer[i]);

                    CompressedSize += codeBufferPointer;
                    codeBuffer[0] = 0;

                    codeBufferPointer = 1;
                    mask = 0x80;
                }

                var lastMatchLength = matchLength;
                var ii = 0;

                for (ii = 0; ii < lastMatchLength && ina.Position != ina.Length; ii++)
                {
                    DeleteNode(s);
                    var c = ina.ReadByte();
                    Buffer[s] = (ushort)c;

                    if (s < UpperMatchLength - 1) Buffer[s + RingBufferSize] = (ushort)c;
                    s = (s + 1) & (RingBufferSize - 1);
                    r = (r + 1) & (RingBufferSize - 1);
                    InsertNode(r);
                }

                while (ii++ < lastMatchLength)
                {
                    DeleteNode(s);
                    s = (s + 1) & (RingBufferSize - 1);

                    r = (r + 1) & (RingBufferSize - 1);
                    if (--length != 0) InsertNode(r);
                }
            } while (length > 0);

            if (codeBufferPointer > 1)
            {
                for (int i = 0; i < codeBufferPointer; i++)
                    outa.WriteByte((byte)codeBuffer[i]);

                CompressedSize += codeBufferPointer;
            }

            if (CompressedSize % 4 != 0)
                for (int i = 0; i < 4 - CompressedSize % 4; i++)
                    outa.WriteBit(0);

            byte[] res = stream.ToArray();
            outa.Close();
            return res;
        }

        static void InitTree()
        {
            for (int i = RingBufferSize + 1; i <= RingBufferSize + 256; i++)
                RightChild[i] = None;

            for (int i = 0; i < RingBufferSize; i++)
                Parent[i] = None;
        }

        static void InsertNode(int r)
        {
            int cmp = 1;
            int p = RingBufferSize + 1 + (Buffer[r] == 0xFFFF ? 0 : Buffer[r]);

            RightChild[r] = LeftChild[r] = None;
            matchLength = 0;

            while (true)
            {
                if (cmp >= 0)
                {
                    if (RightChild[p] != None) p = RightChild[p];
                    else
                    {
                        RightChild[p] = r;
                        Parent[r] = p;
                        return;
                    }
                }
                else
                {
                    if (LeftChild[p] != None) p = LeftChild[p];
                    else
                    {
                        LeftChild[p] = r;
                        Parent[r] = p;
                        return;
                    }
                }

                int i;

                for (i = 1; i < UpperMatchLength; i++)
                    if ((cmp = Buffer[r + i] - Buffer[p + i]) != 0)
                        break;

                if (i > matchLength)
                {
                    matchPosition = p;
                    if ((matchLength = i) >= UpperMatchLength)
                        break;
                }
            }

            Parent[r] = Parent[p];
            LeftChild[r] = LeftChild[p];

            RightChild[r] = RightChild[p];
            Parent[LeftChild[p]] = r;
            Parent[RightChild[p]] = r;

            if (RightChild[Parent[p]] == p) RightChild[Parent[p]] = r;
            else LeftChild[Parent[p]] = r;
            Parent[p] = None;
        }

        static void DeleteNode(int p)
        {
            int q;
            if (Parent[p] == None) return;
            if (RightChild[p] == None) q = LeftChild[p];
            else if (LeftChild[p] == None) q = RightChild[p];
            else
            {
                q = LeftChild[p];
                if (RightChild[q] != None)
                {
                    do
                        q = RightChild[q];
                    while (RightChild[q] != None);

                    RightChild[Parent[q]] = LeftChild[q];
                    Parent[LeftChild[q]] = Parent[q];

                    LeftChild[q] = LeftChild[p];
                    Parent[LeftChild[p]] = q;
                }

                RightChild[q] = RightChild[p];
                Parent[RightChild[p]] = q;
            }

            Parent[q] = Parent[p];
            if (RightChild[Parent[p]] == p) RightChild[Parent[p]] = q;
            else LeftChild[Parent[p]] = q;
            Parent[p] = None;
        }
    }
}
