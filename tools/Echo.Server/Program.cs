using System.Diagnostics;

const string repo = @"C:\dev\Echo-Server";

Console.Title = "Echo Server (Production)";

if (!Directory.Exists(repo))
{
    Console.Error.WriteLine($"No existe la carpeta {repo}");
    Pause();
    return 1;
}

var start = new ProcessStartInfo
{
    FileName = "dotnet",
    Arguments = "run --project src/Echo.Api -c Release --no-launch-profile",
    WorkingDirectory = repo,
    UseShellExecute = false,
};

start.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
start.Environment["ASPNETCORE_URLS"] = "http://0.0.0.0:8080";

Console.WriteLine("Echo Server");
Console.WriteLine($"  {repo}");
Console.WriteLine("  ASPNETCORE_ENVIRONMENT=Production");
Console.WriteLine("  ASPNETCORE_URLS=http://0.0.0.0:8080");
Console.WriteLine("  dotnet run --project src/Echo.Api -c Release --no-launch-profile");
Console.WriteLine();

using var process = Process.Start(start);
if (process is null)
{
    Console.Error.WriteLine("No se pudo iniciar dotnet.");
    Pause();
    return 1;
}

process.WaitForExit();
if (process.ExitCode != 0)
{
    Console.WriteLine();
    Console.WriteLine($"La API terminó con código {process.ExitCode}.");
    Pause();
}

return process.ExitCode;

static void Pause()
{
    Console.WriteLine("Pulsa Enter para cerrar.");
    Console.ReadLine();
}
