/*
 * Copyright (c) Open Metaverse Foundation
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

namespace Simian
{
    /// <summary>
    /// This attribute marks methods that should be registered as API calls
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class ScriptMethodAttribute : Attribute { }

    /// <summary>
    /// Holds a reference to an API call and generated code to dynamically
    /// call the method
    /// </summary>
    public sealed class ApiMethod
    {
        /// <summary>An instance of the API class containing this method, or 
        /// null if the API call is a static method</summary>
        public readonly IScriptApi Parent;
        /// <summary>Reflection information for the API call</summary>
        public readonly MethodInfo MethodInfo;
        /// <summary>Generated code to quickly call the API method</summary>
        public readonly FastInvokeDelegate Invoker;

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="parent">An instance of the API class containing this 
        /// method, or null if the API call is a static method</param>
        /// <param name="methodInfo">A reference to type information for the
        /// API call</param>
        public ApiMethod(IScriptApi parent, MethodInfo methodInfo)
        {
            Parent = parent;
            MethodInfo = methodInfo;
            Invoker = FastInvoke.Create(methodInfo);
        }

        /// <summary>
        /// Fires the API call
        /// </summary>
        /// <param name="args">An array of arguments to pass to the API call</param>
        /// <returns>The return value of the API call</returns>
        public object Call(object[] args)
        {
            return Invoker(Parent, args);
        }
    }
}
