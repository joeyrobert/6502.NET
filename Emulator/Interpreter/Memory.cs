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

/// <summary>
/// Flat byte-addressable memory. By default it is the full 64 KiB of the 6502 address space.
/// Address <see cref="RandomByteAddress"/> reads back a random byte (compatible with 6502asm.com).
/// </summary>
public class Memory
{
    /// <summary>Reads from this address return a random byte when <see cref="RandomEnabled"/> is set.</summary>
    public const int RandomByteAddress = 0xFE;

    readonly byte[] _memory;
    readonly Random _random = new();

    /// <summary>Whether reads of <see cref="RandomByteAddress"/> return random data.</summary>
    public bool RandomEnabled { get; set; } = true;

    /// <summary>Number of addressable bytes.</summary>
    public int Size => _memory.Length;

    /// <summary>Instantiates zero-filled memory.</summary>
    /// <param name="size">Number of bytes, at most 0x10000.</param>
    public Memory(int size = 0x10000)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(size, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(size, 0x10000);
        _memory = new byte[size];
    }

    /// <summary>Sets a memory location to a value.</summary>
    public void Set(int location, byte data)
    {
        if ((uint)location >= (uint)_memory.Length)
            throw new Exceptions.MemoryOutOfBoundsException("Attempted to set memory at location 0x" + location.ToString("X"));

        _memory[location] = data;
    }

    /// <summary>Sets a series of memory locations starting at <paramref name="location"/>.</summary>
    public void Set(int location, IEnumerable<byte> data)
    {
        int current = location;
        foreach (byte datum in data)
            Set(current++, datum);
    }

    /// <summary>Clears a memory location.</summary>
    public void Clear(int location) => Set(location, 0);

    /// <summary>Reads a memory location.</summary>
    public byte Read(int location)
    {
        if (RandomEnabled && location == RandomByteAddress)
            return (byte)_random.Next(0x100);

        if ((uint)location >= (uint)_memory.Length)
            throw new Exceptions.MemoryOutOfBoundsException("Attempted to read memory at location 0x" + location.ToString("X"));

        return _memory[location];
    }

    /// <summary>Reads a little-endian 16 bit word.</summary>
    public int ReadWord(int location) => Read(location) | (Read(location + 1) << 8);

    /// <summary>Fills <paramref name="count"/> locations starting at <paramref name="start"/>.</summary>
    public void FillWith(int start, int count, byte data)
    {
        for (int i = 0; i < count; i++)
            Set(start + i, data);
    }
}
