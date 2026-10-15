using System.Text.Json;

// Owned disposable test program only; never shipped in the app/candidate app directory.
if (args is ["--shell-new-probe", var kind]) return ShellNewProbe.Run(kind);
if (args.Length == 0)
{
    File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "opened.marker"), "owned-fixture-opened");
    return 0;
}
if (args is not ["--stdio-v1"] || !Console.IsInputRedirected) return 2;
using var request = JsonDocument.Parse(Console.In.ReadToEnd());
string? mode = request.RootElement.GetProperty("Item").GetProperty("DisplayName").GetString();
if (mode == "hang") { await Task.Delay(TimeSpan.FromMinutes(2)); return 9; }
if (mode == "ack-then-wait")
{
    // Synthetic provider acknowledgment, NOT a real Shell execution.
    Console.Write(JsonSerializer.Serialize(new { RequestId=request.RootElement.GetProperty("RequestId").GetGuid(),
        Availability=0, Message="fixture accepted", Evidence=1, Outcome=1 }));
    Console.Out.Flush(); CloseHandle(GetStdHandle(-11));
    await Task.Delay(TimeSpan.FromMinutes(2)); return 0;
}
if (mode == "crash") return 7;
if (mode == "oversize") { Console.Write(new string('x', 500000)); return 0; }
Console.Write("{not-json"); return 0;

[System.Runtime.InteropServices.DllImport("kernel32.dll")] static extern nint GetStdHandle(int standard);
[System.Runtime.InteropServices.DllImport("kernel32.dll")] static extern bool CloseHandle(nint handle);
