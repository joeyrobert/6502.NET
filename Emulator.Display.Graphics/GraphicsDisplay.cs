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

using System.Drawing.Drawing2D;
using Emulator.Interpreter;
using Timer = System.Windows.Forms.Timer;

namespace Emulator.Display.Graphics;

/// <summary>WinForms front end: the 32x32 screen at 0x200, one of 16 colours per byte (low nibble).</summary>
public class GraphicsDisplay : Form
{
    /// <summary>Last key pressed is stored here (ASCII), as on 6502asm.com.</summary>
    const int KeyAddress = 0xFF;

    static readonly Color[] Palette =
    [
        Color.Black, Color.White, Color.DarkRed, Color.Cyan, Color.Purple, Color.Green, Color.Blue, Color.Yellow,
        Color.Orange, Color.Brown, Color.Red, Color.DarkGray, Color.Gray, Color.LightGreen, Color.LightSkyBlue, Color.LightGray,
    ];

    readonly Memory _memory = new();
    readonly Processor _processor;
    readonly Bitmap _screen = new(DisplaySettings.Columns, DisplaySettings.Rows);
    readonly Timer _timer = new() { Interval = 1000 / DisplaySettings.RenderFrequency };
    readonly Thread _executeThread;

    public GraphicsDisplay(AssembledProgram program, string title)
    {
        Text = "6502.NET - " + title;
        DoubleBuffered = true;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        ClientSize = new Size(DisplaySettings.Columns * DisplaySettings.PixelWidth, DisplaySettings.Rows * DisplaySettings.PixelHeight);

        program.Load(_memory);
        _processor = new Processor(_memory, (ushort)program.Entry) { LimitProcessorSpeed = true };
        _executeThread = new Thread(Execute) { IsBackground = true };

        KeyPress += (_, e) => _memory.Set(KeyAddress, (byte)e.KeyChar);
        FormClosed += (_, _) => _processor.ProgramRunning = false;
        _timer.Tick += (_, _) => Invalidate();
        Shown += (_, _) =>
        {
            _timer.Start();
            _executeThread.Start();
        };
    }

    void Execute()
    {
        try
        {
            while (_processor.ProgramRunning)
                _processor.Step();
        }
        catch (Exceptions.InvalidOpCodeException e)
        {
            BeginInvoke(() => MessageBox.Show(this, e.Message, "6502.NET", MessageBoxButtons.OK, MessageBoxIcon.Error));
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        for (int row = 0; row < DisplaySettings.Rows; row++)
            for (int col = 0; col < DisplaySettings.Columns; col++)
                _screen.SetPixel(col, row, Palette[_memory.Read(DisplaySettings.Offset + row * DisplaySettings.Columns + col) & 0x0F]);

        e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
        e.Graphics.DrawImage(_screen, ClientRectangle);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
            _screen.Dispose();
        }
        base.Dispose(disposing);
    }
}
