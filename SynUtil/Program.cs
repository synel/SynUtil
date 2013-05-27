using System;
using System.Linq;
using Syndll2;

namespace SynUtil
{
    internal class Program
    {
        private static string _host;
        private static int _port = 3734;
        private static int _terminalId;

        private static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                DisplayHelpText();
                return;
            }

            // TODO - better command input checking

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
            var command = (args[1] == tidArg ? args[2] : args[1]).ToLowerInvariant();
            switch (command)
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
                default:
                    {
                        Console.WriteLine("Unsupported command.");
                        break;
                    }
            }
        }

        private static void DisplayHelpText()
        {
            Console.WriteLine();
            Console.WriteLine("Command-line utility for SY7xx terminals, using Syndll2.");
            Console.WriteLine("Usage:");
            Console.WriteLine();
            Console.WriteLine("  SynUtil <host>[:port] [-t<terminal id>] <command> [arguments]");
            Console.WriteLine("");
            Console.WriteLine("Parameters:");
            Console.WriteLine("  host - The IP or DNS name of the terminal.");
            Console.WriteLine("  port - The TCP port to connect to.  Defaults to 3734.");
            Console.WriteLine("  terminal id - The terminal id to use.  Defaults to 0.");
            Console.WriteLine("  command - The command to execute.");
            Console.WriteLine("  arguments - Any arguments required for the specific command.");
            Console.WriteLine();
            Console.WriteLine("Commands:");
            Console.WriteLine("  getstatus        - Displays the terminal's status information.");
            Console.WriteLine("  gethardwareinfo  - Displays the terminal's hardware information.");
            Console.WriteLine("  getnetworkinfo   - Displays the terminal's network information.");
            Console.WriteLine("  settime          - Sets the terminal's date and time to the");
            Console.WriteLine("                     current date and time of this computer.");
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
            using (var client = SynelClient.Connect(_host, _port, _terminalId))
            {
                var info = client.Terminal.GetTerminalStatus();
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
            }
        }

        private static void GetHardwareInfo()
        {
            using (var client = SynelClient.Connect(_host, _port, _terminalId))
            {
                var info = client.Terminal.GetHardwareConfiguration();
                Console.WriteLine("Terminal ID:         {0}", info.TerminalId);
                Console.WriteLine("Terminal Type:       {0}", info.TerminalType);
                Console.WriteLine("Firmware Version:    {0} ({1:d})", info.FirmwareVersion, info.FirmwareDate);
                Console.WriteLine("Keyboard Type:       {0}", info.KeyboardType);
                Console.WriteLine("Display Type:        {0}", info.DisplayType);
                Console.WriteLine("FPU Type:            {0}", info.FingerprintUnitType);
                Console.WriteLine("FPU Mode:            {0}", info.FingerprintUnitMode);
                Console.WriteLine("Serial Port Info:    {0} {1}", info.HostSerialBaudRate, info.HostSerialParameters.ToUpperInvariant());
                Console.WriteLine("User Defined Field:  {0}", info.UserDefinedField);
            }
        }

        private static void GetNetworkInfo()
        {
            using (var client = SynelClient.Connect(_host, _port, _terminalId))
            {
                var info = client.Terminal.GetNetworkConfiguration();
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
            }
        }

        private static void SetTime()
        {
            using (var client = SynelClient.Connect(_host, _port, _terminalId))
            {
                var now = DateTime.Now;
                client.Terminal.SetTerminalClock(now);
                Console.WriteLine("Set the terminal clock to {0:g}", now);
            }
        }
    }
}
