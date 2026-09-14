namespace OdinEye.Tests
{
    using NUnitLite;
    using System.Reflection;

    // NUnitLite console entry point -- same pattern as
    // OdinEye.Client.Tests: lets CI (and local dev, no `dotnet` CLI
    // available in this repo's toolchain) run the suite as a plain .NET
    // Framework executable: `OdinEye.Tests.exe`.
    public static class Program
    {
        public static int Main(string[] args) => new AutoRun(Assembly.GetExecutingAssembly()).Execute(args);
    }
}
