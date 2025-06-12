using BeXCool.PipeMessages;

class ServerProgram
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("This is server.");

        var server = new PipeMessageServer<string>("Test");
        server.MessageReceived += (sender, args) =>
        {
            Console.WriteLine($"Server received message: {args.Message}");
        };

        await server.StartAsync();
        //await server.SendMessageAsync("I love Viki <3");

        while (true)
        {
            string input = Console.ReadLine() ?? "";
            await server.SendMessageAsync("[Server says]: " + input);
        }
    }
}
