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

namespace Emulator.Interpreter
{
    /// <summary>
    /// Display settings
    /// </summary>
    public static class DisplaySettings
    {
        public const int Rows = 32;
        public const int Columns = 32;
        public const int Size = Rows * Columns;
        public const int Offset = 0x200;
        public static int RenderFrequency = 50; // hertz
        public static bool LimitGraphicsSpeed = true;
        public const int PixelHeight = 6;
        public const int PixelWidth = 6;
    }
}
