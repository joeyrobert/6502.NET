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
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using Emulator;
using Emulator.Interpreter;

namespace Emulator.Display.Graphics
{
    public partial class GraphicsDisplay : Form
    {
        Memory _memory;
        Processor _processor;

        public GraphicsDisplay()
        {
            InitializeComponent();
            this.Paint += new PaintEventHandler(GraphicsDisplay_Paint);
            this.FormClosed += new FormClosedEventHandler(GraphicsDisplay_FormClosing);
            _memory = new Memory(0xFFFF);
            _processor = new Processor(_memory, 0x600);

            // alive.asm
            //_memory.Set(0x600, new List<byte> { 0xa9, 0xf, 0x85, 0x0, 0x85, 0x1, 0xa5, 0xfe, 0x29, 0x3, 0xc9, 0x0, 0xf0, 0x2f, 0xc9, 0x1, 0xf0, 0x30, 0xc9, 0x2, 0xf0, 0x22, 0xc6, 0x1, 0xa5, 0x1, 0x29, 0x1f, 0xa, 0xaa, 0xbd, 0x47, 0x6, 0x85, 0x2, 0xe8, 0xbd, 0x47, 0x6, 0x85, 0x3, 0xa5, 0x0, 0x29, 0x1f, 0xa8, 0xb1, 0x2, 0xaa, 0xe8, 0x8a, 0x91, 0x2, 0x4c, 0x6, 0x6, 0xe6, 0x1, 0x4c, 0x18, 0x6, 0xc6, 0x0, 0x4c, 0x18, 0x6, 0xe6, 0x0, 0x4c, 0x18, 0x6, 0x0, 0x2, 0x20, 0x2, 0x40, 0x2, 0x60, 0x2, 0x80, 0x2, 0xa0, 0x2, 0xc0, 0x2, 0xe0, 0x2, 0x0, 0x3, 0x20, 0x3, 0x40, 0x3, 0x60, 0x3, 0x80, 0x3, 0xa0, 0x3, 0xc0, 0x3, 0xe0, 0x3, 0x0, 0x4, 0x20, 0x4, 0x40, 0x4, 0x60, 0x4, 0x80, 0x4, 0xa0, 0x4, 0xc0, 0x4, 0xe0, 0x4, 0x0, 0x5, 0x20, 0x5, 0x40, 0x5, 0x60, 0x5, 0x80, 0x5, 0xa0, 0x5, 0xc0, 0x5, 0xe0, 0x5 });
            
            // disco.asm
            _memory.Set(0x600, new List<byte> { 0xe8, 0x8a, 0x99, 0x0, 0x2, 0x99, 0x0, 0x3, 0x99, 0x0, 0x4, 0x99, 0x0, 0x5, 0xc8, 0x98, 0xc5, 0x10, 0xd0, 0x4, 0xc8, 0x4c, 0x0, 0x6, 0xc8, 0xc8, 0xc8, 0xc8, 0x4c, 0x0, 0x6 });
        }

        static void ExecuteThread(object processor)
        {
            Processor _processor = (Processor)processor;
            while(_processor.ProgramRunning)
                _processor.Execute();
        }

        Thread executeThread;

        private void GraphicsDisplay_Paint(object sender, PaintEventArgs e)
        {
            executeThread = new Thread(new ParameterizedThreadStart(ExecuteThread));
            executeThread.IsBackground = true;
            executeThread.Start(_processor);

            int location;
            Brush brush;
            try
            {
                for (;;)
                {
                    for (int i = 0; i < Interpreter.DisplaySettings.Rows; i++)
                    {
                        for (int j = 0; j < Interpreter.DisplaySettings.Columns; j++)
                        {
                            location = Interpreter.DisplaySettings.Offset + Interpreter.DisplaySettings.Columns * j + i;

                            switch (_memory.Read(location) & 0xF)
                            {
                                case 0x0:
                                    brush = Brushes.Black;
                                    break;
                                case 0x1:
                                    brush = Brushes.White;
                                    break;
                                case 0x2:
                                    brush = Brushes.DarkRed;
                                    break;
                                case 0x3:
                                    brush = Brushes.Cyan;
                                    break;
                                case 0x4:
                                    brush = Brushes.Purple;
                                    break;
                                case 0x5:
                                    brush = Brushes.Green;
                                    break;
                                case 0x6:
                                    brush = Brushes.Blue;
                                    break;
                                case 0x7:
                                    brush = Brushes.Yellow;
                                    break;
                                case 0x8:
                                    brush = Brushes.Orange;
                                    break;
                                case 0x9:
                                    brush = Brushes.Brown;
                                    break;
                                case 0xA:
                                    brush = Brushes.Red;
                                    break;
                                case 0xB:
                                    brush = Brushes.DarkGray;
                                    break;
                                case 0xC:
                                    brush = Brushes.Gray;
                                    break;
                                case 0xD:
                                    brush = Brushes.LightGreen;
                                    break;
                                case 0xE:
                                    brush = Brushes.LightSkyBlue;
                                    break;
                                case 0xF:
                                    brush = Brushes.LightGray;
                                    break;
                                default:
                                    brush = Brushes.Black;
                                    break;
                            }
                            e.Graphics.FillRectangle(brush, i * Interpreter.DisplaySettings.PixelWidth, j * Interpreter.DisplaySettings.PixelHeight, Interpreter.DisplaySettings.PixelWidth, Interpreter.DisplaySettings.PixelHeight);
                        }
                    }
                    if (Interpreter.DisplaySettings.LimitGraphicsSpeed)
                        Thread.Sleep(1000 / Interpreter.DisplaySettings.RenderFrequency);
                    Application.DoEvents();
                }
            }
            catch
            {
                _processor.ProgramRunning = false;
            }
        }

        private void GraphicsDisplay_FormClosing(Object sender, FormClosedEventArgs e)
        {
            _processor.ProgramRunning = false;
        }
        
    }
}

