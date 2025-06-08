using BeXCool.PipeMessages.Common;
using Newtonsoft.Json;
using System.IO.Pipes;
using Timer = System.Timers.Timer;

namespace BeXCool.PipeMessages
{
    public class PipeMessageServer<T> : IDisposable
    {
        public string PipeName { get; private set; } = "NewPipeMessageServer";
        public bool ManualCheck { get; private set; } = false;
        public event PipeMessageEvent<T>? MessageReceived;

        private NamedPipeServerStream? _pipeServer = null;
        private Timer? _pipeTimer = null;
        private Stack<T> _messageQueue = new();

        public PipeMessageServer(string pipeName)
        {
            PipeName = pipeName;
        }

        public PipeMessageServer(string pipeName, bool manualCheck)
        {
            PipeName = pipeName;
            ManualCheck = manualCheck;
        }

        public void Start()
        {
            _pipeServer = new(PipeName, PipeDirection.InOut);

            _pipeTimer = new(100);
            _pipeTimer.Elapsed += _timer_Elapsed;
            _pipeTimer.Start();
        }

        private void _timer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            if (_pipeServer == null)
            {
                _pipeTimer?.Stop();
                return;
            }
        }

        public bool SendMessage(T message)
        {
            if (_pipeServer == null)
            {
                return false;
            }

            if (!_pipeServer.IsConnected)
            {
                _messageQueue.Push(message);
                return true;
            }

            using (var writer = new StreamWriter(_pipeServer))
            {
                writer.WriteLine(JsonConvert.SerializeObject(message));
                writer.Flush();
            }

            return true;
        }

        public void Dispose()
        {
            _pipeTimer?.Stop();
            _pipeTimer?.Dispose();
            _pipeServer?.Dispose();
            _pipeServer = null;
            _pipeTimer = null;
            _messageQueue.Clear();
        }
    }
}
