using System.Reflection;
using System.Runtime.InteropServices;

[assembly: AssemblyTitle("OdinEye.Client")]
[assembly: AssemblyDescription("")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyProduct("OdinEye.Client")]
[assembly: AssemblyCopyright("Copyright ©  2026")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

[assembly: ComVisible(false)]

[assembly: Guid("2f6e4b8e-6a3f-4b1a-9b0e-8f6c5b4a3d2e")]

[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

// VALSER-93 follow-up: what OdinEyeClientPlugin.SubmitCurrentStats reports
// of itself as ClientVersion. Deliberately NOT AssemblyVersion/
// AssemblyFileVersion above -- those stay fixed at 1.0.0.0 (BepInEx's own
// plugin-dependency-resolution bookkeeping, unrelated to the panel) and
// have never once been bumped since this plugin's first commit, which is
// exactly the bug this fixes: every release ever shipped has self-reported
// the same hardcoded value here, regardless of which real version (v1.2.x
// git tag / Thunderstore release) a player actually has installed.
// release.yaml's "Stamp OdinEye.Client's self-reported version" step
// replaces this placeholder with the real tag (stripped of its leading
// "v") before building, every release, the same way it already stamps
// thunderstore-*.toml's versionNumber. A build that was never stamped by
// that step (a local dev build) reports this placeholder verbatim --
// obviously not a real release version, by design, rather than a
// plausible-looking lie.
[assembly: AssemblyInformationalVersion("0.0.0-dev")]
