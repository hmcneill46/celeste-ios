using AppleEverest.IlFreeze;

int result = CanaryTarget.Scale(5);
Console.WriteLine($"APPLE_EVEREST_IL_FREEZE_RESULT={result}");
bool frozen = string.Equals(Environment.GetEnvironmentVariable("EXPECTED"), "15", StringComparison.Ordinal);
bool pass = result == (frozen ? 15 : 10) &&
    CanaryTarget.StaticCall(5) == (frozen ? 9 : 5) &&
    CanaryTarget.Branch(0) == (frozen ? 99 : 1) &&
    CanaryTarget.Branch(2) == 3 &&
    CanaryTarget.AlterReturn(5) == (frozen ? 9 : 5) &&
    CanaryTarget.LocalRoundTrip(5) == 5 &&
    CanaryTarget.RemoveRoundTrip(5) == 5;
if (!pass)
    Environment.ExitCode = 1;
