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
using System.IO.Compression;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Eduard.Compression
{
    /// <summary>
    /// Represents the devil archive.
    /// </summary>
    [DebuggerStepThrough]
    public class DevilArchive
    {
        private FileAccess mode;
        private string path = string.Empty;
        private MemoryStream ms;

        public DevilArchive(string path, FileAccess mode)
        {
            this.mode = mode;
            Entries = new List<DevilEntry>();

            if (mode == FileAccess.Read)
            {
                ms = new MemoryStream(File.ReadAllBytes(path));
                Flush();
            }
            else
            {
                ms = new MemoryStream();
                this.path = path;
            }
        }

        /// <summary>
        /// Represents the entries from this archive.
        /// </summary>
        public List<DevilEntry> Entries { get; set; }

        private void Flush()
        {
            if (mode == FileAccess.Write)
            {
                // Build and write compressed header.
                MemoryStream header = new MemoryStream();
                GZipStream gstream = new GZipStream(header, CompressionMode.Compress);

                foreach (DevilEntry entry in Entries)
                {
                    byte[] bytes = Encoding.ASCII.GetBytes(entry.GetPath());
                    gstream.Write(bytes, 0, bytes.Length);
                    gstream.WriteByte(32);
                }

                gstream.Close();
                byte[] array = header.ToArray();
                byte[] size = Convert((uint)array.Length);
                ms.Write(size, 0, 4);
                byte[] block = header.ToArray();
                ms.Write(block, 0, block.Length);

                foreach(DevilEntry entry in Entries)
                {
                    if(entry is DevilFile)
                    {
                        DevilFile file = entry as DevilFile;
                        byte[] uncompressed = Convert(file.Length);
                        ms.Write(uncompressed, 0, 4);
                        byte[] compressed = Convert((uint)file.stream.Length);
                        ms.Write(compressed, 0, 4);
                    }
                }

                foreach (DevilEntry entry in Entries)
                {
                    if (entry is DevilFile)
                    {
                        DevilFile file = entry as DevilFile;
                        byte[] buffer = file.stream.ToArray();
                        ms.Write(buffer, 0, buffer.Length);
                    }
                }
            }
            else
            {
                // Read the entire header, decompress it and get the archive entries.
                byte[] buffer = new byte[4];
                ms.Read(buffer, 0, 4);

                uint size = Convert(buffer);
                buffer = new byte[size];

                ms.Read(buffer, 0, buffer.Length);
                MemoryStream output = new MemoryStream();

                using (MemoryStream input = new MemoryStream(buffer))
                {
                    GZipStream gstream = new GZipStream(input, CompressionMode.Decompress);
                    byte[] chunk = new byte[8192];
                    int len = 0;

                    while ((len = gstream.Read(chunk, 0, chunk.Length)) != 0)
                        output.Write(chunk, 0, len);
                }

                string[] all = Encoding.ASCII.GetString(output.ToArray()).Split(' ');
                List<uint> list = new List<uint>();

                foreach(string str in all)
                {
                    string ext = Path.GetExtension(str);

                    if(!string.IsNullOrEmpty(ext))
                    {
                        buffer = new byte[4];
                        ms.Read(buffer, 0, 4);

                        DevilFile file = new DevilFile(str);
                        file.Length = Convert(buffer);

                        buffer = new byte[4];
                        ms.Read(buffer, 0, 4);
                        list.Add(Convert(buffer));
                        Entries.Add(file);
                    }
                    else
                    {
                        DevilFolder entry = new DevilFolder(str);
                        Entries.Add(entry);
                    }
                }

                foreach(DevilEntry entry in Entries)
                {
                    if(entry is DevilFile)
                    {
                        buffer = new byte[list[0]];
                        ms.Read(buffer, 0, buffer.Length);

                        DevilFile file = entry as DevilFile;
                        file.stream = new MemoryStream(buffer);
                        list.RemoveAt(0);
                    }
                }
            }
        }

        private static byte[] Convert(uint val)
        {
            byte[] bytes = BitConverter.GetBytes(val);

            if (BitConverter.IsLittleEndian)
                Array.Reverse(bytes);

            return bytes;
        }

        private static uint Convert(byte[] val)
        {
            if (BitConverter.IsLittleEndian)
                Array.Reverse(val);

            var result = BitConverter.ToUInt32(val, 0);
            return result;
        }

        /// <summary>
        /// Close the archive stream.
        /// </summary>
        public void Close()
        {
            if(mode == FileAccess.Write)
            {
                Flush();
                ms.WriteTo(new FileStream(path, FileMode.Create));
            }

            ms.Close();
        }
    }
}
