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

using Emulator.Display.Text;
using Emulator.Interpreter;

const string Usage = """
    6502net - a 6502 emulator with a text display

    Usage: 6502net [program.asm | program.bin] [options]

      --headless N   run N instructions without a display, then print registers and the screen
      --no-throttle  run at full speed
      --list         list the bundled example programs
      -h, --help     show this help

    Without a program, the bundled 'disco' example is run. A program name without an
    extension is looked up in the Examples folder next to the executable.
    A .bin file is loaded verbatim at $0600; anything else is assembled first.
    """;

string? program = null;
long? headless = null;
bool throttle = true;

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "-h" or "--help":
            Console.WriteLine(Usage);
            return 0;
        case "--list":
            foreach (string example in Directory.GetFiles(ExamplesDirectory(), "*.asm").Order())
                Console.WriteLine(Path.GetFileNameWithoutExtension(example));
            return 0;
        case "--headless" when i + 1 < args.Length && long.TryParse(args[i + 1], out long n):
            headless = n;
            i++;
            break;
        case "--no-throttle":
            throttle = false;
            break;
        case var a when a.StartsWith("--"):
            Console.Error.WriteLine("Unknown option " + a + "\n\n" + Usage);
            return 2;
        default:
            program = args[i];
            break;
    }
}

AssembledProgram assembled;
try
{
    assembled = Load(program ?? "disco");
}
catch (Exception e) when (e is IOException or AssemblyException)
{
    Console.Error.WriteLine(e.Message);
    return 1;
}

var memory = new Memory();
assembled.Load(memory);
var processor = new Processor(memory, (ushort)assembled.Entry) { LimitProcessorSpeed = throttle && headless is null };

if (headless is long count)
{
    processor.Run(count);
    Console.WriteLine($"A={processor.A:X2} X={processor.X:X2} Y={processor.Y:X2} SP={processor.SP:X2} PC={processor.PC:X4} P={processor.Status:X2}");
    Console.WriteLine($"{processor.Instructions} instructions, {processor.Cycles} cycles, {(processor.ProgramRunning ? "still running" : "halted")}");
    Console.Write(new TextDisplay(memory).Frame());
    return 0;
}

TextDisplay.Setup();
var display = new TextDisplay(memory);
var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

var executeThread = new Thread(() =>
{
    try
    {
        while (!cts.IsCancellationRequested && processor.ProgramRunning)
            processor.Step();
    }
    catch (Exception e) when (e is Exceptions.InvalidOpCodeException or Exceptions.MemoryOutOfBoundsException)
    {
        Console.Error.WriteLine(e.Message);
    }
})
{ IsBackground = true };
executeThread.Start();

while (!cts.IsCancellationRequested)
{
    display.Render();
    if (DisplaySettings.LimitGraphicsSpeed)
        Thread.Sleep(1000 / DisplaySettings.RenderFrequency);
}

Console.CursorVisible = true;
Console.ResetColor();
return 0;

static string ExamplesDirectory() => Path.Combine(AppContext.BaseDirectory, "Examples");

static AssembledProgram Load(string name)
{
    if (!File.Exists(name) && Path.GetExtension(name) == "")
        name = Path.Combine(ExamplesDirectory(), name + ".asm");

    if (name.EndsWith(".bin", StringComparison.OrdinalIgnoreCase))
    {
        var binary = new AssembledProgram();
        binary.Segments.Add((Assembler.DefaultOrigin, File.ReadAllBytes(name)));
        binary.Entry = Assembler.DefaultOrigin;
        return binary;
    }

    return Assembler.AssembleFile(name);
}
