namespace BeXCool.PipeMessages.Common
{
    public class PipeMessageEventArgs<T>
    {
        public T Message { get; private set; }
        public PipeMessageEventArgs(T message)
        {
            Message = message;
        }
    }
}