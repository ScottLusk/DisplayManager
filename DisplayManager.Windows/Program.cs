using Gregghz.DisplayManager.Services.Implementations;
using Gregghz.DisplayManager.Cli;
using Gregghz.DisplayManager.Windows.Services.Implementations;

var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
var path = Path.Combine(appDataPath, "DisplayManager");
Directory.CreateDirectory(path);

var layoutService = new FileSystemLayoutService(path);
var displayService = new WindowsDisplayService();

var argsToRun = args.Length == 0 ? ["--help"] : args;

var console = new ConsoleMain(displayService, layoutService);
console.Run(argsToRun);
