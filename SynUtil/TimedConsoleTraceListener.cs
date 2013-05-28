using System.Diagnostics;

namespace SynUtil
{
    internal class TimedConsoleTraceListener : ConsoleTraceListener
    {
        private readonly Stopwatch _stopwatch = new Stopwatch();

        public override void WriteLine(string message)
        {
            if (!_stopwatch.IsRunning)
                _stopwatch.Start();

            base.WriteLine(string.Format("{0:00.000} - {1}", _stopwatch.Elapsed.TotalSeconds, message));
        }
    }
}
