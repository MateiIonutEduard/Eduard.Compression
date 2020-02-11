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
namespace Eduard.Compression
{
    public class BinaryStream
    {
        private Stream stream;
        private int bits, len;

        public BinaryStream(Stream stream)
        {
            this.stream = stream;
            bits = 0;
            len = 0;
        }

        public int ReadBit()
        {
            if(len == 0)
            {
                bits = stream.ReadByte();
                if (bits == -1) return -1;
                len = 8;
            }

            len--;
            return (bits >> len) & 1;
        }

        public int ReadByte()
        {
            int result = 0;
            int mask = 0x80;

            while(mask != 0)
            {
                int bit = ReadBit();
                if (bit == -1) return -1;
                if (bit == 1) result |= mask;
                mask >>= 1;
            }

            return result;
        }

        public void WriteBit(int bit)
        {
            bits <<= 1;
            if (bit == 1) bits |= 1;
            len++;
            if (len == 8) Flush();
        }

        public void Flush()
        {
            if (len == 0) return;
            if(len > 0) bits <<= (8 - len);
            stream.WriteByte((byte)(bits & 0xFF));
            bits = 0;
            len = 0;
        }

        public void WriteByte(byte val)
        {
            for (int i = 0; i < 8; i++)
            {
                int bit = (val >> (8 - i - 1)) & 1;
                WriteBit(bit);
            }
        }

        public void Close()
        { stream.Close(); }
    }
}
