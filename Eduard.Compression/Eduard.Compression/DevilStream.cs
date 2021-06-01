/*
 BSD 3-Clause License

 Copyright (c) 2020, Matei Ionut-Eduard
 All rights reserved.

 Redistribution and use in source and binary forms, with or without
 modification, are permitted provided that the following conditions are met:

 1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.

 2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

 3. Neither the name of the copyright holder nor the names of its
   contributors may be used to endorse or promote products derived from
   this software without specific prior written permission.

 THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
 AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
 IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
 FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
 DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
 SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
 CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
 OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
 OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

using System;
using System.IO;
using System.Diagnostics;

namespace Eduard.Compression
{
    [DebuggerStepThrough]
    public class DevilStream : Stream
    {
        private DevilAccess mode;
        private Stream stream;
        private byte[] data;
        private int index;

        public DevilStream(Stream stream, DevilAccess mode)
        {
            this.mode = mode;
            this.stream = stream;
            data = new byte[65536];
            index = 0;
        }

        private int CopyBlock(byte[] buffer, int offset, int count)
        {
            int len = count - offset;
            int min = (index < len) ? index : len;

            if(min == 0 && stream.Position < stream.Length)
            {
                byte[] block = AdaptiveHuffman.Extract(stream);
                byte[] chunk = QuickLZ.decompress(block);
                Buffer.BlockCopy(chunk, 0, data, index, chunk.Length);
                index = chunk.Length;
            }

            min = (index < len) ? index : len;
            Buffer.BlockCopy(data, 0, buffer, offset, min);

            for (int i = min; i < index; i++)
            {
                data[i - min] = data[i];
                data[i] = 0;
            }

            index -= min;
            return min;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (mode == DevilAccess.Compress) throw new Exception("This stream is used to extract only.");
            int len = CopyBlock(buffer, offset, count);
            return len;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (offset < 0 || offset >= count) throw new ArgumentException("Offset must be valid.");
            if(mode == DevilAccess.Extract) throw new Exception("This stream is used to compress only.");
            if (index == 65536) Flush();

            int len = count - offset;
            int min = ((65536 - index) < len) ? (65536 - index) : len;
            Buffer.BlockCopy(buffer, offset, data, index, min);
            index += min;
        }

        public override long Seek(long offset, SeekOrigin origin)
        { throw new NotSupportedException(); }

        public override void SetLength(long value)
        { throw new IOException("The length of this stream can not be changed."); }

        public override bool CanRead
        {
            get
            {
                if (stream == null) return false;
                return (mode == DevilAccess.Extract && stream.CanRead);
            }
        }

        public override bool CanSeek
        { get { return false; } }

        public override bool CanWrite
        {
            get
            {
                if (stream == null) return false;
                return (mode == DevilAccess.Compress && stream.CanWrite);
            }
        }

        public override long Length
        {
            get { throw new NotSupportedException(); }
        }

        public override long Position
        {
            get { throw new NotSupportedException(); }
            set { throw new NotSupportedException(); }
        }

        public override void Close()
        {
            if (mode == DevilAccess.Compress)
            {
                if (index > 0)
                    Flush();
            }

            stream.Close();
        }

        public override void Flush()
        {
            byte[] buffer = new byte[index];
            Buffer.BlockCopy(data, 0, buffer, 0, index);

            byte[] chunk = QuickLZ.compress(buffer, 3);
            byte[] block = AdaptiveHuffman.Compress(chunk);
            stream.Write(block, 0, block.Length);
            index = 0;
        }
    }
}
