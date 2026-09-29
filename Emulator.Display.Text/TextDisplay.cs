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

using System.Text;
using Emulator.Interpreter;

namespace Emulator.Display.Text;

/// <summary>Renders the 32x32 screen (memory at 0x200) as letters: colour 0 is 'a', 1 is 'b', ... 15 is 'p'.</summary>
public class TextDisplay(Memory memory)
{
    /// <summary>Best-effort console setup; ignored where the console cannot be resized (redirected output, some terminals).</summary>
    public static void Setup()
    {
        try
        {
            Console.Title = "6502 Emulator";
            Console.ForegroundColor = ConsoleColor.Green;
            Console.CursorVisible = false;
            Console.Clear();
        }
        catch (IOException) { }
        catch (PlatformNotSupportedException) { }
    }

    /// <summary>Returns the screen as text, one line per row.</summary>
    public string Frame()
    {
        var sb = new StringBuilder(DisplaySettings.Size + DisplaySettings.Rows);
        for (int row = 0; row < DisplaySettings.Rows; row++)
        {
            for (int col = 0; col < DisplaySettings.Columns; col++)
            {
                int location = DisplaySettings.Offset + row * DisplaySettings.Columns + col;
                sb.Append((char)('a' + (memory.Read(location) & 0x0F)));
            }
            sb.Append('\n');
        }
        return sb.ToString();
    }

    /// <summary>Draws the screen at the top-left of the console.</summary>
    public void Render()
    {
        Console.SetCursorPosition(0, 0);
        Console.Write(Frame());
    }
}
