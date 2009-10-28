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

using System;
using System.Collections.Generic;

namespace Emulator.Interpreter
{
    public class Memory
    {
        byte[] _memory;
        UInt16 _size;
        Random _random = new Random();

        /// <summary>
        /// Instantiates memory class and zero fills 
        /// </summary>
        /// <param name="size"></param>
        public Memory(UInt16 size)
        {
            _size = size;
            _memory = new byte[_size];
        }

        /// <summary>
        /// Sets a memory location to a value
        /// </summary>
        /// <param name="location">memory index</param>
        /// <param name="data">value to set it to</param>
        public void Set(int location, byte data)
        {
            if (location >= 0 && location < _size) // strictly less than _size
            {
                _memory[location] = data;
                return;
            }

            throw new Exceptions.MemoryOutOfBoundsException("Attempted to set memory at location 0x" + location.ToString("X"));
        }

        /// <summary>
        /// Sets a series of memory locations
        /// </summary>
        /// <param name="location">starts at this location</param>
        /// <param name="data">list of data to be written to sequentially</param>
        public void Set(int location, List<byte> data)
        {
            int currentLocation = location;
            foreach (byte datum in data)
            {
                if (currentLocation >= 0 && currentLocation < _size) // strictly less than _size
                {
                    _memory[currentLocation] = datum;
                    currentLocation++;
                }
                else
                    throw new Exceptions.MemoryOutOfBoundsException("Attempted to set memory at location 0x" + location.ToString("X"));
            }
        }

        /// <summary>
        /// Clears a memory location
        /// </summary>
        /// <param name="location">memory index</param>
        public void Clear(int location)
        {
            this.Set(location, 0);
        }

        /// <summary>
        /// Reads a memory location
        /// </summary>
        /// <param name="location">memory index</param>
        /// <returns>value at location</returns>
        public byte Read(int location)
        {
            if (location == 0xFE)                
                return Convert.ToByte(_random.Next(0x00, 0x100)); // random # generator

            if (location >= 0 && location < _size)
                return _memory[location];

            throw new Exceptions.MemoryOutOfBoundsException("Attempted to read memory at location 0x" + location.ToString("X"));
        }

        /// <summary>
        /// Fill memory locations with data
        /// </summary>
        /// <param name="start">starting memory index</param>
        /// <param name="limit">number of locations to fill</param>
        /// <param name="data">value to set it to</param>
        public void FillWith(int start, int limit, byte data)
        {
            for (int i = 0; i <= limit; i++)
            {
                int location = start + i;
                this.Set(location, data);
            }
        }  
    }
}
