// env-report.csx
// Print a short runtime and environment report. Handy as a sanity check
// that the Roslyn host sees the .NET BCL the way you expect it to.

using System.Reflection;

Console.WriteLine("Runtime");
Console.WriteLine($"  OS            : {Environment.OSVersion}");
Console.WriteLine($"  Runtime       : {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");
Console.WriteLine($"  Architecture  : {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}");
Console.WriteLine($"  Processor     : {Environment.ProcessorCount} logical cores");
Console.WriteLine($"  Working set   : {Environment.WorkingSet / 1024 / 1024} MB");

Console.WriteLine();
Console.WriteLine("Process");
Console.WriteLine($"  UserName      : {Environment.UserName}");
Console.WriteLine($"  MachineName   : {Environment.MachineName}");
Console.WriteLine($"  CurrentDir    : {Environment.CurrentDirectory}");

Console.WriteLine();
Console.WriteLine("Loaded Blast assemblies");
foreach (var asm in AppDomain.CurrentDomain.GetAssemblies()
                            .Where(a => a.GetName().Name?.EndsWith("Blast") == true)
                            .OrderBy(a => a.GetName().Name))
{
    var name = asm.GetName();
    Console.WriteLine($"  {name.Name,-12} v{name.Version}");
}
