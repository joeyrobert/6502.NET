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

using System.IO.Compression;

namespace Emulator.Interpreter;

/// <summary>The 16 colour palette (Commodore 64 style, as used by 6502asm.com) and screen snapshot helpers.</summary>
public static class Palette
{
    /// <summary>24-bit RGB values, indexed by the low nibble of a screen byte.</summary>
    public static readonly uint[] Colors =
    [
        0x000000, 0xFFFFFF, 0x880000, 0xAAFFEE, 0xCC44CC, 0x00CC55, 0x0000AA, 0xEEEE77,
        0xDD8855, 0x664400, 0xFF7777, 0x333333, 0x777777, 0xAAFF66, 0x0088FF, 0xBBBBBB,
    ];

    public static (byte R, byte G, byte B) Rgb(int colour)
    {
        uint c = Colors[colour & 0x0F];
        return ((byte)(c >> 16), (byte)(c >> 8), (byte)c);
    }

    /// <summary>Reads the 32x32 screen (row-major) as colour indices 0-15.</summary>
    public static byte[] Snapshot(Memory memory)
    {
        var pixels = new byte[DisplaySettings.Size];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = (byte)(memory.Read(DisplaySettings.Offset + i) & 0x0F);
        return pixels;
    }

    /// <summary>Encodes the screen as a PNG, each pixel drawn <paramref name="scale"/> x <paramref name="scale"/> pixels large.</summary>
    public static byte[] ToPng(Memory memory, int scale = 8)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(scale, 1);
        byte[] pixels = Snapshot(memory);
        int width = DisplaySettings.Columns * scale, height = DisplaySettings.Rows * scale;

        // Raw scanlines: filter byte 0 followed by RGB triples.
        var raw = new byte[height * (1 + width * 3)];
        int p = 0;
        for (int y = 0; y < height; y++)
        {
            raw[p++] = 0;
            for (int x = 0; x < width; x++)
            {
                var (r, g, b) = Rgb(pixels[(y / scale) * DisplaySettings.Columns + x / scale]);
                raw[p++] = r; raw[p++] = g; raw[p++] = b;
            }
        }

        using var output = new MemoryStream();
        output.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        Span<byte> header = stackalloc byte[13];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(header, width);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(header[4..], height);
        header[8] = 8; // bit depth
        header[9] = 2; // truecolour
        WriteChunk(output, "IHDR", header);

        using var compressed = new MemoryStream();
        using (var z = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
            z.Write(raw);
        WriteChunk(output, "IDAT", compressed.ToArray());
        WriteChunk(output, "IEND", []);
        return output.ToArray();
    }

    static void WriteChunk(Stream stream, string type, ReadOnlySpan<byte> data)
    {
        Span<byte> word = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(word, data.Length);
        stream.Write(word);

        var body = new byte[4 + data.Length];
        System.Text.Encoding.ASCII.GetBytes(type, body);
        data.CopyTo(body.AsSpan(4));
        stream.Write(body);

        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(word, Crc32(body));
        stream.Write(word);
    }

    static uint Crc32(ReadOnlySpan<byte> data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (byte b in data)
        {
            crc ^= b;
            for (int k = 0; k < 8; k++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
        }
        return ~crc;
    }
}
