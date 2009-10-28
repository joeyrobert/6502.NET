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
using System.Text;
using System.Collections.Generic;
using Emulator;
using Emulator.Interpreter;

namespace Emulator.Display.Text
{
    /// <summary>
    /// Text Display Class
    /// </summary>
    public class TextDisplay
    {
        Memory _memory;

        /// <summary>
        /// Display settings
        /// </summary>
        public const int Rows = 32;
        public const int Columns = 32;
        public const int Size = Rows * Columns;
        public const int Offset = 0x200;
        public const int RenderFrequency = 30; // hertz
        public const bool LimitGraphicsSpeed = true;
        public const int PixelHeight = 6;
        public const int PixelWidth = 6;

        public TextDisplay(Memory memory)
        {
            _memory = memory;
        }

        /// <summary>
        /// Set default window size to Column x Row
        /// </summary>
        public static void Setup()
        {
            Console.Title = "6502 Emulator";

            Console.SetWindowSize(Columns, Rows);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.CursorVisible = false;
            Console.BufferHeight = Interpreter.DisplaySettings.Rows;
            Console.BufferWidth = Interpreter.DisplaySettings.Columns;
        }

        /// <summary>
        /// Render the display screen
        /// </summary>
        /// <param name="memory">memory object</param>
        public void Render()
        {
            StringBuilder screenBuffer = new StringBuilder();
            int location;
            for (int i = 0; i < Rows; i++)
            {
                for (int j = 0; j < Columns; j++)
                {
                    location = Offset + i * Columns + j;
                    screenBuffer.Append(Convert.ToChar((_memory.Read(location) & 0x0F) + 97));
                }
            }
            Console.Write(screenBuffer.ToString());
            Console.SetCursorPosition(0, 0);
        }
    }
}
