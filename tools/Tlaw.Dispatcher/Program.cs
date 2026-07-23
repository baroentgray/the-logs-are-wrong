namespace Tlaw.Dispatcher;

public static class Program
{
    public static int Main(string[] args) => args.Length > 0 && string.Equals(args[0], "lease", StringComparison.Ordinal)
        ? LeaseCommand.Run(args, Console.Out, Console.Error)
        : args.Length > 0 && string.Equals(args[0], "route", StringComparison.Ordinal)
            ? RouteCommand.Run(args, Console.Out, Console.Error)
            : args.Length > 0 && string.Equals(args[0], "ingest-result", StringComparison.Ordinal)
            ? IngestResultCommand.Run(args, Console.Out, Console.Error)
            : args.Length > 0 && string.Equals(args[0], "finalize-result", StringComparison.Ordinal)
                ? FinalizeResultCommand.Run(args, Console.Out, Console.Error)
            : args.Length > 0 && string.Equals(args[0], "ingest-review", StringComparison.Ordinal)
                ? IngestReviewCommand.Run(args, Console.Out, Console.Error)
            : args.Length > 0 && string.Equals(args[0], "prepare-handoff", StringComparison.Ordinal)
                ? PrepareHandoffCommand.Run(args, Console.Out, Console.Error)
            : args.Length > 0 && string.Equals(args[0], "ingest-handoff", StringComparison.Ordinal)
                ? IngestHandoffCommand.Run(args, Console.Out, Console.Error)
            : TaskPacketCommand.Run(args, Console.Out, Console.Error);
}
