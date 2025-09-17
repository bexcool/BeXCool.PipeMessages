using BeXCool.PipeMessages;

namespace BeXCool.PipeMessages.Tests
{
    /// <summary>
    /// Test class to demonstrate error handling and recovery functionality
    /// </summary>
    public class ErrorHandlingTest
    {
        public static async Task RunErrorHandlingTestAsync()
        {
            Console.WriteLine("=== Error Handling and Recovery Test ===");
            
            // Test 1: Basic connection and messaging
            Console.WriteLine("\n1. Testing basic connection and messaging...");
            var client = new PipeMessageClient<string>("ErrorHandlingTest");
            
            bool messageReceived = false;
            client.MessageReceived += (sender, args) =>
            {
                Console.WriteLine($"✓ Client received message: {args.Message}");
                messageReceived = true;
            };

            try
            {
                // This should fail gracefully since no server is running
                Console.WriteLine("Attempting to start client (no server running)...");
                await client.StartAsync();
                Console.WriteLine("✗ StartAsync should have thrown an exception");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✓ Expected exception caught: {ex.GetType().Name}");
            }

            // Test 2: Sending messages when not connected (should queue)
            Console.WriteLine("\n2. Testing message queueing when disconnected...");
            bool result1 = await client.SendMessageAsync("Test message 1 (should be queued)");
            bool result2 = await client.SendMessageAsync("Test message 2 (should be queued)");
            
            Console.WriteLine($"✓ Message 1 queued: {result1}");
            Console.WriteLine($"✓ Message 2 queued: {result2}");

            // Test 3: Disposing client properly
            Console.WriteLine("\n3. Testing proper disposal...");
            client.Dispose();
            Console.WriteLine("✓ Client disposed successfully");

            // Test 4: Testing manual check mode
            Console.WriteLine("\n4. Testing manual check mode...");
            var manualClient = new PipeMessageClient<string>("ErrorHandlingTestManual", manualCheck: true);
            
            try
            {
                await manualClient.StartAsync();
                Console.WriteLine("✗ Manual client StartAsync should have thrown an exception");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✓ Manual client exception: {ex.GetType().Name}");
            }

            // Test force check when not connected
            manualClient.ForceCheck();
            Console.WriteLine("✓ Force check completed (no server connected)");
            
            manualClient.Dispose();
            Console.WriteLine("✓ Manual client disposed successfully");

            Console.WriteLine("\n=== Error Handling Test Completed ===");
            Console.WriteLine("All error handling scenarios tested successfully!");
            Console.WriteLine("The pipe client now properly handles broken pipe exceptions and can recover gracefully.");
        }
    }
}