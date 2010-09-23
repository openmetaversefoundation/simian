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
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Reflection;
using log4net;
using OpenMetaverse;

namespace Simian.Scripting.Linden
{
    [SceneModule("ScriptConsole")]
    public class ScriptConsole : ISceneModule
    {
        public sealed class ConsoleScriptInstance : IScriptInstance
        {
            private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);

            private readonly object[] m_emptyArgs;

            private UUID m_id;
            private IScene m_scene;
            private ISceneEntity m_host;

            public bool IsTimerEventQueued;
            public int LastControlEventQueued;
            public int ControlEventsQueued;
            public bool IsCollisionEventQueued;

            public UUID ID { get { return m_id; } }
            public UUID SourceAssetID { get { return UUID.Zero; } }
            public ISceneEntity Host { get { return m_host; } }

            public ConsoleScriptInstance(UUID id, IScene scene, ISceneEntity host)
            {
                m_id = id;
                m_scene = scene;
                m_host = host;
                m_emptyArgs = new object[] { this };
            }

            public void AddSleepMS(int ms)
            {
                System.Threading.Thread.Sleep(ms);
            }

            public object CallMethod(string methodName)
            {
                ApiMethod apiMethod;
                if (m_scene.TryGetApiMethod(methodName, out apiMethod))
                    return apiMethod.Call(m_emptyArgs);

                m_log.Warn("Could not find script API method " + methodName);
                return null;
            }

            public object CallMethod(string methodName, object[] args)
            {
                // TODO: Optimize this (could modify FastInvoke)
                object[] fullArgs = new object[args.Length + 1];
                fullArgs[0] = this;
                for (int i = 0; i < args.Length; i++)
                    fullArgs[i + 1] = args[i];

                ApiMethod apiMethod;
                if (m_scene.TryGetApiMethod(methodName, out apiMethod))
                    return apiMethod.Call(fullArgs);

                m_log.Warn("Could not find script API method " + methodName);
                return null;
            }
        }

        private IScene m_scene;
        private ConsoleScriptInstance m_scriptInstance;
 
        public void Start(IScene scene)
        {
            m_scene = scene;
            m_scriptInstance = new ConsoleScriptInstance(UUID.Zero, scene, null);

            m_scene.OnEntityChat += EntityChatHandler;
        }

        public void Stop()
        {
            m_scene.OnEntityChat -= EntityChatHandler;
        }

        private void EntityChatHandler(object sender, ChatArgs e)
        {
            if (e.Source is IScenePresence)
            {
                int startParam = e.Message.IndexOf('(');
                int endParam = e.Message.IndexOf(')');

                if (startParam > 2 && endParam > startParam)
                {
                    // Try and parse this into a function call
                    string name = e.Message.Substring(0, startParam);

                    ApiMethod apiMethod;
                    if (m_scene.TryGetApiMethod(name, out apiMethod))
                    {
                        // Parse the parameters
                        ++startParam;
                        List<string> parameters = ParseParameters(e.Message.Substring(startParam, endParam - startParam));

                        // Parameters sanity check
                        ParameterInfo[] parameterInfos = apiMethod.MethodInfo.GetParameters();
                        if (parameters != null && parameters.Count == parameterInfos.Length - 1)
                        {
                            // Convert the parameters into the required types
                            object[] objParameters = ConvertParameters(parameterInfos, parameters);

                            if (objParameters != null)
                            {
                                object ret = m_scriptInstance.CallMethod(name, objParameters);
                            }
                        }
                    }
                }
            }
        }

        private List<string> ParseParameters(string paramsString)
        {
            List<string> parameters = new List<string>();

            int i = 0;

            while (i < paramsString.Length)
            {
                int commaPos = paramsString.IndexOf(',', i);
                int bracketPos = paramsString.IndexOf('<', i);
                int endBracketPos = paramsString.IndexOf('>', i);
                string param;

                if (bracketPos > 0 && bracketPos < commaPos)
                {
                    if (endBracketPos > bracketPos)
                        commaPos = paramsString.IndexOf(',', endBracketPos);
                    else
                        return null;
                }

                if (commaPos > 0)
                {
                    // ...,
                    param = paramsString.Substring(i, commaPos - i);
                    i = commaPos + 1;
                }
                else
                {
                    // ...
                    param = paramsString.Substring(i);
                    i = paramsString.Length;
                }

                parameters.Add(param.Trim(new char[] { '"' }));
            }

            return parameters;
        }

        private object[] ConvertParameters(ParameterInfo[] parameterInfos, List<string> parameters)
        {
            System.Diagnostics.Debug.Assert(parameterInfos.Length - 1 == parameters.Count);

            object[] objParameters = new object[parameters.Count];

            for (int i = 0; i < parameters.Count; i++)
            {
                Type paramType = parameterInfos[i + 1].ParameterType;

                if (paramType == typeof(int))
                {
                    int value;
                    if (Int32.TryParse(parameters[i], out value))
                        objParameters[i] = (lsl_integer)value;
                    else
                        return null;
                }
                else if (paramType == typeof(double))
                {
                    double value;
                    if (Double.TryParse(parameters[i], out value))
                        objParameters[i] = (lsl_float)value;
                    else
                        return null;
                }
                else if (paramType == typeof(lsl_vector))
                {
                    lsl_vector value;
                    value = (lsl_vector)parameters[i];
                    objParameters[i] = value;
                }
                else if (paramType == typeof(lsl_rotation))
                {
                    lsl_rotation value;
                    value = (lsl_rotation)parameters[i];
                    objParameters[i] = value;
                }
                else
                {
                    // String value, or something that can (hopefully) be implicitly converted
                    objParameters[i] = (lsl_string)parameters[i];
                }
            }

            return objParameters;
        }
    }
}
