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
      --png FILE     with --headless, also save the final screen as a PNG
      --gif FILE     record an animated GIF of the program (no display); see --seconds and --fps
      --seconds S    length of the GIF recording in emulated seconds (default 4)
      --fps N        GIF frame rate (default 25)
      --scale N      pixel size of the PNG / GIF (default 8)
      --mhz X        emulated clock speed in MHz (default 4)
      --no-throttle  run at full speed
      --letters      draw pixels as letters a-p instead of colour blocks
      --list         list the bundled example programs
      -h, --help     show this help

    Without a program, the bundled 'disco' example is run. A program name without an
    extension is looked up in the Examples folder next to the executable.
    A .bin file is loaded verbatim at $0600; anything else is assembled first.
    """;

string? program = null;
long? headless = null;
bool throttle = true;
bool letters = false;
string? pngPath = null;
string? gifPath = null;
double seconds = 4;
int fps = 25;
int scale = 8;
double mhz = 4;

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
        case "--png" when i + 1 < args.Length:
            pngPath = args[++i];
            break;
        case "--gif" when i + 1 < args.Length:
            gifPath = args[++i];
            break;
        case "--seconds" when i + 1 < args.Length && double.TryParse(args[i + 1], out double sec) && sec > 0:
            seconds = sec;
            i++;
            break;
        case "--fps" when i + 1 < args.Length && int.TryParse(args[i + 1], out int f) && f > 0:
            fps = f;
            i++;
            break;
        case "--scale" when i + 1 < args.Length && int.TryParse(args[i + 1], out int sc) && sc > 0:
            scale = sc;
            i++;
            break;
        case "--mhz" when i + 1 < args.Length && double.TryParse(args[i + 1], out double mh) && mh > 0:
            mhz = mh;
            i++;
            break;
        case "--letters":
            letters = true;
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
var processor = new Processor(memory, (ushort)assembled.Entry);

if (gifPath is not null)
{
    // Record: advance the emulated clock frame by frame, no real-time waiting.
    var frames = new List<byte[]>();
    double cyclesPerFrame = mhz * 1_000_000 / fps;
    int frameCount = (int)Math.Round(seconds * fps);
    for (int frame = 1; frame <= frameCount; frame++)
    {
        while (processor.ProgramRunning && processor.Cycles < frame * cyclesPerFrame)
            processor.Step();
        frames.Add(Palette.Snapshot(memory));
    }
    File.WriteAllBytes(gifPath, GifEncoder.Encode(frames, scale, Math.Max(1, 100 / fps)));
    Console.WriteLine($"Wrote {frames.Count} frames to {gifPath}");
    return 0;
}

if (headless is long count)
{
    processor.Run(count);
    Console.WriteLine($"A={processor.A:X2} X={processor.X:X2} Y={processor.Y:X2} SP={processor.SP:X2} PC={processor.PC:X4} P={processor.Status:X2}");
    Console.WriteLine($"{processor.Instructions} instructions, {processor.Cycles} cycles, {(processor.ProgramRunning ? "still running" : "halted")}");
    Console.Write(new TextDisplay(memory).Frame());
    if (pngPath is not null)
        File.WriteAllBytes(pngPath, Palette.ToPng(memory, scale));
    return 0;
}

TextDisplay.Setup();
var display = new TextDisplay(memory) { Colour = !letters && !Console.IsOutputRedirected };
var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

var executeThread = new Thread(() =>
{
    try
    {
        if (throttle)
            processor.RunRealtime(mhz * 1_000_000, cts.Token);
        else
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
