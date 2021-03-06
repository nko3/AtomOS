﻿/*
* PROJECT:          Atomix Development
* LICENSE:          BSD 3-Clause (LICENSE.md)
* PURPOSE:          Shl MSIL
* PROGRAMMERS:      Aman Priyadarshi (aman.eureka@gmail.com)
*/

using System;
using System.Reflection;

using Atomixilc.Machine;
using Atomixilc.Attributes;
using Atomixilc.Machine.x86;

namespace Atomixilc.IL
{
    [ILImpl(ILCode.Shl)]
    internal class Shl_il : MSIL
    {
        public Shl_il()
            : base(ILCode.Shl)
        {

        }

        /*
         * URL : https://msdn.microsoft.com/en-us/library/system.reflection.emit.opcodes.Shl(v=vs.110).aspx
         * Description : Shifts an integer value to the left (in zeroes) by a specified number of bits, pushing the result onto the evaluation stack.
         */
        internal override void Execute(Options Config, OpCodeType xOp, MethodBase method, Optimizer Optimizer)
        {
            if (Optimizer.vStack.Count < 2)
                throw new Exception("Internal Compiler Error: vStack.Count < 2");

            var itemA = Optimizer.vStack.Pop();
            var itemB = Optimizer.vStack.Pop();

            var size = Math.Max(Helper.GetTypeSize(itemA.OperandType, Config.TargetPlatform),
                Helper.GetTypeSize(itemB.OperandType, Config.TargetPlatform));

            /* The stack transitional behavior, in sequential order, is:
             * A value is pushed onto the stack.
             * The amount of bits to be shifted is pushed onto the stack.
             * The number of bits to be shifted and the value are popped from the stack; the value is shifted left by the specified number of bits.
             * The result is pushed onto the stack.
             */

            switch (Config.TargetPlatform)
            {
                case Architecture.x86:
                    {
                        if (itemA.IsFloat || itemB.IsFloat || size > 4)
                            throw new Exception(string.Format("UnImplemented '{0}'", msIL));

                        if (!itemA.SystemStack || !itemB.SystemStack)
                            throw new Exception(string.Format("UnImplemented-RegisterType '{0}'", msIL));

                        new Pop { DestinationReg = Register.ECX };
                        new Shl { DestinationReg = Register.ESP, SourceReg = Register.CL, DestinationIndirect = true };

                        Optimizer.vStack.Push(new StackItem(itemB.OperandType));
                        Optimizer.SaveStack(xOp.NextPosition);
                    }
                    break;
                default:
                    throw new Exception(string.Format("Unsupported target platform '{0}' for MSIL '{1}'", Config.TargetPlatform, msIL));
            }
        }
    }
}
