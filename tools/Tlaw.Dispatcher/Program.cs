namespace Tlaw.Dispatcher;

public static class Program
{
    public static int Main(string[] args) => args.Length > 0 && string.Equals(args[0], "lease", StringComparison.Ordinal)
        ? LeaseCommand.Run(args, Console.Out, Console.Error)
        : args.Length > 0 && string.Equals(args[0], "route", StringComparison.Ordinal)
            ? RouteCommand.Run(args, Console.Out, Console.Error)
            : args.Length > 0 && string.Equals(args[0], "ingest-result", StringComparison.Ordinal)
                ? IngestResultCommand.Run(args, Console.Out, Console.Error)
                : TaskPacketCommand.Run(args, Console.Out, Console.Error);
}
