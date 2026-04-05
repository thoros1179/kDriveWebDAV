using System.CommandLine;
using kDriveWebDav.Config;
using kDriveWebDav.KDrive;
using kDriveWebDav.WebDav;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NWebDav.Server;
using NWebDav.Server.Locking;

// ======================================================================
//  Root command
// ======================================================================

var rootCommand = new RootCommand("kDrive WebDAV – exposes Infomaniak kDrive accounts via WebDAV");

// ======================================================================
//  "start" command
// ======================================================================

var portOption = new Option<int>(
    aliases: ["--port", "-p"],
    description: "TCP port the WebDAV server will listen on",
    getDefaultValue: () => 8080);

var hostOption = new Option<string>(
    aliases: ["--host", "-H"],
    description: "Host/IP address to bind to",
    getDefaultValue: () => "localhost");

var configOption = new Option<string?>(
    aliases: ["--config", "-c"],
    description: "Path to the accounts configuration file (optional)");

var verboseOption = new Option<bool>(
    aliases: ["--verbose", "-v"],
    description: "Log every WebDAV request and response to stdout");

var startCommand = new Command("start", "Start the WebDAV server");
startCommand.AddOption(portOption);
startCommand.AddOption(hostOption);
startCommand.AddOption(configOption);
startCommand.AddOption(verboseOption);

startCommand.SetHandler(async (int port, string host, string? config, bool verbose) =>
{
    var configManager = new ConfigManager(config);
    var accounts = configManager.LoadAccounts();

    if (accounts.Count == 0)
    {
        Console.Error.WriteLine(
            "No accounts configured. Add accounts with 'account add' before starting the server.");
        Environment.Exit(1);
        return;
    }

    Console.WriteLine($"Starting kDrive WebDAV server on http://{host}:{port}/");
    Console.WriteLine($"Serving {accounts.Count} account(s):");
    foreach (var a in accounts)
        Console.WriteLine($"  • {a.Name}  (drive id: {a.DriveId}) → http://{host}:{port}/{a.Name}/");

    var builder = WebApplication.CreateBuilder();
    builder.WebHost.UseUrls($"http://{host}:{port}");
    builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = null); // no limit
    builder.Logging.SetMinimumLevel(LogLevel.Warning);

    var app = builder.Build();

    // Request/response logger – activate with --verbose
    if (verbose)
    {
        app.Use(async (context, next) =>
        {
            var method = context.Request.Method;
            var path   = context.Request.Path.Value ?? "/";
            var depth  = context.Request.Headers["Depth"].FirstOrDefault();
            var ct     = context.Request.ContentType;
            var cl     = context.Request.ContentLength;

            var headerParts = new List<string>();
            if (depth  != null) headerParts.Add($"Depth:{depth}");
            if (ct     != null) headerParts.Add($"CT:{ct.Split(';')[0].Trim()}");
            if (cl     != null) headerParts.Add($"CL:{cl}");
            var extra = headerParts.Count > 0 ? $" [{string.Join(" ", headerParts)}]" : "";

            Console.WriteLine($"→ {method,-9} {path}{extra}");

            await next();

            Console.WriteLine($"← {method,-9} {context.Response.StatusCode}  {path}");
        });
    }

    // Mount the WebDAV dispatcher for all requests
    var lockingManager = new InMemoryLockingManager();
    var store = new MultiTenantStore(accounts, lockingManager);
    var dispatcher = new WebDavDispatcher(store, new RequestHandlerFactory());

    app.Run(async context =>
    {
        var httpCtx = new AspNetCoreHttpContext(context);
        await dispatcher.DispatchRequestAsync(httpCtx);
    });

    await app.RunAsync();

}, portOption, hostOption, configOption, verboseOption);

// ======================================================================
//  "account" command group
// ======================================================================

var accountCommand = new Command("account", "Manage kDrive accounts");

// -- account add --

var nameOption = new Option<string>("--name", "Unique name for the account (used as WebDAV path prefix)")
    { IsRequired = true };
var tokenOption = new Option<string>("--token", "Infomaniak API Bearer token")
    { IsRequired = true };
var driveIdOption = new Option<long>("--drive-id", "Numeric kDrive ID")
    { IsRequired = true };
var addConfigOption = new Option<string?>(["--config", "-c"], "Path to the accounts configuration file (optional)");

var addCommand = new Command("add", "Add or update a kDrive account");
addCommand.AddOption(nameOption);
addCommand.AddOption(tokenOption);
addCommand.AddOption(driveIdOption);
addCommand.AddOption(addConfigOption);

addCommand.SetHandler((string name, string token, long driveId, string? cfgPath) =>
{
    var configManager = new ConfigManager(cfgPath);
    configManager.AddOrUpdateAccount(new AccountConfig
    {
        Name = name,
        Token = token,
        DriveId = driveId,
    });
    Console.WriteLine($"Account '{name}' saved to {configManager.ConfigPath}");

}, nameOption, tokenOption, driveIdOption, addConfigOption);

// -- account list --

var listConfigOption = new Option<string?>(["--config", "-c"], "Path to the accounts configuration file (optional)");
var listCommand = new Command("list", "List all configured accounts");
listCommand.AddOption(listConfigOption);

listCommand.SetHandler((string? cfgPath) =>
{
    var configManager = new ConfigManager(cfgPath);
    var accounts = configManager.LoadAccounts();

    if (accounts.Count == 0)
    {
        Console.WriteLine("No accounts configured.");
        return;
    }

    Console.WriteLine($"{"Name",-20} {"Drive ID",10}");
    Console.WriteLine(new string('-', 32));
    foreach (var a in accounts)
        Console.WriteLine($"{a.Name,-20} {a.DriveId,10}");

}, listConfigOption);

// -- account remove --

var removeNameOption = new Option<string>("--name", "Name of the account to remove")
    { IsRequired = true };
var removeConfigOption = new Option<string?>(["--config", "-c"], "Path to the accounts configuration file (optional)");

var removeCommand = new Command("remove", "Remove a configured account");
removeCommand.AddOption(removeNameOption);
removeCommand.AddOption(removeConfigOption);

removeCommand.SetHandler((string name, string? cfgPath) =>
{
    var configManager = new ConfigManager(cfgPath);
    if (configManager.RemoveAccount(name))
        Console.WriteLine($"Account '{name}' removed.");
    else
        Console.Error.WriteLine($"Account '{name}' not found.");

}, removeNameOption, removeConfigOption);

accountCommand.AddCommand(addCommand);
accountCommand.AddCommand(listCommand);
accountCommand.AddCommand(removeCommand);

// ======================================================================
//  Wire everything up
// ======================================================================

rootCommand.AddCommand(startCommand);
rootCommand.AddCommand(accountCommand);

return await rootCommand.InvokeAsync(args);
