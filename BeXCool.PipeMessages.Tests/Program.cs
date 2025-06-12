using BeXCool.PipeMessages;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("This is client.");

        var client = new PipeMessageClient<string>("Test");
        client.MessageReceived += (sender, args) =>
        {
            Console.WriteLine($"Client received message: {args.Message}");
        };

        await client.StartAsync();
        //await client.SendMessageAsync("I love Viki to server <3");
        //await client.SendMessageAsync("Are you alive - server?");

        Console.WriteLine("Messages sent.");

        while (true)
        {
            string input = Console.ReadLine() ?? "";
            await client.SendMessageAsync("[Client says]: " + input);
        }
    }
}
