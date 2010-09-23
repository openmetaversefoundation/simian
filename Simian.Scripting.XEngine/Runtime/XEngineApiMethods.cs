using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using log4net;
using Microsoft.CSharp;
using Amib.Threading;
using OpenMetaverse;
using Simian.Protocols.Linden;

namespace Simian.Scripting.Linden
{
    public partial class XEngine : ISceneModule, ILSLScriptEngine
    {
        [ScriptMethod]
        public void state(IScriptInstance script, string newState)
        {
            TriggerState(script.ID, newState);
        }

        [ScriptMethod]
        public void llSleep(IScriptInstance script, double sec)
        {
            // Convert seconds to milliseconds and sleep
            System.Threading.Thread.Sleep((int)(sec * 0.001));
        }

        [ScriptMethod]
        public int llGetFreeMemory()
        {
            return 16384;
        }
    }
}
