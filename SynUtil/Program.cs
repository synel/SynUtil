using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Syndll2;

namespace SynUtil
{
    internal class Program
    {
        private static string _host;
        private static int _port = 3734;
        private static int _terminalId;
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);
        private static bool _verbose;

        private static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                DisplayHelpText();
                return;
            }

            // host and port
            var arg0 = args[0].Split(':');
            _host = arg0[0];
            if (arg0.Length == 2 && !int.TryParse(arg0[1], out _port))
            {
                Console.WriteLine("Invalid Port.");
                return;
            }

            // terminal id
            var tidArg = args.FirstOrDefault(x => x.StartsWith("-t", StringComparison.OrdinalIgnoreCase));
            if (tidArg != null && (tidArg.Length < 3 || !int.TryParse(tidArg.Substring(2), out _terminalId)))
            {
                Console.WriteLine("Invalid Terminal ID.");
                return;
            }

            // verbose flag
            _verbose = args.Contains("-v", StringComparer.OrdinalIgnoreCase);
            if (_verbose)
                Trace.Listeners.Add(new TimedConsoleTraceListener());

            // command
            try
            {
                var command = args.Skip(1).First(x => !x.StartsWith("-"));
                var commandIndex = Array.IndexOf(args, command);
                var commandArgs = args.Skip(commandIndex + 1).Where(x => !x.StartsWith("-"));
                var commandArg = commandArgs.FirstOrDefault();

                switch (command.ToLowerInvariant())
                {
                    case "getstatus":
                        GetStatus();
                        break;
                    case "gethardwareinfo":
                        GetHardwareInfo();
                        break;
                    case "getnetworkinfo":
                        GetNetworkInfo();
                        break;
                    case "settime":
                        SetTime();
                        break;
                    case "eraseallmemory":
                        EraseAllMemory();
                        break;
                    case "deletealltables":
                        DeleteAllTables();
                        break;
                    case "deletetable":
                        if (string.IsNullOrEmpty(commandArg))
                            Console.WriteLine("Pass the table type and id to delete.  Example:  deletetable x001");
                        else
                            DeleteTable(commandArg);
                        break;
                    case "upload":
                        if (string.IsNullOrEmpty(commandArg))
                            Console.WriteLine("Pass the path(s) of the file(s) to upload.");
                        else
                            UploadFile(commandArgs);
                        break;
                    default:
                        {
                            Console.WriteLine("Unsupported command.");
                            break;
                        }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);

                if (ex is TimeoutException)
                    Console.WriteLine("Be sure that you specified the correct host, port, and terminal ID.");
            }
        }

        private static void DisplayHelpText()
        {
            Console.WriteLine();
            Console.WriteLine("SynUtil v" + typeof(Program).Assembly.GetName().Version.ToString(3));
            Console.WriteLine("Command-line utility for SY7xx terminals, using Syndll2.");
            Console.WriteLine("Copyright (C) 2013, Synel Industries Ltd.");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine();
            Console.WriteLine("  SynUtil <host>[:port] [-t<terminal id>] [-v] <command> [arguments]");
            Console.WriteLine("");
            Console.WriteLine("Parameters:");
            Console.WriteLine("  host - The IP or DNS name of the terminal.");
            Console.WriteLine("  port - The TCP port to connect to.  Defaults to 3734.");
            Console.WriteLine("  -t<terminal id> - The terminal id to use.  Defaults to 0.");
            Console.WriteLine("  -v - Omits verbose debugging information.");
            Console.WriteLine("  command - The command to execute.");
            Console.WriteLine("  arguments - Any arguments required for the specific command.");
            Console.WriteLine();
            Console.WriteLine("Commands:");
            Console.WriteLine("  getstatus          - Displays the terminal's status information.");
            Console.WriteLine();
            Console.WriteLine("  gethardwareinfo    - Displays the terminal's hardware information.");
            Console.WriteLine();
            Console.WriteLine("  getnetworkinfo     - Displays the terminal's network information.");
            Console.WriteLine();
            Console.WriteLine("  settime            - Sets the terminal's date and time to the");
            Console.WriteLine("                       current date and time of this computer.");
            Console.WriteLine();
            Console.WriteLine("  deletetable <tXXX> - Deletes a specific table from the terminal.");
            Console.WriteLine();
            Console.WriteLine("  deletealltables    - Deletes all tables from the terminal.");
            Console.WriteLine();
            Console.WriteLine("  eraseallmemory     - Erases all of the terminal's memory.");
            Console.WriteLine();
            Console.WriteLine("  upload <file1> [file2] [file3] [...] ");
            Console.WriteLine("                     - Uploads one or more RDY files to the terminal.");
            Console.WriteLine("                       Supports wildcards and dirXXX files also.");
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  SynUtil 1.2.3.4 getstatus");
            Console.WriteLine("  SynUtil 1.2.3.4 -t0 getstatus");
            Console.WriteLine("  SynUtil 1.2.3.4:3734 getstatus");
            Console.WriteLine("  SynUtil 1.2.3.4:3734 -t0 getstatus");
            Console.WriteLine();
        }

        private static void GetStatus()
        {
            using (var client = SynelClient.Connect(_host, _port, _terminalId, Timeout))
            {
                var info = client.Terminal.GetTerminalStatus();
                Console.WriteLine();
                Console.WriteLine("Hardware Model:      {0}", info.HardwareModel);
                Console.WriteLine("Hardware Revision:   {0}", info.HardwareRevision);
                Console.WriteLine("Firmware Version:    {0}", info.FirmwareVersion);
                Console.WriteLine("Terminal Type:       {0}", info.TerminalType);
                Console.WriteLine("Current Time:        {0:g}", info.Timestamp);
                Console.WriteLine("Active Function:     {0}", info.ActiveFunction);
                Console.WriteLine("Powered On:          {0}", info.PoweredOn);
                Console.WriteLine("Buffers Full:        {0}", info.BuffersFull);
                Console.WriteLine("Buffers Faulty:      {0}", info.BuffersFaulty);
                Console.WriteLine("Buffers Transmitted: {0}", info.BuffersTransmitted);
                Console.WriteLine("Buffers Empty:       {0}", info.BuffersEmpty);
                Console.WriteLine("Memory Used:         {0} bytes", info.MemoryUsed);
                Console.WriteLine("Polling Interval:    {0} seconds", info.PollingInterval.TotalSeconds);
                Console.WriteLine("Transport Type:      {0}", info.TransportType.ToString().ToUpperInvariant());
                Console.WriteLine("FPU Mode:            {0}", info.FingerprintUnitMode);
                Console.WriteLine();
            }
        }

        private static void GetHardwareInfo()
        {
            using (var client = SynelClient.Connect(_host, _port, _terminalId, Timeout))
            {
                var info = client.Terminal.GetHardwareConfiguration();
                Console.WriteLine();
                Console.WriteLine("Terminal ID:         {0}", info.TerminalId);
                Console.WriteLine("Terminal Type:       {0}", info.TerminalType);
                Console.WriteLine("Firmware Version:    {0} ({1:d})", info.FirmwareVersion, info.FirmwareDate);
                Console.WriteLine("Keyboard Type:       {0}", info.KeyboardType);
                Console.WriteLine("Display Type:        {0}", info.DisplayType);
                Console.WriteLine("FPU Type:            {0}", info.FingerprintUnitType);
                Console.WriteLine("FPU Mode:            {0}", info.FingerprintUnitMode);
                Console.WriteLine("Serial Port Info:    {0} {1}", info.HostSerialBaudRate, info.HostSerialParameters.ToUpperInvariant());
                Console.WriteLine("User Defined Field:  {0}", info.UserDefinedField);
                Console.WriteLine();
            }
        }

        private static void GetNetworkInfo()
        {
            using (var client = SynelClient.Connect(_host, _port, _terminalId, Timeout))
            {
                var info = client.Terminal.GetNetworkConfiguration();
                Console.WriteLine();
                Console.WriteLine("Network Card:        {0} (ver {1})", info.NetworkCardType, info.NetworkCardFirmwareVersion);
                Console.WriteLine("Transport Type:      {0}", info.TransportType.ToString().ToUpperInvariant());
                Console.WriteLine("MAC Address:         {0}", info.TerminalMACAddress);
                Console.WriteLine("IP Address/Port:     {0}:{1}", info.TerminalIPAddress, info.TerminalPort);
                Console.WriteLine("Remote Address/Port: {0}:{1}", info.RemoteIPAddress, info.RemotePort);
                Console.WriteLine("Subnet Mask:         {0}", info.SubnetMask);
                Console.WriteLine("Gateway Address:     {0}", info.GatewayIPAddress);
                Console.WriteLine("Disconnect Time:     {0} seconds", info.DisconnectTime.TotalSeconds);
                Console.WriteLine("Polling Interval:    {0} seconds", info.PollingInterval.TotalSeconds);
                Console.WriteLine("Polling Enabled:     {0}", info.EnablePolling);
                Console.WriteLine("DHCP Enabled:        {0}", info.EnableDHCP);
                Console.WriteLine("MAC Sending Enabled: {0}", info.EnableSendMAC);
                Console.WriteLine();
            }
        }

        private static void SetTime()
        {
            using (var client = SynelClient.Connect(_host, _port, _terminalId, Timeout))
            {
                var now = DateTime.Now;
                client.Terminal.SetTerminalClock(now);
                Console.WriteLine("Set the terminal clock to {0:g}", now);
            }
        }

        private static void DeleteTable(string tableName)
        {
            if (tableName.Length != 4)
            {
                Console.WriteLine("Invalid table name.");
                return;
            }

            var type = tableName[0];
            int id;
            if (!int.TryParse(tableName.Substring(1), out id))
            {
                Console.WriteLine("Invalid table name.");
                return;
            }

            using (var client = SynelClient.Connect(_host, _port, _terminalId, Timeout))
            using (var p = client.Terminal.Programming())
            {
                p.DeleteTable(type, id);
                Console.WriteLine("Sent command to delete table {0} from the terminal.", tableName);
            }
        }

        private static void DeleteAllTables()
        {
            using (var client = SynelClient.Connect(_host, _port, _terminalId, Timeout))
            using (var p = client.Terminal.Programming())
            {
                p.DeleteAllTables();
                Console.WriteLine("Sent command to delete all tables from the terminal.");
            }
        }

        private static void EraseAllMemory()
        {
            using (var client = SynelClient.Connect(_host, _port, _terminalId, Timeout))
            using (var p = client.Terminal.Programming())
            {
                p.EraseAllMemoryFromTerminal();
                Console.WriteLine("Sent command to erase all memory from the terminal.");
            }
        }

        private static void UploadFile(IEnumerable<string> paths)
        {
            using (var client = SynelClient.Connect(_host, _port, _terminalId, Timeout))
            using (var p = client.Terminal.Programming())
            {
                Console.CursorVisible = false;

                string filename = null;

                if (!_verbose)
                {
                    p.ProgressChanged += (sender, args) =>
                        {
                            if (filename != args.Filename)
                            {
                                filename = args.Filename;
                                Console.WriteLine();
                                Console.Write("Uploading {0} ", filename.PadRight(12, ' '));
                            }

                            const int barSize = 30;

                            DrawProgressBar(args.CurrentBlock, args.TotalBlocks, barSize);

                            if (args.CurrentBlock == args.TotalBlocks)
                            {
                                Console.CursorLeft += barSize + 1;
                                Console.Write("Complete!");
                            }
                        };
                }

                foreach (var path in paths)
                {
                    var directory = Path.GetDirectoryName(path);
                    var pattern = Path.GetFileName(path);
                    
                    if (directory == null || pattern == null)
                        continue;

                    var fullDir = Path.GetFullPath(directory);
                    var files = Directory.GetFiles(fullDir, pattern).OrderBy(x => x);

                    foreach (var file in files)
                    {
                        var thisPath = Path.Combine(fullDir, file);
                        p.UploadTableFromFile(thisPath);
                    }
                }
                

                if (!_verbose)
                {
                    Console.WriteLine();
                    Console.CursorVisible = true;
                }
            }
        }

        private static void DrawProgressBar(int current, int total, int barSize)
        {
            const char character = '\u2592';

            var left = Console.CursorLeft;
            var percent = (double)current / total;
            var chars = (int)Math.Floor(percent * barSize);
            
            var originalColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write(new string(character, chars));
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.Write(new string(character, barSize - chars));
            Console.ForegroundColor = originalColor;
            var pct = string.Format(" {0:N2}%", percent*100);
            Console.Write(pct.PadRight(12));
            Console.Write("[{0}/{1}]", current, total);
            Console.CursorLeft = left;
        }
    }
}
