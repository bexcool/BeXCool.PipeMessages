using BeXCool.PipeMessages.Tests;
using BeXCool.PipeMessages;

class Program
{
    static async Task Main(string[] args)
    {
        var server = new PipeMessageServer<TestModel>("Test");
        server.MessageReceived += (sender, args) =>
        {
            Console.WriteLine($"Server received message: {args.Message.Data}");
        };

        server.Start();
        server.SendMessage(new TestModel(22, "I love Viki <3"));

        var client = new PipeMessageClient<TestModel>("Test");
        client.MessageReceived += (sender, args) =>
        {
            Console.WriteLine($"Client received message: {args.Message.Data}");
        };

        client.Start();
        client.SendMessage(new TestModel(22, "I love Viki to server <3"));
        client.SendMessage(new TestModel(1, "Are you alive - server?"));

        Console.WriteLine("Messages sent.");

        while (true)
        {
            await Task.Delay(1000);
            client.SendMessage(new TestModel(1, "Are you alive - server?"));
        }
    }
}
