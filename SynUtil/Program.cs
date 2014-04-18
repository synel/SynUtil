using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using SynUtil.Properties;
using Syndll2;
using Syndll2.Data;

namespace SynUtil
{
    internal class Program
    {
        private static string _host;
        private static int _port = 3734;
        private static int _terminalId;
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);
        private static bool _verbose;
        private static bool _force;
        private static string _outputFile;
        private static string _outputHeader;

        private static void Main(string[] args)
        {
            Console.WriteLine("SynUtil " + GetVersion());

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

                // force flag
                _force = args.Contains("-f", StringComparer.OrdinalIgnoreCase);

                // output flag
                for (int i = 0; i < args.Length-1; i++)
                {
                    if (args[i].Equals("-o", StringComparison.OrdinalIgnoreCase))
                    {
                        _outputFile = args[i + 1];
                        var l = args.ToList();
                        l.RemoveAt(i);
                        l.RemoveAt(i);
                        args = l.ToArray();
                        break;
                    }
                }

                // output header
                for (int i = 0; i < args.Length - 1; i++)
                {
                    if (args[i].Equals("-h", StringComparison.OrdinalIgnoreCase))
                    {
                        _outputHeader = args[i + 1];
                        var l = args.ToList();
                        l.RemoveAt(i);
                        l.RemoveAt(i);
                        args = l.ToArray();
                        break;
                    }
                }

                // command
                var command = args.Skip(1).First(x => !x.StartsWith("-"));
                var commandIndex = Array.IndexOf(args, command);
                var commandArgs = args.Skip(commandIndex + 1).Where(x => !x.StartsWith("-")).ToArray();
                var commandArg = commandArgs.FirstOrDefault();

                switch (command.ToLowerInvariant())
                {
                    /* Basic Commands */

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
                        SetTime(commandArgs);
                        break;

                    /* Programming Commands */

                    case "deletetable":
                        if (string.IsNullOrEmpty(commandArg))
                            Console.WriteLine("Pass the table type and id to delete.  Example:  deletetable x001");
                        else
                            DeleteTable(commandArg);
                        break;
                    case "deletealltables":
                        DeleteAllTables();
                        break;
                    case "fixmemcrash":
                        FixMemCrash();
                        break;
                    case "upload":
                        if (string.IsNullOrEmpty(commandArg))
                            Console.WriteLine("Pass the path(s) of the file(s) to upload.");
                        else
                            UploadFile(commandArgs);
                        break;
                    case "halt":
                        HaltTerminal();
                        break;
                    case "run":
                        RunTerminal();
                        break;

                    /* Transaction Data Commands */

                    case "getdata":
                        GetData();
                        break;
                    case "resetdata":
                        ResetData();
                        break;
                    case "cleardata":
                        ClearData();
                        break;

                    /* Fingerprint Commands */

                    case "getfingerinfo":
                        GetFingerprintInfo();
                        break;
                    case "setfingermode":
                        SetFingerMode(commandArg);
                        break;
                    case "setfingerthreshold":
                        SetFingerThreshold(commandArg);
                        break;
                    case "setfingerenrollment":
                        SetFingerEnrollment(commandArg);
                        break;
                    case "listfinger":
                    case "listfingers":
                        ListFingerprintTemplates();
                        break;
                    case "getfinger":
                    case "getfingers":
                        GetFingerprintTemplates(commandArg);
                        break;
                    case "sendfinger":
                    case "sendfingers":
                        SendFingerprintTemplates(commandArgs);
                        break;
                    case "deletefinger":
                    case "deletefingers":
                        DeleteFingerprintTemplates(commandArg);
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
            var version = GetVersion();
            var helpText = Resources.Help.Replace("{version}", version);
            Console.Write(helpText);
        }

        private static string GetVersion()
        {
            return "v" + typeof(Program).Assembly.GetName().Version.ToString(3);
        }

        private static void GetStatus()
        {
            using (var client = SynelClient.Connect(_host, _port, _terminalId, Timeout))
            {
                var info = client.Terminal.GetTerminalStatus();
                ClearOutputFile();
                OutputLine("Hardware Model:      {0}", info.HardwareModel);
                OutputLine("Hardware Revision:   {0}", info.HardwareRevision);
                OutputLine("Firmware Version:    {0}", info.FirmwareVersion);
                OutputLine("Terminal Type:       {0}", info.TerminalType);
                OutputLine("Current Time:        {0:g}", info.Timestamp);
                OutputLine("Active Function:     {0}", info.ActiveFunction);
                OutputLine("Powered On:          {0}", info.PoweredOn);
                OutputLine("Buffers Full:        {0}", info.BuffersFull);
                OutputLine("Buffers Faulty:      {0}", info.BuffersFaulty);
                OutputLine("Buffers Transmitted: {0}", info.BuffersTransmitted);
                OutputLine("Buffers Empty:       {0}", info.BuffersEmpty);
                OutputLine("Memory Used:         {0} bytes", info.MemoryUsed);
                OutputLine("Polling Interval:    {0} seconds", info.PollingInterval.TotalSeconds);
                OutputLine("Transport Type:      {0}", info.TransportType.ToString().ToUpperInvariant());
                OutputLine("FPU Mode:            {0}", info.FingerprintUnitMode);
                OutputLine("User Defined Field:  {0}", info.UserDefinedField);
            }
        }

        private static void Listen(bool acknowledge)
        {
            Console.WriteLine("Listening on port {0}.  Press Ctrl-C to terminate.", _port);

            using (SynelServer.Listen(_port, notification =>
                {
                    if (notification.Data == null)
                        return;

                    Output("{0:yyyy-MM-dd HH:mm:sszzz} [{1}|{2}]  {3}",
                                  DateTimeOffset.Now,
                                  notification.Client.RemoteEndPoint.Address,
                                  notification.TerminalId,
                                  notification.Data);

                    if (acknowledge)
                    {
                        if (notification.Type == NotificationType.Data)
                            notification.Acknowledege();

                        if (notification.Type == NotificationType.Query)
                            notification.Reply(true, 0, "OK", TextAlignment.Center);

                        Output("  [ACKNOWLEDGED]");

                    }

                    OutputLine();
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
                ClearOutputFile();
                OutputLine("Terminal ID:         {0}", info.TerminalId);
                OutputLine("Terminal Type:       {0}", info.TerminalType);
                OutputLine("Firmware Version:    {0} ({1:d})", info.FirmwareVersion, info.FirmwareDate);
                OutputLine("Keyboard Type:       {0}", info.KeyboardType);
                OutputLine("Display Type:        {0}", info.DisplayType);
                OutputLine("FPU Type:            {0}", info.FingerprintUnitType);
                OutputLine("FPU Mode:            {0}", info.FingerprintUnitMode);
                OutputLine("Serial Port Info:    {0} {1}", info.HostSerialBaudRate, info.HostSerialParameters.ToUpperInvariant());
                OutputLine("User Defined Field:  {0}", info.UserDefinedField);
            }
        }

        private static void GetNetworkInfo()
        {
            using (var client = SynelClient.Connect(_host, _port, _terminalId, Timeout))
            {
                var info = client.Terminal.GetNetworkConfiguration();
                ClearOutputFile();
                OutputLine("Network Card:        {0} (ver {1})", info.NetworkCardType, info.NetworkCardFirmwareVersion);
                OutputLine("Transport Type:      {0}", info.TransportType.ToString().ToUpperInvariant());
                OutputLine("MAC Address:         {0}", info.TerminalMACAddress);
                OutputLine("IP Address/Port:     {0}:{1}", info.TerminalIPAddress, info.TerminalPort);
                OutputLine("Remote Address/Port: {0}:{1}", info.RemoteIPAddress, info.RemotePort);
                OutputLine("Subnet Mask:         {0}", info.SubnetMask);
                OutputLine("Gateway Address:     {0}", info.GatewayIPAddress);
                OutputLine("Disconnect Time:     {0} seconds", info.DisconnectTime.TotalSeconds);
                OutputLine("Polling Interval:    {0} seconds", info.PollingInterval.TotalSeconds);
                OutputLine("Polling Enabled:     {0}", info.EnablePolling);
                OutputLine("DHCP Enabled:        {0}", info.EnableDHCP);
                OutputLine("MAC Sending Enabled: {0}", info.EnableSendMAC);
            }
        }

        private static void GetFingerprintInfo()
        {
            using (var client = SynelClient.Connect(_host, _port, _terminalId, Timeout))
            using (var p = client.Terminal.Programming())
            {
                var status = p.Fingerprint.GetUnitStatus();
                ClearOutputFile();
                OutputLine("Comparison Mode:   {0}", status.ComparisonMode);
                OutputLine("Kernel Version:    {0}", new object[] { status.KernelVersion });
                OutputLine("Loaded Templates:  {0}", status.LoadedTemplates);
                OutputLine("Maximum Templates: {0}", status.MaximumTemplates);
                OutputLine("FPU Mode:          {0}", status.FingerprintUnitMode);
                OutputLine("Global Threshold:  {0}", status.GlobalThreshold);
                OutputLine("Enroll Mode:       {0}", status.EnrollMode);
            }
        }

        private static void SetFingerMode(string setting)
        {
            FingerprintUnitModes mode;
            if (!Enum.TryParse(setting, true, out mode))
            {
                Console.WriteLine("Invalid mode.  Pass either \"Master\" or \"Slave\"");
                return;
            }

            using (var client = SynelClient.Connect(_host, _port, _terminalId, Timeout))
            using (var p = client.Terminal.Programming())
            {
                p.Fingerprint.SetUnitMode(mode);
                Console.WriteLine("Set the fingerprint unit mode to {0}.", mode);
            }
        }

        private static void SetFingerThreshold(string setting)
        {
            FingerprintThreshold threshold;
            if (!Enum.TryParse(setting, true, out threshold))
            {
                Console.WriteLine("Invalid threshold.  Pass one of \"VeryHigh\", \"High\", \"Medium\", \"Low\" or \"VeryLow\"");
                return;
            }

            using (var client = SynelClient.Connect(_host, _port, _terminalId, Timeout))
            using (var p = client.Terminal.Programming())
            {
                p.Fingerprint.SetThreshold(threshold);
                Console.WriteLine("Set the fingerprint global threshold to {0}.", threshold);
            }
        }

        private static void SetFingerEnrollment(string setting)
        {
            FingerprintEnrollModes mode;
            if (!Enum.TryParse(setting, true, out mode))
            {
                Console.WriteLine("Invalid mode.  Pass one of \"Once\", \"Twice\", or \"Dual\"");
                return;
            }

            using (var client = SynelClient.Connect(_host, _port, _terminalId, Timeout))
            using (var p = client.Terminal.Programming())
            {
                p.Fingerprint.SetEnrollMode(mode);
                Console.WriteLine("Set the fingerprint enroll mode to {0}.", mode);
            }
        }

        private static void ListFingerprintTemplates()
        {
            throw new NotImplementedException();
        }

        private static void GetFingerprintTemplates(string arg)
        {
            throw new NotImplementedException();
        }

        private static void SendFingerprintTemplates(string[] args)
        {
            throw new NotImplementedException();
        }

        private static void DeleteFingerprintTemplates(string arg)
        {
            throw new NotImplementedException();
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

        private static void FixMemCrash()
        {
            using (var client = SynelClient.Connect(_host, _port, _terminalId, Timeout))
            using (var p = client.Terminal.Programming())
            {
                p.FixMemCrash();
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
                    if (directory == null)
                        continue;

                    var pattern = Path.GetFileName(path);

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
                        p.UploadTableFromFile(thisPath, force: _force);
                    }
                }


                if (!_verbose)
                {
                    Console.CursorVisible = true;
                }
            }
        }

        private static void HaltTerminal()
        {
            using (var client = SynelClient.Connect(_host, _port, _terminalId, Timeout))
            {
                client.Terminal.Halt();
            }
        }

        private static void RunTerminal()
        {
            using (var client = SynelClient.Connect(_host, _port, _terminalId, Timeout))
            {
                client.Terminal.Run();
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
                string item;
                bool gotData = false;
                while ((item = client.Terminal.GetDataAndAcknowledge()) != null)
                {
                    OutputLine(item);
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
                Console.WriteLine("Data has been reset.");
            }
        }

        private static void ClearData()
        {
            using (var client = SynelClient.Connect(_host, _port, _terminalId, Timeout))
            {
                client.Terminal.ClearBuffer();
                Console.WriteLine("Data has been cleared.");
            }
        }

        private static void OutputLine()
        {
            Output(Environment.NewLine);
        }

        private static void OutputLine(string s, params object[] args)
        {
            Output(s + Environment.NewLine, args);
        }

        private static void Output(string s, params object[] args)
        {
            if (_outputHeader != null)
            {
                var header = _outputHeader;
                _outputHeader = null;
                OutputLine(header);
            }

            var output = string.Format(s, args);

            if (_outputFile != null)
            {
                File.AppendAllText(_outputFile, output);
            }
            else
            {
                Console.Write(output);
            }
        }

        private static void ClearOutputFile()
        {
            if (_outputFile != null && File.Exists(_outputFile))
                File.Delete(_outputFile);
        }
    }
}
