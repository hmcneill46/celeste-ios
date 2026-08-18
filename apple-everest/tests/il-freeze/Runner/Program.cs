using AppleEverest.IlFreeze;

int result = CanaryTarget.Scale(5);
Console.WriteLine($"APPLE_EVEREST_IL_FREEZE_RESULT={result}");
if (result != int.Parse(Environment.GetEnvironmentVariable("EXPECTED") ?? "10"))
    Environment.ExitCode = 1;
