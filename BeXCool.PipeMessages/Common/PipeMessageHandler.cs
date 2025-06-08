namespace BeXCool.PipeMessages.Common
{
    public delegate void PipeMessageHandler<T>(object sender, PipeMessageEventArgs<T> e);
}
