using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
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
            try
            {
                // verbose flag
                _verbose = args.Contains("-v", StringComparer.OrdinalIgnoreCase);
                if (_verbose)
                    Trace.Listeners.Add(new TimedConsoleTraceListener());

                // server mode is special input
                if (args.Contains("listen", StringComparer.InvariantCultureIgnoreCase))
                {
                    // port is optional
                    int i;
                    var p = args.FirstOrDefault(x => int.TryParse(x, out i));
                    if (p != null)
                        _port = int.Parse(p);

                    var acknowledge = args.Any(x => x.StartsWith("ack", StringComparison.InvariantCultureIgnoreCase));
                    Listen(acknowledge);

                    return;
                }

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

                // command
                var command = args.Skip(1).First(x => !x.StartsWith("-"));
                var commandIndex = Array.IndexOf(args, command);
                var commandArgs = args.Skip(commandIndex + 1).Where(x => !x.StartsWith("-")).ToArray();
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
                    case "getfingerprintinfo":
                        GetFingerprintInfo();
                        break;
                    case "settime":
                        SetTime(commandArgs);
                        break;
                    case "fixmemcrash":
                        FixMemCrash();
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
                    case "getdata":
                        GetData();
                        break;
                    case "resetdata":
                        ResetData();
                        break;
                    case "cleardata":
                        ClearData();
                        break;
                    default:
                        {
                            Console.WriteLine("Unsupported command.");
                            break;
                        }
                }
            }
            catch (SocketException ex)
            {
                if (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
                {
                    Console.WriteLine("Another application is already listening on port {0}.", _port);
                }
                else
                {
                    Console.WriteLine(ex.Message);
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
            Console.WriteLine("  SynUtil <host>[:port] [-t<id>] [-v] <command> [arguments]");
            Console.WriteLine("or");
            Console.WriteLine("  SynUtil listen [port] [ack]");
            Console.WriteLine("");
            Console.WriteLine("Parameters:");
            Console.WriteLine("  host      - The IP or DNS name of the terminal.");
            Console.WriteLine("  port      - The TCP port to connect to.  Defaults to 3734.");
            Console.WriteLine("  -t<id>    - The terminal id to use.  Defaults to 0.");
            Console.WriteLine("  -v        - Emits verbose debugging information.");
            Console.WriteLine("  command   - The command to execute.");
            Console.WriteLine("  arguments - Any arguments required for the specific command.");
            Console.WriteLine();
            Console.WriteLine("Commands:");
            Console.WriteLine("  getstatus          - Displays the terminal's status information.");
            Console.WriteLine();
            Console.WriteLine("  gethardwareinfo    - Displays the terminal's hardware information.");
            Console.WriteLine();
            Console.WriteLine("  getnetworkinfo     - Displays the terminal's network information.");
            Console.WriteLine();
            Console.WriteLine("  getfingerprintinfo - Displays the terminal's fingerprint information.");
            Console.WriteLine();
            Console.WriteLine("  settime [YYYY-MM-DD HH:MM:SS]");
            Console.WriteLine("                     - Sets the terminal's date and time.");
            Console.WriteLine("                       Pass the date and time desired, or leave blank");
            Console.WriteLine("                       to use the current date and time of this computer.");
            Console.WriteLine();
            Console.WriteLine("  deletetable <tXXX> - Deletes a specific table from the terminal.");
            Console.WriteLine();
            Console.WriteLine("  deletealltables    - Deletes all tables from the terminal.");
            Console.WriteLine();
            Console.WriteLine("  fixmemcrash        - Erases all of the terminal's memory, which will");
            Console.WriteLine("                       put a terminal that is \"Mem Crashed\" back into");
            Console.WriteLine("                       \"No Prog\" mode.");
            Console.WriteLine();
            Console.WriteLine("  upload <file1> [file2] [file3] [...]");
            Console.WriteLine("                     - Uploads one or more RDY files to the terminal.");
            Console.WriteLine("                       Supports wildcards and dirXXX files also.");
            Console.WriteLine();
            Console.WriteLine("  getdata            - Gets transaction data from the terminal.");
            Console.WriteLine();
            Console.WriteLine("  resetdata          - Resets all transactions in the terminal's memory,");
            Console.WriteLine("                       so that they can be sent again.");
            Console.WriteLine();
            Console.WriteLine("  cleardata          - Clears all transactions from the terminal's memory");
            Console.WriteLine("                       that have already been acknowledged.");
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine("  listen [port] [ack]");
            Console.WriteLine("                     - Acts as a server, listening for incoming data.");
            Console.WriteLine("                       Listens on port 3734 if not specified.");
            Console.WriteLine("                       Optionally pass \"ack\" to acknowledge receipt.");
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

        private static void Listen(bool acknowledge)
        {
            Console.WriteLine();
            Console.WriteLine("Listening on port {0}.  Press Ctrl-C to terminate.", _port);
            Console.WriteLine();

            // todo: doesn't work on command prompt, but works in VS????

            using (SynelServer.Listen(_port, notification =>
                {
                    if (notification.Data == null)
                        return;

                    Console.Write("{0:yyyy-MM-dd HH:mm:sszzz} [{1}|{2}]  {3}",
                                  DateTimeOffset.Now,
                                  notification.RemoteEndPoint.Address,
                                  notification.TerminalId,
                                  notification.Data);

                    if (acknowledge)
                    {
                        notification.Acknowledege();
                        Console.Write("  [ACKNOWLEDGED]");
                    }

                    Console.WriteLine();
                }))
            {
                Thread.Sleep(-1);
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

        private static void GetFingerprintInfo()
        {
            using (var client = SynelClient.Connect(_host, _port, _terminalId, Timeout))
            using (var p = client.Terminal.Programming())
            {
                var status = p.Fingerprint.GetUnitStatus();
                Console.WriteLine();
                Console.WriteLine("Comparison Mode:   {0}", status.ComparisonMode);
                Console.WriteLine("Kernel Version:    {0}", new object[] { status.KernelVersion });
                Console.WriteLine("Loaded Templates:  {0}", status.LoadedTemplates);
                Console.WriteLine("Maximum Templates: {0}", status.MaximumTemplates);
                Console.WriteLine("FPU Mode:          {0}", status.FingerprintUnitMode);
                Console.WriteLine("Global Threshold:  {0}", status.GlobalThreshold);
                Console.WriteLine("Enroll Mode:       {0}", status.EnrollMode);
                Console.WriteLine();
            }
        }

        private static void SetTime(string[] commandArgs)
        {
            DateTime dt;
            if (commandArgs.Length == 0)
            {
                dt = DateTime.Now;
            }
            else if (commandArgs.Length != 2 ||
                     !DateTime.TryParseExact(string.Join(" ", commandArgs), "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
            {
                Console.WriteLine("Leave blank to set the current time from this computer,");
                Console.WriteLine("or pass in YYYY-MM-DD HH:MM:SS format.");
                return;
            }

            using (var client = SynelClient.Connect(_host, _port, _terminalId, Timeout))
            {
                client.Terminal.SetTerminalClock(dt);
                Console.WriteLine("Set the terminal clock to {0:g}", dt);
            }
        }

        private static void DeleteTable(string tableName)
        {
            if (tableName.Length != 4)
            {
                Console.WriteLine();
                Console.WriteLine("Invalid table name.");
                return;
            }

            var type = tableName[0];
            int id;
            if (!int.TryParse(tableName.Substring(1), out id))
            {
                Console.WriteLine();
                Console.WriteLine("Invalid table name.");
                return;
            }

            using (var client = SynelClient.Connect(_host, _port, _terminalId, Timeout))
            using (var p = client.Terminal.Programming())
            {
                p.DeleteTable(type, id);
                Console.WriteLine();
                Console.WriteLine("Sent command to delete table {0} from the terminal.", tableName);
            }
        }

        private static void DeleteAllTables()
        {
            using (var client = SynelClient.Connect(_host, _port, _terminalId, Timeout))
            using (var p = client.Terminal.Programming())
            {
                p.DeleteAllTables();
                Console.WriteLine();
                Console.WriteLine("Sent command to delete all tables from the terminal.");
            }
        }

        private static void FixMemCrash()
        {
            using (var client = SynelClient.Connect(_host, _port, _terminalId, Timeout))
            using (var p = client.Terminal.Programming())
            {
                p.FixMemCrash();
                Console.WriteLine();
                Console.WriteLine("Sent command to fix terminal memcrash.");
            }
        }

        private static void UploadFile(IEnumerable<string> paths)
        {
            using (var client = SynelClient.Connect(_host, _port, _terminalId, Timeout))
            using (var p = client.Terminal.Programming())
            {
                Console.CursorVisible = false;

                string filename = null;

                var uniqueFiles = new List<string>();

                if (!_verbose)
                {
                    Console.WriteLine();

                    p.ProgressChanged += (sender, args) =>
                        {
                            if (filename != args.Filename)
                            {
                                filename = args.Filename;
                                Console.Write("Uploading {0} ", filename.PadRight(12, ' '));
                                uniqueFiles.Add(filename);
                            }

                            const int barSize = 30;

                            DrawProgressBar(args.CurrentBlock, args.TotalBlocks, barSize);

                            if (args.CurrentBlock == args.TotalBlocks)
                            {
                                Console.CursorLeft += barSize + 1;
                                Console.WriteLine("Complete!");
                            }
                        };
                }

                foreach (var path in paths)
                {
                    var directory = Path.GetDirectoryName(path);
                    var pattern = Path.GetFileName(path);

                    if (directory == null || pattern == null)
                        continue;

                    // get files, sorting directory files first.
                    var fullDir = Path.GetFullPath(directory);
                    var files = Directory.GetFiles(fullDir, pattern)
                                         .OrderBy(x => (x.StartsWith("dir", StringComparison.OrdinalIgnoreCase) ? "0" : "1") + x);

                    foreach (var file in files)
                    {
                        // skip files we've already uploaded
                        var thisFilename = Path.GetFileName(file);
                        if (thisFilename == null || uniqueFiles.Contains(thisFilename, StringComparer.OrdinalIgnoreCase))
                            continue;
                        
                        var thisPath = Path.Combine(fullDir, file);
                        p.UploadTableFromFile(thisPath);
                    }
                }


                if (!_verbose)
                {
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
            var pct = string.Format(" {0:N2}%", percent * 100);
            Console.Write(pct.PadRight(12));
            Console.Write("[{0}/{1}]", current, total);
            Console.CursorLeft = left;
        }

        private static void GetData()
        {
            using (var client = SynelClient.Connect(_host, _port, _terminalId, Timeout))
            {
                Console.WriteLine();
                string item;
                bool gotData = false;
                while ((item = client.Terminal.GetDataAndAcknowledge()) != null)
                {
                    Console.WriteLine(item);
                    gotData = true;
                }
                if (!gotData)
                {
                    Console.WriteLine("The terminal has no transaction data to send.");
                }
                
            }
        }

        private static void ResetData()
        {
            using (var client = SynelClient.Connect(_host, _port, _terminalId, Timeout))
            {
                client.Terminal.ResetBuffer();
                Console.WriteLine();
                Console.WriteLine("Data has been reset.");
            }
        }

        private static void ClearData()
        {
            using (var client = SynelClient.Connect(_host, _port, _terminalId, Timeout))
            {
                client.Terminal.ClearBuffer();
                Console.WriteLine();
                Console.WriteLine("Data has been cleared.");
            }
        }
    }
}
