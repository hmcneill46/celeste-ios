using System.Reflection;

if (args.Length != 2)
    throw new ArgumentException("usage: Runner target.dll expected-result");
Assembly target = Assembly.LoadFrom(Path.GetFullPath(args[0]));
MethodInfo method = target.GetType("AppleEverest.IlCompose.ComposeTarget", throwOnError: true)!
    .GetMethod("Compose", BindingFlags.Public | BindingFlags.Static)!;
int actual = (int)method.Invoke(null, [4])!;
int expected = int.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture);
Console.WriteLine($"APPLE_EVEREST_IL_COMPOSE_RESULT={actual}");
if (actual != expected) Environment.ExitCode = 1;
