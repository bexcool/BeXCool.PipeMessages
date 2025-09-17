using BeXCool.PipeMessages;
using BeXCool.PipeMessages.Tests;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("BeXCool.PipeMessages Test Application");
        Console.WriteLine("=====================================");
        
        // Run error handling tests first
        await ErrorHandlingTest.RunErrorHandlingTestAsync();
        
        Console.WriteLine("\n\nPress Enter to start interactive client test...");
        Console.ReadLine();
        
        Console.WriteLine("This is client.");

        var client = new PipeMessageClient<string>("Test");
        client.MessageReceived += (sender, args) =>
        {
            Console.WriteLine($"Client received message: {args.Message}");
        };

        try
        {
            await client.StartAsync();
            Console.WriteLine("Client started successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Client failed to start: {ex.Message}");
            Console.WriteLine("Make sure the server is running first.");
            return;
        }

        Console.WriteLine("Messages sent.");
        Console.WriteLine("Type messages to send to server (or 'quit' to exit):");

        while (true)
        {
            string input = Console.ReadLine() ?? "";
            if (input.ToLower() == "quit")
                break;
                
            bool success = await client.SendMessageAsync("[Client says]: " + input);
            if (!success)
            {
                Console.WriteLine("Failed to send message");
            }
        }
        
        client.Dispose();
        Console.WriteLine("Client disposed. Goodbye!");
    }
}
