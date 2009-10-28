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
using System.Threading;
using System.Collections.Generic;

namespace Emulator.Interpreter
{
    public class Processor
    {
        Memory _memory;

        /// <summary>
        /// Processor settings
        /// </summary>
        public static int SpinWait = 5000;
        public static bool LimitProcessorSpeed = true;

        /// <summary>
        /// Registers: 8 bit => byte, 16 bit => UInt16.
        /// </summary>
        byte _accumulator = 0;
        byte _indexX = 0;
        byte _indexY = 0;
        byte _stackPointer = 0xFF;
        UInt16 _programCounter = 0x00; // points to the next location to be executed
        
        /// <summary>
        /// CPU Flags
        /// </summary>
        bool _carryFlag = false;
        bool _zeroFlag = false;
        bool _interruptDisableFlag = false;
        bool _decimalModeFlag = false;
        bool _breakFlag = false;
        bool _overflowFlag = false;
        bool _negativeFlag = false;

        /// <summary>
        /// Persistent variables used in the switch()
        /// (limits garbage collector)
        /// </summary>
        byte value1;
        byte value2;
        int sum;

        /// <summary>
        /// Flow control variables. Halts Execute() if false
        /// </summary>
        bool _programRunning = true;

        public bool ProgramRunning
        {
            get {
                return _programRunning;
            }
            set
            {
                _programRunning = value;
            }

        }

        /// <summary>
        /// Loads memory that the processor has access to.
        /// </summary>
        /// <param name="memory">computer's memory component</param>
        public Processor(Memory memory)
        {
            _memory = memory;
        }

        /// <summary>
        /// Loads memory and initial position
        /// </summary>
        /// <param name="memory">computer's memory component</param>
        /// <param name="programCounter">first location to be executed</param>
        public Processor(Memory memory, UInt16 programCounter)
        {
            _memory = memory;
            _programCounter = programCounter;
        }

        /// <summary>
        /// Peeks at the next byte to be executed (using the program counter).
        /// </summary>
        /// <returns>next memory location value</returns>
        private byte PeekByte()
        {
            return _memory.Read(_programCounter);
        }

        /// <summary>
        /// Pops the next byte to be executed (using the program counter) and increments the program counter.
        /// </summary>
        /// <returns>next memory location value</returns>
        private byte PopByte()
        {
            _programCounter++;
            return _memory.Read(_programCounter - 1);
        }

        /// <summary>
        /// Pushes on the 6502's stack
        /// </summary>
        private void PushOnStack()
        {

        }

        /// <summary>
        /// Pulls from the 6502's stack
        /// </summary>
        private void PullFromStack()
        {
        }

        /// <summary>
        /// Adds offset to program counter.
        /// </summary>
        /// <param name="offset">Can be in the range of -128 to 127</param>
        private void JumpRelative(int offset)
        {
            if (offset > 0x7F)
                _programCounter -= Convert.ToUInt16(0x100 - offset);
            else
                _programCounter += Convert.ToUInt16(offset);
        }

        /// <summary>
        /// Core of the intepreter, executes a single instruction.
        /// </summary>
        public void Execute()
        {
            //Thread.Sleep(1);
            if (!_programRunning) return;
            int a = 0;
            if (LimitProcessorSpeed)
            {
                for (int i = 0; i < SpinWait; i++)                
                    a += 2;
                
            }

            byte opcode = PopByte();
            // Console.WriteLine("0x" + opcode.ToString("X"));

            switch (opcode)
            {
                case 0x00: // BRK
                    _breakFlag = true;
                    _programRunning = false;
                    break;
                case 0x01: // Logical OR, Indirect, X
                    value1 = PopByte();
                    _accumulator |= _memory.Read(value1 + _indexX);

                    _zeroFlag = _accumulator == 0;
                    _negativeFlag = (_accumulator & 0x80) != 0;
                    break;
                case 0x05: // Logical OR, Zero-paged
                    value1 = PopByte();
                    _accumulator |= _memory.Read(value1);

                    _zeroFlag = _accumulator == 0;
                    _negativeFlag = (_accumulator & 0x80) != 0;
                    break;
                case 0x06: // Arithmetic shift left, Zero-paged
                    _carryFlag = (_accumulator & 0x80) != 0;
                    value1 = PopByte();
                    _memory.Set(value1, Convert.ToByte(_memory.Read(value1) << 1));

                    _zeroFlag = _accumulator == 0;
                    _negativeFlag = (_accumulator & 0x80) != 0;
                    break;
                case 0x08:

                    break;
                case 0x09: // Logical OR, Immediate
                    value1 = PopByte();
                    _accumulator |= value1;

                    _zeroFlag = _accumulator == 0;
                    _negativeFlag = (_accumulator & 0x80) != 0;
                    break;
                case 0x0A: // Arithmetic shift left, Accumulator
                    // Note: actually logical shift left
                    _carryFlag = (_accumulator & 0x80) != 0;

                    _accumulator = Convert.ToByte((_accumulator * 2) & 0xFF);

                    _zeroFlag = _accumulator == 0;
                    _negativeFlag = (_accumulator & 0x80) != 0;
                    break;
                case 0x0D: // Logical OR, Absolute
                    value1 = PopByte();
                    value2 = PopByte();

                    _accumulator |= _memory.Read(value2 * 0x100 + value1);

                    _zeroFlag = _accumulator == 0;
                    _negativeFlag = (_accumulator & 0x80) != 0;
                    break;
                case 0x0E: // Arithmetic shift left, Absolute
                    _carryFlag = (_accumulator & 0x80) != 0;

                    value1 = PopByte();
                    value2 = PopByte();
                    _memory.Set(value2 * 0x100 + value1, Convert.ToByte(2 * _memory.Read(value1 * 0x100 + value2)));

                    _zeroFlag = _accumulator == 0;
                    break;
                case 0x10:
                    break;
                case 0x11:
                    break;
                case 0x15:
                    break;
                case 0x16:
                    break;
                case 0x18:
                    break;
                case 0x19:
                    break;
                case 0x1D:
                    break;
                case 0x1E:
                    break;

                    
                case 0x20:
                    break;
                case 0x21:
                    break;
                case 0x24:
                    break;
                case 0x25:
                    break;
                case 0x26:
                    break;
                case 0x28:
                    break;
                case 0x29: // Logical AND, Immediate
                    value1 = PopByte();
                    _accumulator &= value1;

                    _zeroFlag = _accumulator == 0;
                    _negativeFlag = (_accumulator & 0x80) != 0;
                    break;
                case 0x2A:
                    break;
                case 0x2C:
                    break;
                case 0x2D:
                    break;
                case 0x2E:
                    break;
                    

                case 0x30:
                    break;
                case 0x31:
                    break;
                case 0x35:
                    break;
                case 0x36:
                    break;
                case 0x38:
                    break;
                case 0x39:
                    break;
                case 0x3D:
                    break;
                case 0x3E:
                    break;


                case 0x40:
                    break;
                case 0x41:
                    break;
                case 0x45:
                    break;
                case 0x46:
                    break;
                case 0x48:
                    break;
                case 0x49:
                    break;
                case 0x4A: // Logical shift right
                    break;
                case 0x4C: // Jump, Absolute
                    value1 = PopByte();
                    value2 = PopByte();

                    _programCounter = Convert.ToUInt16(value2 * 0x100 + value1);
                    break;
                case 0x4D:
                    break;
                case 0x4E:
                    break;


                case 0x50:
                    break;
                case 0x51:
                    break;
                case 0x55:
                    break;
                case 0x56:
                    break;
                case 0x58:
                    break;
                case 0x59:
                    break;
                case 0x5D:
                    break;
                case 0x5E:
                    break;


                case 0x60:
                    break;
                case 0x61: // Add with Carry, Indirect, X
                    value1 = PopByte();
                    sum = _accumulator + _memory.Read(value1 + _indexX);
                    if (_carryFlag)
                        sum++;

                    _carryFlag = sum > 0xFF;
                    _accumulator = Convert.ToByte(sum % 0xFF);
                    break;
                case 0x65: // Add with Carry, Zero-paged
                    value1 = PopByte();

                    sum = _accumulator + _memory.Read(value1);
                    if (_carryFlag)
                        sum++;

                    _carryFlag = sum > 0xFF;
                    _accumulator = Convert.ToByte(sum % 0xFF);
                    break;
                case 0x66:
                    break;
                case 0x68:
                    break;
                case 0x69: // Add with Carry, Immediate
                    value1 = PopByte();
                    sum = _accumulator + value1;
                    if (_carryFlag)
                        sum++;

                    _carryFlag = sum > 0xFF;
                    _accumulator = Convert.ToByte(sum % 0xFF);
                    break;
                case 0x6A:
                    break;
                case 0x6C: // Jump, Indirect
                    value1 = PopByte();
                    value2 = PopByte();

                    _programCounter = Convert.ToUInt16(_memory.Read(value2 * 0x100 + value1 + 1) * 0x100 + _memory.Read(value2 * 0x100 + value1));
                    break;
                case 0x6D: // Add with Carry, Absolute
                    value1 = PopByte();
                    value2 = PopByte();
                    sum = _accumulator + _memory.Read(value2 * 0x100 + value1);
                    if (_carryFlag)
                        sum++;

                    _carryFlag = sum > 0xFF;
                    _accumulator = Convert.ToByte(sum % 0xFF);
                    break;
                case 0x6E:
                    break;


                case 0x70:
                    break;
                case 0x71:
                    break;
                case 0x75: // Add with Carry, Zero-paged, X
                    value1 = PopByte();
                    sum = _accumulator + _memory.Read((value1 + _indexX) % 0xFF);
                    if (_carryFlag)
                        sum++;

                    _carryFlag = sum > 0xFF;
                    _accumulator = Convert.ToByte(sum % 0xFF);
                    _programCounter++;
                    break;
                case 0x76:
                    break;
                case 0x78:
                    break;
                case 0x79: // Add with Carry, Absolute, Y
                    value1 = PopByte();
                    value2 = PopByte();
                    sum = _accumulator + _memory.Read(value2 * 0x100 + value1 + _indexY);
                    if (_carryFlag)
                        sum++;

                    _carryFlag = sum > 0xFF;
                    _accumulator = Convert.ToByte(sum % 0xFF);
                    break;
                case 0x7D: // Add with Carry, Absolute, X
                    value1 = PopByte();
                    value2 = PopByte();
                    sum = _accumulator + _memory.Read(value2 * 0x100 + value1 + _indexX);
                    if (_carryFlag)
                        sum++;

                    _carryFlag = sum > 0xFF;
                    _accumulator = Convert.ToByte(sum % 0xFF);
                    break;
                case 0x7E:
                    break;


                case 0x81: // Store accumulator, Indirect, X
                    value1 = PopByte();
                    _memory.Set((value1 + _indexX) % 0xFFFF, _accumulator);

                    break;
                case 0x84:
                    break;
                case 0x85: // Store accumulator, Zero-paged
                    value1 = PopByte();
                    _memory.Set(value1, _accumulator);
                    break;
                case 0x86: // Store X, Zero-paged
                    value1 = PopByte();
                    _memory.Set(value1, _indexX);
                    break;
                case 0x88:
                    break;
                case 0x8A: // Transfer X to A, Implied
                    _accumulator = _indexX;

                    _zeroFlag = _accumulator == 0;
                    _negativeFlag = (_accumulator & 0x80) != 0;
                    break;
                case 0x8C:
                    break;
                case 0x8D: // Store accumulator, Absolute
                    value1 = PopByte();
                    value2 = PopByte();
                    _memory.Set(value2 * 0x100 + value1, _accumulator);
                    break;
                case 0x8E:
                    break;


                case 0x90:
                    break;
                case 0x91:  // Store accumulator, Indirect, Y
                    value1 = PopByte();
                    value2 = _memory.Read(value1);
                    value1 = _memory.Read(value1 + 1);

                    _memory.Set((value1 * 0x100 + value2 + _indexY) & 0xFFFF, _accumulator);
                    break;
                case 0x94:
                    break;
                case 0x95: // Store accumulator, Zero-paged, X
                    value1 = PopByte();
                    _memory.Set((value1 + _indexX) % 0xFF, _accumulator);
                    break;
                case 0x96:
                    break;
                case 0x98: // Transfer Y to A, Implied
                    _accumulator = _indexY;

                    _zeroFlag = _accumulator == 0;
                    _negativeFlag = (_accumulator & 0x80) != 0;
                    break;
                case 0x99: // Store accumulator, Absolute, Y
                    value1 = PopByte();
                    value2 = PopByte();
                    _memory.Set(value2 * 0x100 + value1 + _indexY, _accumulator);
                    break;
                case 0x9A:
                    break;
                case 0x9D:
                    break;


                case 0xA0:
                    break;
                case 0xA1: // Load accumulator, Indirect, X
                    value1 = PopByte();
                    _accumulator = _memory.Read((value1 + _indexX) % 0xFFFF); // wraps around

                    _zeroFlag = _accumulator == 0;
                    _negativeFlag = (_accumulator & 0x80) != 0;
                    break;
                case 0xA2: // Load X, Immediate
                    _indexX = PopByte();

                    _zeroFlag = _indexX == 0;
                    _negativeFlag = (_indexX & 0x80) != 0;
                    break;
                case 0xA4:
                    break;
                case 0xA5: // Load accumulator, Zero-paged
                    value1 = PopByte();
                    _accumulator = _memory.Read(value1);

                    _zeroFlag = _accumulator == 0;
                    _negativeFlag = (_accumulator & 0x80) != 0;
                    break;
                case 0xA6: // Load X, Zero-paged
                    value1 = PopByte();
                    _indexX = _memory.Read(value1);

                    _zeroFlag = _indexX == 0;
                    _negativeFlag = (_indexX & 0x80) != 0;
                    break;
                case 0xA8: // Transfer A to Y, Implied
                    _indexY = _accumulator;

                    _zeroFlag = _indexY == 0;
                    _negativeFlag = (_indexY & 0x80) != 0;
                    break;
                case 0xA9: // Load accumulator, Immediate
                    value1 = PopByte();
                    _accumulator = value1;

                    _zeroFlag = _accumulator == 0;
                    _negativeFlag = (_accumulator & 0x80) != 0;
                    break;
                case 0xAA: // Transfer A to X, Implied
                    _indexX = _accumulator;

                    _zeroFlag = _indexX == 0;
                    _negativeFlag = (_indexX & 0x80) != 0;
                    break;
                case 0xAC:
                    break;
                case 0xAD: // Load accumulator, Absolute
                    value1 = PopByte();
                    value2 = PopByte();
                    _accumulator = _memory.Read(value2 * 0x100 + value1);
                    break;
                case 0xAE:
                    break;


                case 0xB0:
                    break;
                case 0xB1: // Load accumulator, Indirect, Y
                    value1 = PopByte();
                    value2 = _memory.Read(value1);
                    value1 = _memory.Read(value1 + 1);
                    _accumulator = _memory.Read((value1 * 0x100 + value2 + _indexY) & 0xFFFF); // wraps around

                    _zeroFlag = _accumulator == 0;
                    _negativeFlag = (_accumulator & 0x80) != 0;
                    break;
                case 0xB4:
                    break;
                case 0xB5: // Load accumulator, Zero-paged, X
                    value1 = PopByte();
                    _accumulator = _memory.Read((value1 + _indexX) & 0xFF); // wraps around
                    break;
                case 0xB6:
                    break;
                case 0xB8:
                    break;
                case 0xB9: // Load accumulator, Absolute, Y
                    value1 = PopByte();
                    value2 = PopByte();
                    _accumulator = _memory.Read(value2 * 0x100 + value1 + _indexY);

                    _zeroFlag = _accumulator == 0;
                    _negativeFlag = (_accumulator & 0x80) != 0;
                    break;
                case 0xBA:
                    break;
                case 0xBC:
                    break;
                case 0xBD: // Load accumulator, Absolute, X
                    value1 = PopByte();
                    value2 = PopByte();
                    _accumulator = _memory.Read(value2 * 0x100 + value1 + _indexX);

                    _zeroFlag = _accumulator == 0;
                    _negativeFlag = (_accumulator & 0x80) != 0;
                    break;
                case 0xBE:
                    break;


                case 0xC0:
                    break;
                case 0xC1:
                    break;
                case 0xC4:
                    break;
                case 0xC5: // Compare, Zero-paged
                    value1 = PopByte();
                    _carryFlag = _accumulator >= _memory.Read(value1);
                    _accumulator -= _memory.Read(value1);

                    _zeroFlag = _accumulator == 0;
                    _negativeFlag = (_accumulator & 0x80) != 0;
                    break;
                case 0xC6: // Decrement memory, Zero Paged
                    value1 = PopByte();
                    if (_memory.Read(value1) - 1 == -1)
                        value2 = Convert.ToByte(0xFF);
                    else
                        value2 = Convert.ToByte(_memory.Read(value1) - 1);
                    
                    _memory.Set(value1, value2);

                    _negativeFlag = (value2 & 0x80) != 0;
                    _zeroFlag = value2 == 0;
                    break;
                case 0xC8: // Increment Y, Implied
                    _indexY = Convert.ToByte((_indexY + 1) & 0xFF);

                    _zeroFlag = _indexY == 0;
                    _negativeFlag = (_indexY & 0x80) != 0;
                    break;
                case 0xC9: // Compare, Immediate
                    value1 = PopByte();
                    _carryFlag = _accumulator >= value1;
                    _accumulator -= value1;

                    _zeroFlag = _accumulator == 0;
                    _negativeFlag = (_accumulator & 0x80) != 0;
                    break;
                case 0xCA:
                    break;
                case 0xCC:
                    break;
                case 0xCD:
                    break;
                case 0xCE:
                    break;


                case 0xD0: // Branch if not equal
                    value1 = PopByte();
                    if (!_zeroFlag)
                        JumpRelative(value1);
                    break;
                case 0xD1:
                    break;
                case 0xD5:
                    break;
                case 0xD6:
                    break;
                case 0xD8:
                    break;
                case 0xD9:
                    break;
                case 0xDD:
                    break;
                case 0xDE:
                    break;


                case 0xE0:
                    break;
                case 0xE1:
                    break;
                case 0xE4:
                    break;
                case 0xE5:
                    break;
                case 0xE6: // Increment memory, Zero Paged
                    value1 = PopByte();
                    value2 = Convert.ToByte((_memory.Read(value1) + 1) & 0xFF);
                    _memory.Set(value1, value2);

                    _negativeFlag = (value2 & 0x80) != 0;
                    _zeroFlag = value2 == 0;
                    break;
                case 0xE8: // Increment X, Implied
                    _indexX = Convert.ToByte((_indexX + 1) & 0xFF);

                    _zeroFlag = _indexX == 0;
                    _negativeFlag = (_indexX & 0x80) != 0;
                    break;
                case 0xE9:
                    break;
                case 0xEA: // No-op
                    return;
                case 0xEB:
                    break;
                case 0xEC:
                    break;
                case 0xED:
                    break;
                case 0xEE: // Increment memory, Absolute
                    value1 = PopByte();
                    value2 = PopByte();

                    sum = _memory.Read(value1 * 0x100 + value2) + 1;
                    _memory.Set(value2 * 0x100 + value1, Convert.ToByte(sum));
                    _negativeFlag = (sum & 0x80) != 0;
                    _zeroFlag = sum == 0;
                    break;

                case 0xF0: // Branch if equal
                    value1 = PopByte();
                    if (_zeroFlag)
                        JumpRelative(value1);
                    break;
                case 0xF1:
                    break;
                case 0xF5:
                    break;
                case 0xF6: // Increment memory, Zero Paged, X
                    value1 = PopByte();
                    value2 = Convert.ToByte(_memory.Read((value1 + _indexX) & 0xFF) + 1);
                    _memory.Set((value1 + _indexX) & 0xFF, value2);
                    _negativeFlag = (value2 & 0x80) != 0;
                    _zeroFlag = value2 == 0;
                    break;
                case 0xF8:
                    break;
                case 0xF9:
                    break;
                case 0xFD:
                    break;
                case 0xFE: // Increment memory, Absolute, X
                    value1 = PopByte();
                    value2 = PopByte();
                    sum = _memory.Read(value2 * 0x100 + value1) + 1;
                    _memory.Set(value2 * 0x100 + value1, Convert.ToByte(sum));
                    _negativeFlag = (sum & 0x80) != 0;
                    _zeroFlag = sum == 0;
                    break;
                default:
                    throw new Exceptions.InvalidOpCodeException("Opcode 0x" + opcode.ToString("X") + " is invalid.");
            }
        }
    }
}
