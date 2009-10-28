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

namespace Emulator.Interpreter
{
    public class Exceptions
    {
        public class MemoryOutOfBoundsException : Exception
        {
            /// <summary>
            /// Thrown when memory outside of the allocated memory is accessed
            /// </summary>
            /// <param name="message"></param>
            public MemoryOutOfBoundsException(string message) : base(message) { }
        }

        public class InvalidOpCodeException : Exception
        {
            /// <summary>
            /// Thrown when the processor encounters an opcode not in the opcode list
            /// </summary>
            /// <param name="message"></param>
            public InvalidOpCodeException(string message) : base(message) { }
        }
    }
}
