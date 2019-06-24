/**
 * UDP-socket Network Server
 * INTERNAL/PROPRIETARY/CONFIDENTIAL. Use is subject to license terms.
 * DO NOT ALTER OR REMOVE COPYRIGHT NOTICES OR THIS FILE HEADER.
 * 
 * Author:	Bryan Biedenkapp, <gatekeep@gmail.com>
 * Copyright 2018 Bryan Biedenkapp
 */
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using System.Threading;

using UDPServer.Service;

namespace UDPServer
{
    /// <summary>
    /// This class serves as the entry point for the application.
    /// </summary>
    public class UDPServerService : ServiceBase
    {
        /**
         * Fields
         */
        public const string _ServiceName = "UDPServer-Standalone-Service";
        public const string ServiceInstallName = "UDPServer";
        private const string ServiceNamespace = "UDPServer.Service";
        private const string ServiceAssembly = "UDPServer";

        private CommManager commManager;

        public const string lockFile = "udpsrv.lock";

        public static bool HasCompletedStartup = false;

        private static UDPServerService instance;
        
        /**
         * Methods
         */
        /// <summary>
        /// Initializes a new instance of the <see cref="UDPServerService"/> class.
        /// </summary>
        public UDPServerService()
        {
            instance = this;
            ServiceName = _ServiceName;
        }

        /// <summary>
        /// Occurs when the service is started.
        /// </summary>
        /// <param name="args">Arguments service started with</param>
        protected override void OnStart(string[] args)
        {
            List<string> extraArgs = new List<string>();

            // command line parameters
            OptionSet options = new OptionSet()
            {
                { "i=|interface=", "", v => { Trace.WriteLine("interface [" + v + "]"); Program.interfaceToUse = Convert.ToInt32(v); } },
            };

            // attempt to parse the commandline
            try
            {
                extraArgs = options.Parse(args);
            }
            catch (OptionException)
            {
                /* stub */
            }

            try
            {
                StartService();
            }
            catch (Exception e)
            {
                Util.StackTrace(e, false);
                OnStop();
            }
        }

        /// <summary>
        /// Starts the management service.
        /// </summary>
        public void StartService()
        {
            commManager = new CommManager();
            commManager.Start();

            // create service lock file
            FileStream _lock = File.Open(lockFile, FileMode.Create);

            // create stream writer and write this processes PID to the lock file
            StreamWriter stream = new StreamWriter(_lock);
            Process proc = Process.GetCurrentProcess();
            stream.WriteLine(proc.Id);
            stream.Flush();

            // dispose the file stream
            _lock.Dispose();
            HasCompletedStartup = true;
            Trace.WriteLine("started service");
        }

        /// <summary>
        /// Occurs when the service is stopped.
        /// </summary>
        protected override void OnStop()
        {
            HasCompletedStartup = false;
            try
            {
                StopService();
            }
            catch (Exception e)
            {
                StopService();
                Util.StackTrace(e, false);
            }
        }

        /// <summary>
        /// Stops the management service.
        /// </summary>
        public void StopService()
        {
            // stop comm manager
            if (commManager != null)
            {
                try
                {
                    commManager.Stop();
                    commManager = null;
                }
                catch (ThreadAbortException tae)
                {
                    Util.StackTrace(tae, false);
                }
            }

            // delete process lock file
            File.Delete(lockFile);
            Trace.WriteLine("stopped service");
        }

        /// <summary>
        /// Force starts the service on the CLI.
        /// </summary>
        /// <param name="args"></param>
        public void ForceStartCLI(string[] args)
        {
            OnStart(args);
        }

        /// <summary>
        /// Force stops the service running on the CLI.
        /// </summary>
        public void ForceStopCLI()
        {
            OnStop();
        }
    } // public class EmuService : ServiceBase
} // namespace UDPServer
