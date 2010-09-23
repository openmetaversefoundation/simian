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
using System.Diagnostics;
using System.IO;
using System.Threading;
using log4net;
using log4net.Config;

[assembly: log4net.Config.XmlConfigurator(Watch = true)]

namespace Simian
{
    class Program
    {
        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);
        private static Simian m_simian;
        private static bool m_running = true;

        static void Main(string[] args)
        {
            bool coloredLogging = true;
            bool printHelp = false;
            bool printVersion = false;

            // Name the main thread
            Thread.CurrentThread.Name = "Main";

            #region Command Line Argument Handling

            Mono.Options.OptionSet set = new Mono.Options.OptionSet()
            {
                { "nocolor", "Disable colored console logging", v => coloredLogging = false },
                { "h|?|help", "Shows launch options", v => printHelp = true },
                { "version", "Show version information", v => printVersion = true }
            };
            set.Parse(args);

            if (printHelp)
            {
                set.WriteOptionDescriptions(Console.Out);
                return;
            }

            if (printVersion)
            {
                string version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
                Console.WriteLine("Simian " + version);
                return;
            }

            #endregion Command Line Argument Handling

            #region log4net Setup

            // If error level reporting isn't enabled we assume no logger is configured and initialize a default appender
            if (!m_log.Logger.IsEnabledFor(log4net.Core.Level.Error))
            {
                log4net.Appender.AppenderSkeleton appender;

                if (coloredLogging)
                {
                    log4net.Appender.ColoredConsoleAppender coloredAppender = new log4net.Appender.ColoredConsoleAppender();

                    var mapping = new log4net.Appender.ColoredConsoleAppender.LevelColors();
                    mapping.Level = log4net.Core.Level.Debug;
                    mapping.ForeColor = log4net.Appender.ColoredConsoleAppender.Colors.HighIntensity;
                    coloredAppender.AddMapping(mapping);

                    mapping = new log4net.Appender.ColoredConsoleAppender.LevelColors();
                    mapping.Level = log4net.Core.Level.Info;
                    mapping.ForeColor = log4net.Appender.ColoredConsoleAppender.Colors.White;
                    coloredAppender.AddMapping(mapping);

                    mapping = new log4net.Appender.ColoredConsoleAppender.LevelColors();
                    mapping.Level = log4net.Core.Level.Warn;
                    mapping.BackColor = log4net.Appender.ColoredConsoleAppender.Colors.Purple;
                    mapping.ForeColor = log4net.Appender.ColoredConsoleAppender.Colors.White;
                    coloredAppender.AddMapping(mapping);

                    mapping = new log4net.Appender.ColoredConsoleAppender.LevelColors();
                    mapping.Level = log4net.Core.Level.Error;
                    mapping.BackColor = log4net.Appender.ColoredConsoleAppender.Colors.Red;
                    mapping.ForeColor = log4net.Appender.ColoredConsoleAppender.Colors.White;
                    coloredAppender.AddMapping(mapping);

                    appender = coloredAppender;
                }
                else
                {
                    appender = new log4net.Appender.ConsoleAppender();
                }

                appender.Layout = new log4net.Layout.PatternLayout("%timestamp [%thread] %-5level %logger - %message%newline");
                appender.ActivateOptions();
                BasicConfigurator.Configure(appender);

                m_log.Info("No log configuration found, defaulting to console logging");
            }

            // Hook up Debug.Assert statements to log4net
            Debug.Listeners.Insert(0, new log4netTraceListener());

            #endregion log4net Setup

            // Set the working directory to the application dir
            Directory.SetCurrentDirectory(Util.ExecutingDirectory());

            // Initialize the Simian object
            m_simian = new Simian();

            // Handle Ctrl+C
            Console.CancelKeyPress +=
                delegate(object sender, ConsoleCancelEventArgs e)
                {
                    e.Cancel = true;

                    m_simian.Shutdown();
                    m_running = false;
                };
            
            // Attempt to load modules
            if (m_simian.LoadModules())
            {
                // Initialize the interactive console
                InteractiveConsole();
            }
            else
            {
                m_log.Error("Application module loading failed, shutting down");
            }
        }

        private static void CreateUserConsole()
        {
            string input;
            string promptName = "First name: ";
            // Last name:
            // E-mail:
            // Password:

            Mono.Terminal.LineEditor console = new Mono.Terminal.LineEditor("Simian");

            // FIXME: Finish implementing this to force a grid admin to be created on startup.
            // This will resolve the bootstrapping issues with estate/parcel ownership
            while (m_running && (input = console.Edit(promptName, String.Empty)) != null)
            {
                string[] inputWords = input.Split(' ');
                if (inputWords.Length > 0)
                {
                    string command = inputWords[0];
                    string[] args = new string[inputWords.Length - 1];

                    for (int i = 0; i < args.Length; i++)
                        args[i] = inputWords[i + 1];
                }
            }
        }

        private static void InteractiveConsole()
        {
            // Initialize the interactive console
            Mono.Terminal.LineEditor console = new Mono.Terminal.LineEditor("Simian", 100);
            console.TabAtStartCompletes = true;

            console.AutoCompleteEvent +=
                delegate(string text, int pos)
                {
                    string prefix = null;
                    string complete = text.Substring(0, pos);

                    string[] completions = m_simian.GetCompletions(complete, out prefix);
                    return new Mono.Terminal.LineEditor.Completion(prefix, completions);
                };

            // Wait a moment for async startup log messages to print so they don't hide
            // the initial console
            Thread.Sleep(Simian.LONG_SLEEP_INTERVAL);

            // Do a one-time forced garbage collection once everything is loaded and running
            GC.Collect();

            string input;
            string promptName = "simian";

            while (m_running && (input = console.Edit(promptName + '>', String.Empty)) != null)
            {
                string[] inputWords = input.Split(' ');
                if (inputWords.Length > 0)
                {
                    string command = inputWords[0];
                    string[] args = new string[inputWords.Length - 1];

                    for (int i = 0; i < args.Length; i++)
                        args[i] = inputWords[i + 1];

                    m_simian.HandleCommand(command, args, out promptName);
                }
            }
        }
    }

    public class log4netTraceListener : DefaultTraceListener
    {
        private static readonly ILog m_log = LogManager.GetLogger("TraceListener");

        public override void Fail(string message, string detailMessage)
        {
            StackTrace trace = new StackTrace(4, true);

            if (!String.IsNullOrEmpty(detailMessage))
            {
                if (!String.IsNullOrEmpty(message))
                    message += Environment.NewLine;
                message += detailMessage;
            }

            if (!String.IsNullOrEmpty(message))
                message += Environment.NewLine;

            message += trace.ToString();

            m_log.Error("[ASSERT]: " + message);
        }
    }
}
