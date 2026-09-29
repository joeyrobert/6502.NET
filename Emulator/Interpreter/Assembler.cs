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

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Emulator.Interpreter;

/// <summary>Thrown for syntax errors in assembly source.</summary>
public class AssemblyException(string message, int line) : Exception($"Line {line}: {message}")
{
    public int Line { get; } = line;
}

/// <summary>Assembled output: one or more segments of bytes plus the symbol table.</summary>
public class AssembledProgram
{
    /// <summary>Contiguous chunks of output, each with its load address.</summary>
    public List<(int Address, byte[] Bytes)> Segments { get; } = new();

    /// <summary>Labels and constants.</summary>
    public Dictionary<string, int> Symbols { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Address of the first byte emitted.</summary>
    public int Origin => Segments.Count > 0 ? Segments[0].Address : 0;

    /// <summary>Address of the first instruction (not data) emitted; where execution should start.</summary>
    public int Entry { get; set; } = -1;

    /// <summary>Total number of bytes emitted.</summary>
    public int Length => Segments.Sum(s => s.Bytes.Length);

    /// <summary>Writes all segments into memory.</summary>
    public void Load(Memory memory)
    {
        foreach (var (address, bytes) in Segments)
            memory.Set(address, bytes);
    }
}

/// <summary>
/// A small two pass 6502 assembler.
/// Supports labels (<c>name:</c>), constants (<c>name = expr</c>), <c>.org</c>, <c>.byte</c>, <c>.word</c>,
/// <c>.text</c>, <c>.fill</c>, immediate (<c>#</c>), <c>&lt;</c>/<c>&gt;</c> low/high byte operators, <c>+</c>/<c>-</c>
/// expressions, and numbers as decimal, <c>$hex</c>, <c>0xhex</c>, <c>%binary</c> or <c>'c'</c>. Comments start with <c>;</c>.
/// </summary>
public static class Assembler
{
    public const int DefaultOrigin = 0x600;

    static readonly Regex LabelRegex = new(@"^\s*([A-Za-z_][A-Za-z0-9_]*):", RegexOptions.Compiled);
    static readonly Regex ConstRegex = new(@"^\s*([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(.+)$", RegexOptions.Compiled);

    sealed record Item(int Line, int Address, Instruction? Instruction, string Operand, string? Directive);

    public static AssembledProgram Assemble(string source, int origin = DefaultOrigin)
    {
        var program = new AssembledProgram();
        var items = new List<Item>();
        int pc = origin;
        int lineNumber = 0;

        // Pass 1: sizes and symbols.
        foreach (string rawLine in source.Split('\n'))
        {
            lineNumber++;
            string line = StripComment(rawLine).Trim();

            Match m;
            while ((m = LabelRegex.Match(line)).Success)
            {
                Define(program, m.Groups[1].Value, pc, lineNumber);
                line = line[m.Length..].Trim();
            }

            if (line.Length == 0) continue;

            if ((m = ConstRegex.Match(line)).Success && !line.StartsWith('.'))
            {
                Define(program, m.Groups[1].Value, Evaluate(m.Groups[2].Value, program.Symbols, lineNumber, strict: true), lineNumber);
                continue;
            }

            int split = line.IndexOfAny([' ', '\t']);
            string head = split < 0 ? line : line[..split];
            string operand = split < 0 ? "" : line[split..].Trim();

            if (head.StartsWith('.'))
            {
                string directive = head.ToLowerInvariant();
                if (directive is ".org" or ".pc")
                {
                    pc = Evaluate(operand, program.Symbols, lineNumber, strict: true);
                    continue;
                }

                int size = directive switch
                {
                    ".byte" or ".db" => SplitArguments(operand).Count,
                    ".word" or ".dw" => SplitArguments(operand).Count * 2,
                    ".text" or ".ascii" => ParseString(operand, lineNumber).Length,
                    ".fill" => Evaluate(SplitArguments(operand)[0], program.Symbols, lineNumber, strict: true),
                    _ => throw new AssemblyException("Unknown directive " + head, lineNumber),
                };
                items.Add(new Item(lineNumber, pc, null, operand, directive));
                pc += size;
                continue;
            }

            if (!Opcodes.IsMnemonic(head))
                throw new AssemblyException("Unknown mnemonic " + head, lineNumber);

            Instruction instruction = ChooseInstruction(head, operand, program.Symbols, lineNumber);
            items.Add(new Item(lineNumber, pc, instruction, operand, null));
            pc += instruction.Length;
        }

        // Pass 2: emit bytes.
        var current = new List<byte>();
        int segmentStart = -1;
        int expected = -1;

        void Emit(int address, IEnumerable<byte> bytes)
        {
            if (address != expected)
            {
                if (current.Count > 0) program.Segments.Add((segmentStart, current.ToArray()));
                current = new List<byte>();
                segmentStart = address;
            }
            current.AddRange(bytes);
            expected = address + (current.Count - (address - segmentStart));
        }

        foreach (Item item in items)
        {
            if (program.Entry < 0 && item.Instruction is not null)
                program.Entry = item.Address;
            Emit(item.Address, EmitItem(item, program.Symbols));
        }

        if (current.Count > 0)
            program.Segments.Add((segmentStart, current.ToArray()));

        if (program.Entry < 0)
            program.Entry = program.Origin;

        return program;
    }

    /// <summary>Assembles a source file.</summary>
    public static AssembledProgram AssembleFile(string path, int origin = DefaultOrigin) =>
        Assemble(File.ReadAllText(path), origin);

    static void Define(AssembledProgram program, string name, int value, int line)
    {
        if (Opcodes.IsMnemonic(name))
            throw new AssemblyException($"'{name}' is a mnemonic and cannot be used as a symbol", line);
        if (!program.Symbols.TryAdd(name, value))
            throw new AssemblyException($"Symbol '{name}' is already defined", line);
    }

    static string StripComment(string line)
    {
        bool inString = false;
        for (int i = 0; i < line.Length; i++)
        {
            if (line[i] == '"' ) inString = !inString;
            else if (line[i] == ';' && !inString) return line[..i];
        }
        return line;
    }

    static IEnumerable<byte> EmitItem(Item item, Dictionary<string, int> symbols)
    {
        var bytes = new List<byte>();
        if (item.Instruction is Instruction instruction)
        {
            bytes.Add(instruction.Code);
            if (instruction.Length == 1) return bytes;

            string operand = StripAddressing(item.Operand, instruction.Mode);
            int value = Evaluate(operand, symbols, item.Line, strict: true);

            switch (instruction.Mode)
            {
                case AddressingMode.Relative:
                    int offset = value - (item.Address + 2);
                    if (offset is < -128 or > 127)
                        throw new AssemblyException($"Branch target out of range ({offset} bytes)", item.Line);
                    bytes.Add((byte)offset);
                    break;
                case AddressingMode.Absolute or AddressingMode.AbsoluteX or AddressingMode.AbsoluteY or AddressingMode.Indirect:
                    CheckRange(value, 0xFFFF, item.Line);
                    bytes.Add((byte)value);
                    bytes.Add((byte)(value >> 8));
                    break;
                default:
                    CheckRange(value, 0xFF, item.Line);
                    bytes.Add((byte)value);
                    break;
            }
            return bytes;
        }

        switch (item.Directive)
        {
            case ".byte" or ".db":
                foreach (string arg in SplitArguments(item.Operand))
                {
                    int v = Evaluate(arg, symbols, item.Line, strict: true);
                    CheckRange(v, 0xFF, item.Line);
                    bytes.Add((byte)v);
                }
                break;
            case ".word" or ".dw":
                foreach (string arg in SplitArguments(item.Operand))
                {
                    int v = Evaluate(arg, symbols, item.Line, strict: true);
                    CheckRange(v, 0xFFFF, item.Line);
                    bytes.Add((byte)v);
                    bytes.Add((byte)(v >> 8));
                }
                break;
            case ".text" or ".ascii":
                bytes.AddRange(ParseString(item.Operand, item.Line));
                break;
            case ".fill":
                var args = SplitArguments(item.Operand);
                int count = Evaluate(args[0], symbols, item.Line, strict: true);
                byte fill = args.Count > 1 ? (byte)Evaluate(args[1], symbols, item.Line, strict: true) : (byte)0;
                bytes.AddRange(Enumerable.Repeat(fill, count));
                break;
        }
        return bytes;
    }

    static void CheckRange(int value, int max, int line)
    {
        // Allow negative values that fit in a signed byte / word (e.g. #-1).
        int min = max == 0xFF ? -128 : -32768;
        if (value > max || value < min)
            throw new AssemblyException($"Value {value} does not fit in {(max == 0xFF ? "a byte" : "a word")}", line);
    }

    /// <summary>Parses the operand shape, then picks zero page over absolute when the value is known to fit.</summary>
    static Instruction ChooseInstruction(string mnemonic, string operand, Dictionary<string, int> symbols, int line)
    {
        mnemonic = mnemonic.ToUpperInvariant();
        operand = operand.Trim();
        AddressingMode[] candidates;

        if (operand.Length == 0)
            candidates = [AddressingMode.Implied, AddressingMode.Accumulator];
        else if (operand.Equals("A", StringComparison.OrdinalIgnoreCase))
            candidates = [AddressingMode.Accumulator];
        else if (operand.StartsWith('#'))
            candidates = [AddressingMode.Immediate];
        else if (Regex.IsMatch(operand, @"^\(.*,\s*[xX]\s*\)$"))
            candidates = [AddressingMode.IndirectX];
        else if (Regex.IsMatch(operand, @"^\(.*\)\s*,\s*[yY]$"))
            candidates = [AddressingMode.IndirectY];
        else if (operand.StartsWith('(') && operand.EndsWith(')'))
            candidates = [AddressingMode.Indirect];
        else if (Opcodes.TryGet(mnemonic, AddressingMode.Relative, out _))
            candidates = [AddressingMode.Relative];
        else
        {
            bool x = Regex.IsMatch(operand, @",\s*[xX]$");
            bool y = Regex.IsMatch(operand, @",\s*[yY]$");
            string expr = StripAddressing(operand, x ? AddressingMode.AbsoluteX : y ? AddressingMode.AbsoluteY : AddressingMode.Absolute);
            int? value = TryEvaluate(expr, symbols, line);
            bool zeroPage = value is >= 0 and <= 0xFF;

            candidates = (x, y, zeroPage) switch
            {
                (true, _, true) => [AddressingMode.ZeroPageX, AddressingMode.AbsoluteX],
                (true, _, false) => [AddressingMode.AbsoluteX],
                (_, true, true) => [AddressingMode.ZeroPageY, AddressingMode.AbsoluteY],
                (_, true, false) => [AddressingMode.AbsoluteY],
                (_, _, true) => [AddressingMode.ZeroPage, AddressingMode.Absolute],
                _ => [AddressingMode.Absolute],
            };
        }

        foreach (AddressingMode mode in candidates)
            if (Opcodes.TryGet(mnemonic, mode, out Instruction instruction))
                return instruction;

        throw new AssemblyException($"{mnemonic} does not support operand '{operand}'", line);
    }

    /// <summary>Removes #, parentheses and index suffixes leaving the bare expression.</summary>
    static string StripAddressing(string operand, AddressingMode mode)
    {
        operand = operand.Trim();
        switch (mode)
        {
            case AddressingMode.Immediate:
                return operand[1..];
            case AddressingMode.IndirectX:
                return Regex.Replace(operand, @"^\((.*),\s*[xX]\s*\)$", "$1");
            case AddressingMode.IndirectY:
                return Regex.Replace(operand, @"^\((.*)\)\s*,\s*[yY]$", "$1");
            case AddressingMode.Indirect:
                return operand[1..^1];
            case AddressingMode.ZeroPageX or AddressingMode.AbsoluteX:
                return Regex.Replace(operand, @",\s*[xX]$", "");
            case AddressingMode.ZeroPageY or AddressingMode.AbsoluteY:
                return Regex.Replace(operand, @",\s*[yY]$", "");
            default:
                return operand;
        }
    }

    static List<string> SplitArguments(string operand)
    {
        var args = new List<string>();
        var sb = new StringBuilder();
        bool inString = false;
        foreach (char c in operand)
        {
            if (c == '"' || c == '\'') inString = !inString;
            if (c == ',' && !inString) { args.Add(sb.ToString().Trim()); sb.Clear(); }
            else sb.Append(c);
        }
        if (sb.ToString().Trim().Length > 0 || args.Count > 0) args.Add(sb.ToString().Trim());
        return args;
    }

    static byte[] ParseString(string operand, int line)
    {
        operand = operand.Trim();
        if (operand.Length < 2 || operand[0] != '"' || operand[^1] != '"')
            throw new AssemblyException("Expected a quoted string", line);
        return Encoding.ASCII.GetBytes(operand[1..^1]);
    }

    static int? TryEvaluate(string expression, Dictionary<string, int> symbols, int line)
    {
        try { return Evaluate(expression, symbols, line, strict: false); }
        catch (AssemblyException) { return null; }
    }

    /// <summary>Evaluates <c>[&lt;|&gt;]term (+|- term)*</c>. Non-strict mode returns 0x1000 (a placeholder that is not zero page) for unknown symbols.</summary>
    static int Evaluate(string expression, Dictionary<string, int> symbols, int line, bool strict)
    {
        expression = expression.Trim();
        if (expression.Length == 0)
            throw new AssemblyException("Missing operand", line);

        int highLow = 0; // 1 = low byte, 2 = high byte
        if (expression[0] == '<') { highLow = 1; expression = expression[1..]; }
        else if (expression[0] == '>') { highLow = 2; expression = expression[1..]; }

        int total = 0;
        int sign = 1;
        int i = 0;
        bool expectTerm = true;
        while (i < expression.Length)
        {
            char c = expression[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }
            if (c is '+' or '-' && expectTerm) { if (c == '-') sign = -sign; i++; continue; }
            if (c is '+' or '-') { sign = c == '-' ? -1 : 1; expectTerm = true; i++; continue; }

            int start = i;
            if (c == '\'' && i + 2 < expression.Length && expression[i + 2] == '\'')
                i += 3;
            else
                while (i < expression.Length && (char.IsLetterOrDigit(expression[i]) || expression[i] is '_' or '$' or '%')) i++;

            if (i == start)
                throw new AssemblyException($"Unexpected '{c}' in expression '{expression}'", line);

            total += sign * ParseTerm(expression[start..i], symbols, line, strict);
            sign = 1;
            expectTerm = false;
        }

        return highLow switch
        {
            1 => total & 0xFF,
            2 => (total >> 8) & 0xFF,
            _ => total,
        };
    }

    static int ParseTerm(string term, Dictionary<string, int> symbols, int line, bool strict)
    {
        if (term.Length == 3 && term[0] == '\'' && term[2] == '\'') return term[1];
        if (term[0] == '$' && int.TryParse(term[1..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int hex)) return hex;
        if (term.StartsWith("0x", StringComparison.OrdinalIgnoreCase) && int.TryParse(term[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out hex)) return hex;
        if (term[0] == '%') { try { return Convert.ToInt32(term[1..], 2); } catch (Exception) { throw new AssemblyException("Bad binary number " + term, line); } }
        if (char.IsDigit(term[0]) && int.TryParse(term, NumberStyles.None, CultureInfo.InvariantCulture, out int dec)) return dec;
        if (symbols.TryGetValue(term, out int symbol)) return symbol;
        if (!strict) return 0x1000;
        throw new AssemblyException("Unknown symbol or bad number '" + term + "'", line);
    }
}
