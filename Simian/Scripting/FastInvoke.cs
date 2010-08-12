/*
 * Copyright (c) Open Metaverse Foundation and Alessandro Febretti
 * All rights reserved.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions
 * are met:
 * 1. Redistributions of source code must retain the above copyright
 *    notice, this list of conditions and the following disclaimer.
 * 2. Redistributions in binary form must reproduce the above copyright
 *    notice, this list of conditions and the following disclaimer in the
 *    documentation and/or other materials provided with the distribution.
 * 3. The name of the author may not be used to endorse or promote products
 *    derived from this software without specific prior written permission.
 * 
 * THIS SOFTWARE IS PROVIDED BY THE AUTHOR ``AS IS'' AND ANY EXPRESS OR
 * IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES
 * OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED.
 * IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR ANY DIRECT, INDIRECT,
 * INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT
 * NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
 * DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
 * THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF
 * THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */




using System;
using System.Reflection;
using System.Reflection.Emit;

namespace Simian
{
    public delegate object FastInvokeDelegate(object target, object[] args);

    public static class FastInvoke
    {
        private static readonly Type[] ArgTypes = { typeof(object), typeof(object[]) };

        public static FastInvokeDelegate Create(MethodInfo method)
        {
            ParameterInfo[] parms = method.GetParameters();
            int numparams = parms.Length;

            // Create a dynamic method and obtain its IL generator to inject code
            DynamicMethod dynam = new DynamicMethod(String.Empty, typeof(object), ArgTypes, typeof(FastInvoke));
            ILGenerator il = dynam.GetILGenerator();

            #region IL generation

            #region Argument count check

            // Define a label for succesfull argument count checking
            Label argsOK = il.DefineLabel();

            // Check input argument count
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldlen);
            il.Emit(OpCodes.Ldc_I4, numparams);
            il.Emit(OpCodes.Beq, argsOK);

            // Argument count was wrong, throw TargetParameterCountException
            il.Emit(OpCodes.Newobj, typeof(TargetParameterCountException).GetConstructor(Type.EmptyTypes));
            il.Emit(OpCodes.Throw);

            // Mark IL with argsOK label
            il.MarkLabel(argsOK);

            #endregion Argument count check

            #region Standard argument layout

            // If method isn't static push target instance on top
            // of stack
            if (!method.IsStatic)
                il.Emit(OpCodes.Ldarg_0); // Argument 0 of dynamic method is target instance

            // Lay out args array onto stack
            int i = 0;
            while (i < numparams)
            {
                // Push args array reference onto the stack, followed
                // by the current argument index (i). The Ldelem_Ref opcode
                // will resolve them to args[i]

                // Argument 1 of dynamic method is argument array
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Ldc_I4, i);
                il.Emit(OpCodes.Ldelem_Ref);

                // If parameter [i] is a value type perform an unboxing
                Type parmType = parms[i].ParameterType;
                if (parmType.IsValueType)
                    il.Emit(OpCodes.Unbox_Any, parmType);

                i++;
            }

            #endregion Standard argument layout

            #region Method call

            // Perform actual call.
            // If method is not final and virtual callvirt is required
            // otherwise a normal call will be emitted
            if (method.IsFinal || !method.IsVirtual)
                il.Emit(OpCodes.Call, method);
            else
                il.Emit(OpCodes.Callvirt, method);

            if (method.ReturnType != typeof(void))
            {
                // If result is of value type it needs to be boxed
                if (method.ReturnType.IsValueType)
                    il.Emit(OpCodes.Box, method.ReturnType);
            }
            else
            {
                il.Emit(OpCodes.Ldnull);
            }

            // Emit return opcode
            il.Emit(OpCodes.Ret);

            #endregion Method call

            #endregion IL generation

            return (FastInvokeDelegate)dynam.CreateDelegate(typeof(FastInvokeDelegate));
        }
    }
}
