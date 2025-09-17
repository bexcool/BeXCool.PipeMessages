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
        public async Task StartAsync()
        {
            _pipeClient = new(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await _pipeClient.ConnectAsync();

            _pipeReader = new StreamReader(_pipeClient);
            _pipeWriter = new StreamWriter(_pipeClient) { AutoFlush = true };

            _ = Task.Run(MessageLoopAsync);

            if (!ManualCheck)
            {
                _pipeTimer = new(100);
                _pipeTimer.Elapsed += _timer_Elapsed;
                _pipeTimer.Start();
            }
        }

        /// <summary>
        /// Forces a check for messages from the pipe server. This is useful when ManualCheck is set to true.
        /// </summary>
        public async void ForceCheck()
        {
            if (ManualCheck)
            {
                await CheckForMessagesAsync();
            }
        }

        /// <summary>
        /// Sends a message to the pipe server. If the server is not connected, the message is queued for later sending.
        /// </summary>
        /// <param name="message">The message to send.</param>
        /// <returns>True if message is sent or queued for later sending, otherwise false.</returns>
        public async Task<bool> SendMessageAsync(T message)
        {
            if (_pipeClient == null)
            {
                return false;
            }

            // If not connected, try to reconnect first
            if (!_pipeClient.IsConnected)
            {
                await TryReconnectAsync();
            }

            // If still not connected after reconnection attempt, queue the message
            if (_pipeClient == null || !_pipeClient.IsConnected)
            {
                _messageQueue.Push(message);
                return true;
            }

            await WriteMessageToStreamAsync(message);

            return true;
        }

        /// <summary>
        /// Disposes the pipe client and timer, clearing the message queue.
        /// </summary>
        public void Dispose()
        {
            _pipeTimer?.Stop();
            _pipeTimer?.Dispose();
            
            try
            {
                _pipeReader?.Dispose();
                _pipeWriter?.Dispose();
                _pipeClient?.Dispose();
            }
            catch
            {
                // Ignore exceptions during disposal
            }
            
            _pipeClient = null;
            _pipeReader = null;
            _pipeWriter = null;
            _pipeTimer = null;
            _messageQueue.Clear();
        }

        private async Task MessageLoopAsync()
        {
            while (_pipeClient != null)
            {
                try
                {
                    // Try to reconnect if not connected
                    if (_pipeClient != null && !_pipeClient.IsConnected)
                    {
                        await TryReconnectAsync();
                    }

                    // Process queued messages if connected
                    if (_messageQueue.Count > 0 && _pipeClient != null && _pipeClient.IsConnected)
                    {
                        while (_messageQueue.Count > 0)
                        {
                            var message = _messageQueue.Pop();
                            await WriteMessageToStreamAsync(message);
                            
                            // Check if connection was lost during write
                            if (_pipeClient == null || !_pipeClient.IsConnected)
                            {
                                // Put the message back if it wasn't sent
                                _messageQueue.Push(message);
                                break;
                            }
                        }
                    }

                    // Check for incoming messages
                    await CheckForMessagesAsync();
                }
                catch (Exception)
                {
                    // Handle any unexpected exceptions by disconnecting
                    await DisconnectAsync();
                }

                // Small delay to prevent busy waiting
                await Task.Delay(50);
            }
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

            // Run async operations without blocking the timer thread
            _ = Task.Run(async () =>
            {
                try
                {
                    // Try to reconnect if not connected
                    if (!_pipeClient.IsConnected)
                    {
                        await TryReconnectAsync();
                    }

                    // Process queued messages if connected
                    if (_messageQueue.Count > 0 && _pipeClient != null && _pipeClient.IsConnected)
                    {
                        while (_messageQueue.Count > 0)
                        {
                            var message = _messageQueue.Pop();
                            await WriteMessageToStreamAsync(message);
                            
                            // Check if connection was lost during write
                            if (_pipeClient == null || !_pipeClient.IsConnected)
                            {
                                // Put the message back if it wasn't sent
                                _messageQueue.Push(message);
                                break;
                            }
                        }
                    }

                    // Check for incoming messages
                    await CheckForMessagesAsync();
                }
                catch (Exception)
                {
                    // Handle any unexpected exceptions by disconnecting
                    await DisconnectAsync();
                }
            });
        }

        /// <summary>
        /// Checks for incoming messages from the pipe server and raises the MessageReceived event for each message.
        /// </summary>
        private async Task CheckForMessagesAsync()
        {
            if (_pipeClient == null || _pipeReader == null)
            {
                return;
            }

            try
            {
                while (_pipeClient != null && _pipeClient.IsConnected)
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
                    else
                    {
                        // No data available, break to prevent busy waiting
                        break;
                    }
                }
            }
            catch (IOException)
            {
                // Pipe is broken, clean up the connection
                await DisconnectAsync();
            }
            catch (ObjectDisposedException)
            {
                // Stream has been disposed, clean up the connection
                await DisconnectAsync();
            }
            catch (InvalidOperationException)
            {
                // Stream is in an invalid state, clean up the connection
                await DisconnectAsync();
            }
        }

        /// <summary>
        /// Writes a message to the pipe stream in JSON format.
        /// </summary>
        /// <param name="message">The message to send.</param>
        private async Task WriteMessageToStreamAsync(T message)
        {
            if (_pipeClient == null || _pipeWriter == null)
            {
                return;
            }

            try
            {
                // Double-check connection state before writing
                if (!_pipeClient.IsConnected)
                {
                    return;
                }

                await _pipeWriter.WriteLineAsync(JsonConvert.SerializeObject(message));
            }
            catch (IOException)
            {
                // Pipe is broken, clean up the connection
                await DisconnectAsync();
            }
            catch (ObjectDisposedException)
            {
                // Stream has been disposed, clean up the connection
                await DisconnectAsync();
            }
            catch (InvalidOperationException)
            {
                // Stream is in an invalid state, clean up the connection
                await DisconnectAsync();
            }
        }

        /// <summary>
        /// Attempts to reconnect to the pipe server if the connection was lost.
        /// </summary>
        private async Task TryReconnectAsync()
        {
            if (_pipeClient != null && _pipeClient.IsConnected)
            {
                return; // Already connected
            }

            try
            {
                // Clean up existing connection first
                await DisconnectAsync();

                // Create new connection
                _pipeClient = new(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                
                // Try to connect with a timeout
                var connectTask = _pipeClient.ConnectAsync();
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
                
                var completedTask = await Task.WhenAny(connectTask, timeoutTask);
                
                if (completedTask == timeoutTask)
                {
                    // Connection timeout
                    await DisconnectAsync();
                    return;
                }

                // Connection successful, setup streams
                _pipeReader = new StreamReader(_pipeClient);
                _pipeWriter = new StreamWriter(_pipeClient) { AutoFlush = true };
            }
            catch
            {
                // Connection failed, clean up
                await DisconnectAsync();
            }
        }

        /// <summary>
        /// Disconnects and cleans up the current pipe connection.
        /// </summary>
        private async Task DisconnectAsync()
        {
            try
            {
                _pipeReader?.Dispose();
                _pipeWriter?.Dispose();
                _pipeClient?.Dispose();
            }
            catch
            {
                // Ignore exceptions during cleanup
            }
            finally
            {
                _pipeReader = null;
                _pipeWriter = null;
                _pipeClient = null;
            }

            // Small delay before allowing reconnection attempts
            await Task.Delay(100);
        }
    }
}
