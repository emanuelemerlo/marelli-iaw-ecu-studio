using MarelliIawEcuStudio.Cli;

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancellation.Cancel();
};
return new CliApp(Console.Out, Console.In).Run(args, cancellation.Token);
