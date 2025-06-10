using BeXCool.PipeMessages.Common;
using Newtonsoft.Json;
using System.IO.Pipes;
using Timer = System.Timers.Timer;

namespace BeXCool.PipeMessages
{
    public class PipeMessageClient<T> : IDisposable
    {
        /// <summary>
        /// Name of the named pipe used for communication.
        /// </summary>
        public string PipeName { get; private set; } = "NewPipeMessageServer";
        /// <summary>
        /// Indicates whether the client checks for messages automatically or requires manual checking.
        /// </summary>
        public bool ManualCheck { get; private set; } = false;
        /// <summary>
        /// Event that is raised when a message is received from the pipe client.
        /// </summary>
        public event PipeMessageHandler<T>? MessageReceived;
        /// <summary>
        /// The named pipe server stream used for communication with clients.
        /// </summary>
        private NamedPipeClientStream? _pipeClient = null;
        /// <summary>
        /// The stream reader and writer for reading and writing messages to the pipe server.
        /// </summary>
        private StreamReader? _pipeReader = null;
        /// <summary>
        /// The stream writer for writing messages to the pipe server.
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
        /// Initializes a new instance of the PipeMessageClient class with the specified pipe name.
        /// </summary>
        /// <param name="pipeName">Name of the pipe.</param>
        public PipeMessageClient(string pipeName)
        {
            PipeName = pipeName;
        }

        /// <summary>
        /// Initializes a new instance of the PipeMessageClient class with the specified pipe name and manual check option.
        /// </summary>
        /// <param name="pipeName">Name of the pipe.</param>
        /// <param name="manualCheck">If true, the timer for automatic checking is not started.</param>
        public PipeMessageClient(string pipeName, bool manualCheck)
        {
            PipeName = pipeName;
            ManualCheck = manualCheck;
        }

        /// <summary>
        /// Starts the pipe server and begins checking for messages at regular intervals.
        /// </summary>
        public void Start()
        {
            _pipeClient = new(".", PipeName, PipeDirection.InOut);
            _pipeClient.Connect();

            _pipeReader = new StreamReader(_pipeClient);
            _pipeWriter = new StreamWriter(_pipeClient) { AutoFlush = true };

            _pipeTimer = new(100);
            _pipeTimer.Elapsed += _timer_Elapsed;
            _pipeTimer.Start();
        }

        /// <summary>
        /// Forces a check for messages from the pipe server. This is useful when ManualCheck is set to true.
        /// </summary>
        public void ForceCheck()
        {
            if (ManualCheck)
            {
                CheckForMessages();
            }
        }

        /// <summary>
        /// Sends a message to the pipe server. If the server is not connected, the message is queued for later sending.
        /// </summary>
        /// <param name="message">The message to send.</param>
        /// <returns>True if message is sent or queued for later sending, otherwise false.</returns>
        public bool SendMessage(T message)
        {
            if (_pipeClient == null)
            {
                return false;
            }

            if (!_pipeClient.IsConnected)
            {
                _messageQueue.Push(message);
                return true;
            }

            WriteMessageToStream(message);

            return true;
        }

        /// <summary>
        /// Disposes the pipe server and timer, clearing the message queue.
        /// </summary>
        public void Dispose()
        {
            _pipeTimer?.Stop();
            _pipeTimer?.Dispose();
            _pipeClient?.Dispose();
            _pipeClient = null;
            _pipeTimer = null;
            _messageQueue.Clear();
        }

        /// <summary>
        /// Handles the timer elapsed event to check for messages and send queued messages if the pipe server is connected.
        /// </summary>
        private void _timer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            if (_pipeClient == null)
            {
                _pipeTimer?.Stop();
                return;
            }

            if (_messageQueue.Count > 0 && _pipeClient.IsConnected)
            {
                while (_messageQueue.Count > 0)
                {
                    var message = _messageQueue.Pop();
                    WriteMessageToStream(message);
                }
            }

            CheckForMessages();
        }

        /// <summary>
        /// Checks for incoming messages from the pipe server and raises the MessageReceived event for each message.
        /// </summary>
        private void CheckForMessages()
        {
            if (_pipeClient == null || _pipeReader == null || !_pipeClient.IsConnected)
            {
                return;
            }

            while (_pipeClient.IsConnected && _pipeReader.Peek() >= 0)
            {
                var line = _pipeReader.ReadLine();
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

        /// <summary>
        /// Writes a message to the pipe server stream in JSON format.
        /// </summary>
        /// <param name="message">The message to send.</param>
        private void WriteMessageToStream(T message)
        {
            if (_pipeClient == null || _pipeWriter == null)
            {
                return;
            }

            _pipeWriter.WriteLine(JsonConvert.SerializeObject(message));
            _pipeWriter.Flush();
        }
    }
}
