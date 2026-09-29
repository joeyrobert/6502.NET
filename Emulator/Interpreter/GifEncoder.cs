/* Copyright (c) 2009 Joseph Robert. All rights reserved.
 *
 * This file is part of 6502.NET.
 *
 * 6502.NET is free software; you can redistribute it and/or modify
 * it under the terms of the GNU Lesser General Public License as
 * published by the Free Software Foundation; either version 3.0  of
 * the License, or (at your option) any later version.
 *
 * 6502.NET is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with 6502.NET.  If not, see <http://www.gnu.org/licenses/>.
 */

namespace Emulator.Interpreter;

/// <summary>Minimal animated GIF89a writer using the fixed 16 colour <see cref="Palette"/>.</summary>
public static class GifEncoder
{
    /// <param name="frames">Screen snapshots from <see cref="Palette.Snapshot"/>.</param>
    /// <param name="scale">Pixel size multiplier.</param>
    /// <param name="frameDelayCentiseconds">Delay between frames in 1/100 s.</param>
    public static byte[] Encode(IReadOnlyList<byte[]> frames, int scale = 8, int frameDelayCentiseconds = 4)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(scale, 1);
        int width = DisplaySettings.Columns * scale, height = DisplaySettings.Rows * scale;

        using var output = new MemoryStream();
        using var writer = new BinaryWriter(output);

        writer.Write("GIF89a"u8);
        writer.Write((ushort)width);
        writer.Write((ushort)height);
        writer.Write((byte)0xF3); // global colour table, 4 bits per colour, 16 entries
        writer.Write((byte)0);    // background
        writer.Write((byte)0);    // aspect
        foreach (uint colour in Palette.Colors)
        {
            writer.Write((byte)(colour >> 16));
            writer.Write((byte)(colour >> 8));
            writer.Write((byte)colour);
        }

        // Loop forever.
        writer.Write(new byte[] { 0x21, 0xFF, 0x0B });
        writer.Write("NETSCAPE2.0"u8);
        writer.Write(new byte[] { 0x03, 0x01, 0x00, 0x00, 0x00 });

        var indices = new byte[width * height];
        foreach (byte[] frame in frames)
        {
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    indices[y * width + x] = frame[(y / scale) * DisplaySettings.Columns + x / scale];

            writer.Write(new byte[] { 0x21, 0xF9, 0x04, 0x00 }); // graphic control extension
            writer.Write((ushort)frameDelayCentiseconds);
            writer.Write(new byte[] { 0x00, 0x00 });

            writer.Write((byte)0x2C); // image descriptor
            writer.Write((ushort)0);
            writer.Write((ushort)0);
            writer.Write((ushort)width);
            writer.Write((ushort)height);
            writer.Write((byte)0);

            const int minCodeSize = 4;
            writer.Write((byte)minCodeSize);
            byte[] compressed = Lzw(indices, minCodeSize);
            for (int offset = 0; offset < compressed.Length; offset += 255)
            {
                int length = Math.Min(255, compressed.Length - offset);
                writer.Write((byte)length);
                writer.Write(compressed, offset, length);
            }
            writer.Write((byte)0); // block terminator
        }

        writer.Write((byte)0x3B);
        writer.Flush();
        return output.ToArray();
    }

    static byte[] Lzw(byte[] data, int minCodeSize)
    {
        int clear = 1 << minCodeSize, end = clear + 1;
        var output = new List<byte>();
        int bitBuffer = 0, bitCount = 0;

        void Emit(int code, int size)
        {
            bitBuffer |= code << bitCount;
            bitCount += size;
            while (bitCount >= 8)
            {
                output.Add((byte)bitBuffer);
                bitBuffer >>= 8;
                bitCount -= 8;
            }
        }

        var dictionary = new Dictionary<int, int>();
        int nextCode = end + 1, codeSize = minCodeSize + 1;
        Emit(clear, codeSize);

        int prefix = data[0];
        for (int i = 1; i < data.Length; i++)
        {
            int key = (prefix << 8) | data[i];
            if (dictionary.TryGetValue(key, out int existing))
            {
                prefix = existing;
                continue;
            }

            Emit(prefix, codeSize);
            if (nextCode < 4096)
            {
                dictionary[key] = nextCode++;
                if (nextCode - 1 == (1 << codeSize) && codeSize < 12) codeSize++;
            }
            else
            {
                Emit(clear, codeSize);
                dictionary.Clear();
                nextCode = end + 1;
                codeSize = minCodeSize + 1;
            }
            prefix = data[i];
        }

        Emit(prefix, codeSize);
        Emit(end, codeSize);
        if (bitCount > 0) output.Add((byte)bitBuffer);
        return output.ToArray();
    }
}
