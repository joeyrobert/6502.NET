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
using NUnit.Framework;

namespace Emulator.UnitTests
{
    [TestFixture]
    public class Interpreter
    {
        [SetUp]
        public void Init()
        {
            Emulator.Interpreter.Memory memory = new Emulator.Interpreter.Memory(0xFFFF);
            Emulator.Interpreter.Processor processor = new Emulator.Interpreter.Processor(memory);
        }

        [TearDown]
        public void Dispose()
        {
           
        }

        [Test]
        public void OpCode01()
        {

        }
    }
}
