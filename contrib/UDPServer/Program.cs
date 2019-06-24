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

using SharpPcap;

namespace UDPServer
{
    /// <summary>
    /// This class serves as the entry point for the application.
    /// </summary>
    public sealed class Program
    {
        /**
         * Fields
         */
        public static int interfaceToUse = 0;
        public static bool privateNetwork = false;

        /**
         * Methods
         */
        /// <summary>
        /// Internal helper to prints the program usage.
        /// </summary>
        private static void Usage(OptionSet p)
        {
            Console.WriteLine("usage: NetworkProxy <arguments ...>");
            Console.WriteLine();
            Console.WriteLine("Options:");
            p.WriteOptionDescriptions(Console.Out);
        }

        /// <summary>
        /// 
        /// </summary>
        private static void DisplayInterfaces()
        {
            CaptureDeviceList devices = CaptureDeviceList.Instance;
            int i = 0;

            // print out the available devices
            foreach (ICaptureDevice dev in devices)
            {
                Console.WriteLine("({0}) {1}", i, dev.Description);
                i++;
            }
        }

        /// <summary>
        /// Internal helper to execute the service in foreground mode.
        /// </summary>
        /// <param name="args"></param>
        private static void RunForeground(string[] args)
        {
            UDPServerService svc = new UDPServerService();
            svc.ForceStartCLI(args);

            Console.WriteLine(">>> Press Q to quit");

            while (Console.ReadKey().KeyChar.ToString().ToUpper() != "Q") ;

            svc.ForceStopCLI();
            Environment.Exit(0);
        }
#if WIN32
        /// <summary>
        /// Internal helper to install the server as a service on Windows computers.
        /// </summary>
        private static void InstallService()
        {
            ServiceInstaller installer = new ServiceInstaller();

            Console.WriteLine("Installing the NetworkProxy Service");
            string programExe = AppDomain.CurrentDomain.FriendlyName;
            try
            {
                installer.InstallService(Environment.CurrentDirectory + Path.DirectorySeparatorChar +
                    programExe, UDPServerService.ServiceInstallName, "NetworkProxy Server");
            }
            catch (Exception e)
            {
                Trace.WriteLine("failed to install service");
                Util.StackTrace(e, false);
            }
        }

        /// <summary>
        /// Internal helper to uninstall the server as a service on Windows computers.
        /// </summary>
        private static void UninstallService()
        {
            ServiceInstaller installer = new ServiceInstaller();

            Console.WriteLine("Uninstalling the NetworkProxy Service");
            try
            {
                installer.UninstallService(UDPServerService.ServiceInstallName);
            }
            catch (Exception e)
            {
                Trace.WriteLine("failed to uninstall service");
                Util.StackTrace(e, false);
            }
        }
#endif
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        public static void Main(string[] args)
        {
            List<string> extraArgs = new List<string>();
            bool showHelp = false, runForeground = false, displayInterfaces = false, privateNetwork = false;

            // command line parameters
            OptionSet options = new OptionSet()
            {
                { "h|help", "show this message and exit", v => showHelp = v != null },
                { "i=|interface=", "interface to capture and send packets on", v => { Trace.WriteLine("interface [" + v + "]"); interfaceToUse = Convert.ToInt32(v); } },
                { "display-interfaces", "display interfaces to use for packet capture", v => displayInterfaces = v != null },
                { "private-network", "relays packets between connected VARCem clients", v => privateNetwork = v != null },
#if WIN32
                { "install-service", "install the server as a service on Windows computers", v => InstallService() },
                { "uninstall-service", "uninstall the server as a service on Windows computers", v => UninstallService() },
#endif
                { "f|foreground", "force the server to run in the foreground mode", v => runForeground = v != null },
            };

            // attempt to parse the commandline
            try
            {
                extraArgs = options.Parse(args);
            }
            catch (OptionException)
            {
                Console.WriteLine("error: invalid arguments");
                Usage(options);
                Environment.Exit(1);
            }

            // show help
            if (showHelp)
                Usage(options);

            Program.privateNetwork = privateNetwork;
            if (runForeground)
            {
                Console.WriteLine(AssemblyVersion._VERSION_STRING + " (Built: " + AssemblyVersion._BUILD_DATE + ")");
                Console.WriteLine(AssemblyVersion._COPYRIGHT + "., All Rights Reserved.");
                Console.WriteLine();
            }

            if (!runForeground && !displayInterfaces)
            {
                Console.Error.WriteLine("Do not start the NetworkProxy as a normal program!");
                ServiceBase[] ServicesToRun;
                ServicesToRun = new ServiceBase[]
                {
                    new UDPServerService()
                };

                ServiceBase.Run(ServicesToRun);
            }

            if (runForeground)
                RunForeground(args);

            if (displayInterfaces)
                DisplayInterfaces();

            Environment.Exit(0);
        }
    } // public class Program
} // namespace UDPServer
