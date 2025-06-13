using BeXCool.PipeMessages.Common;
using Newtonsoft.Json;
using System.IO.Pipes;
using Timer = System.Timers.Timer;

namespace BeXCool.PipeMessages
{
    public class PipeMessageServer<T> : IDisposable
    {
        /// <summary>
        /// Name of the named pipe used for communication.
        /// </summary>
        public string PipeName { get; private set; } = "NewPipeMessageServer";
        /// <summary>
        /// Indicates whether the server checks for messages automatically or requires manual checking.
        /// </summary>
        public bool ManualCheck { get; private set; } = false;
        /// <summary>
        /// Event that is raised when a message is received from the pipe server.
        /// </summary>
        public event PipeMessageHandler<T>? MessageReceived;

        /// <summary>
        /// The named pipe server stream used for communication with clients.
        /// </summary>
        private NamedPipeServerStream? _pipeServer = null;
        /// <summary>
        /// The stream reader and writer for reading and writing messages to the pipe client.
        /// </summary>
        private StreamReader? _pipeReader = null;
        /// <summary>
        /// The stream writer for writing messages to the pipe client.
        /// </summary>
        private StreamWriter? _pipeWriter = null;
        /// <summary>
        /// Timer that checks for messages at regular intervals if ManualCheck is false.
        /// </summary>
        private Timer? _pipeTimer = null;
        /// <summary>
        /// Queue for storing messages that are sent when the pipe server is not connected.
        /// </summary>
        private Stack<T> _messageQueue = new();

        /// <summary>
        /// Initializes a new instance of the PipeMessageServer class with the specified pipe name.
        /// </summary>
        /// <param name="pipeName">Name of the pipe.</param>
        public PipeMessageServer(string pipeName)
        {
            PipeName = pipeName;
        }

        /// <summary>
        /// Initializes a new instance of the PipeMessageServer class with the specified pipe name and manual check option.
        /// </summary>
        /// <param name="pipeName">Name of the pipe.</param>
        /// <param name="manualCheck">If true, the timer for automatic checking is not started.</param>
        public PipeMessageServer(string pipeName, bool manualCheck)
        {
            PipeName = pipeName;
            ManualCheck = manualCheck;
        }

        /// <summary>
        /// Starts the pipe server and begins checking for messages at regular intervals.
        /// </summary>
        public async Task StartAsync()
        {
            _pipeServer = new(PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Message, PipeOptions.Asynchronous);
            await _pipeServer.WaitForConnectionAsync();

            _pipeReader = new StreamReader(_pipeServer);
            _pipeWriter = new StreamWriter(_pipeServer) { AutoFlush = true };

            _ = Task.Run(MessageLoopAsync);

            return;
            _pipeTimer = new(100);
            _pipeTimer.Elapsed += _timer_Elapsed;
            _pipeTimer.Start();
        }

        /// <summary>
        /// Forces a check for messages from the pipe client. This is useful when ManualCheck is set to true.
        /// </summary>
        public async void ForceCheck()
        {
            if (ManualCheck)
            {
                await CheckForMessagesAsync();
            }
        }

        /// <summary>
        /// Sends a message to the pipe client. If the client is not connected, the message is queued for later sending.
        /// </summary>
        /// <param name="message">The message to send.</param>
        /// <returns>True if message is sent or queued for later sending, otherwise false.</returns>
        public async Task<bool> SendMessageAsync(T message)
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

            await WriteMessageToStreamAsync(message);

            return true;
        }

        /// <summary>
        /// Disposes the pipe server and timer, clearing the message queue.
        /// </summary>
        public void Dispose()
        {
            _pipeTimer?.Stop();
            _pipeTimer?.Dispose();
            _pipeServer?.Dispose();
            _pipeServer = null;
            _pipeTimer = null;
            _messageQueue.Clear();
        }

        private async Task MessageLoopAsync()
        {
            while (_pipeServer != null)
            {
                if (_messageQueue.Count > 0 && _pipeServer.IsConnected)
                {
                    while (_messageQueue.Count > 0)
                    {
                        var message = _messageQueue.Pop();
                        await WriteMessageToStreamAsync(message);
                    }
                }

                await CheckForMessagesAsync();
            }
        }

        /// <summary>
        /// Handles the timer elapsed event to check for messages and send queued messages if the pipe client is connected.
        /// </summary>
        private void _timer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            if (_pipeServer == null)
            {
                _pipeTimer?.Stop();
                return;
            }

            if (_messageQueue.Count > 0 && _pipeServer.IsConnected)
            {
                while (_messageQueue.Count > 0)
                {
                    var message = _messageQueue.Pop();
                    WriteMessageToStreamAsync(message);
                }
            }

            CheckForMessagesAsync();
        }

        /// <summary>
        /// Checks for incoming messages from the pipe client and raises the MessageReceived event for each message.
        /// </summary>
        private async Task CheckForMessagesAsync()
        {
            if (_pipeServer == null || _pipeReader == null || !_pipeServer.IsConnected)
            {
                return;
            }

            while (_pipeServer != null && _pipeServer.IsConnected)
            {
                if (_pipeReader.Peek() >= 0)
                {
                    var line = await _pipeReader.ReadLineAsync();
                    if (line != null)
                    {
                        var message = JsonConvert.DeserializeObject<T>(line);
                        if (message != null)
                        {
                            MessageReceived?.Invoke(this, new PipeMessageEventArgs<T>(message));
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Writes a message to the pipe stream in JSON format.
        /// </summary>
        /// <param name="message">The message to send.</param>
        private async Task WriteMessageToStreamAsync(T message)
        {
            if (_pipeServer == null || _pipeWriter == null)
            {
                return;
            }

            await _pipeWriter.WriteLineAsync(JsonConvert.SerializeObject(message));
        }
    }
}
