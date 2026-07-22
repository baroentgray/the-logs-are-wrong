namespace Tlaw.Dispatcher;

public static class Program
{
    public static int Main(string[] args) => TaskPacketCommand.Run(args, Console.Out, Console.Error);
}
