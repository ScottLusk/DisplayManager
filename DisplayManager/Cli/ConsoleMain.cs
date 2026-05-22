using CommandLine;
using Gregghz.DisplayManager.Services;
using Gregghz.DisplayManager.Cli.Model;

namespace Gregghz.DisplayManager.Cli;

public class ConsoleMain(IDisplayService displayService, ILayoutService layoutService)
{
  public void Run(string[] args)
  {
    Parser.Default.ParseArguments<CliOptions>(args)
      .WithParsed(opts => Run(opts).GetAwaiter().GetResult())
      .WithNotParsed(errs =>
      {
        foreach (var err in errs) Console.WriteLine(err);
      });
  }

  private async Task Run(CliOptions opts)
  {
    if (opts.Info)
    {
      var result = displayService.GetMonitorInfo();
      Console.WriteLine(result);
      return;
    }

    if (opts.ListLayouts)
    {
      await ListLayouts();
      return;
    }

    if (opts.ClearLayouts)
    {
      await ClearLayouts();
      return;
    }

    switch (opts.ConfigToLoad, opts.ConfigToSave)
    {
      case (not null, null):
        var layout = await layoutService.GetLayout(opts.ConfigToLoad);
        if (layout is not null) await displayService.ApplyLayout(layout);
        else
          // @TODO: handle this
          await Console.Error.WriteLineAsync($"{layout} does not exist.");

        break;
      case (null, not null):
        var currentLayout = displayService.GetDisplayLayout();
        await layoutService.SaveLayout(opts.ConfigToSave, currentLayout);
        break;
      default:
        await Console.Error.WriteLineAsync("Invalid arguments.");
        break;
    }
  }


  private async Task ListLayouts()
  {
    var layoutNames = await layoutService.GetSavedLayouts();

    if (layoutNames.Count == 0)
    {
      var previousColor = Console.ForegroundColor;
      Console.ForegroundColor = ConsoleColor.Yellow;
      Console.WriteLine("No saved layouts found.");
      Console.ForegroundColor = previousColor;
      return;
    }

    foreach (var layoutName in layoutNames) Console.WriteLine(layoutName);
  }

  private async Task ClearLayouts()
  {
    var clearedCount = await layoutService.ClearLayouts();

    if (clearedCount == 0)
    {
      var previousColor = Console.ForegroundColor;
      Console.ForegroundColor = ConsoleColor.Yellow;
      Console.WriteLine("No saved layouts found.");
      Console.ForegroundColor = previousColor;
      return;
    }

    Console.WriteLine($"Cleared {clearedCount} saved layout(s).");
  }
}