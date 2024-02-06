/*
* BUDDYWORKS UnityPackage Icon Tool
* Copyright (C) 2024 BUDDYWORKS
* hi@buddyworks.wtf

* This program is free software; you can redistribute it and/or
* modify it under the terms of the GNU Lesser General Public
* License as published by the Free Software Foundation; either
* version 3 of the License, or (at your option) any later version.

* This program is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
* Lesser General Public License for more details.

* You should have received a copy of the GNU Lesser General Public License
* along with this program; if not, write to the Free Software Foundation,
* Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
*/

using ICSharpCode.SharpZipLib.Tar;
using System.IO;

namespace BUDDYWORKS.UnityPackageIcon
{
    // TarArchive and TarOutputStream could not take a byte array or an arbitrary stream to write, wtf? -- Dor
    internal static class TarOutputStreamExtensions
    {
        public static void WriteEntry(this TarOutputStream tarOutput, TarEntry entry, Stream stream)
        {
            entry.Size = stream.Length;

            tarOutput.PutNextEntry(entry);

            byte[] localBuffer = new byte[32 * 1024];
            while (true)
            {
                int numRead = stream.Read(localBuffer, 0, localBuffer.Length);

                if (numRead <= 0)
                {
                    break;
                }

                tarOutput.Write(localBuffer, 0, numRead);
            }

            tarOutput.CloseEntry();
        }

        public static void WriteEntry(this TarOutputStream tarOutput, TarEntry entry, byte[] data)
        {
            entry.Size = data.Length;

            tarOutput.PutNextEntry(entry);

            tarOutput.Write(data, 0, data.Length);

            tarOutput.CloseEntry();
        }

        public static void WriteEntry(this TarOutputStream tarOutput, TarEntry entry)
        {
            if (string.IsNullOrEmpty(entry.File)) return;

            using (FileStream stream = File.OpenRead(entry.File)) tarOutput.WriteEntry(entry, stream);
        }
    }
}