namespace OdinEye.Http.Api.Controllers
{
    using Extensions;
    using Models.Api;
    using System.Collections.Generic;
    using System.Linq;
    using WebSocketSharp.Server;

    // Read-only report of the world's CURRENT difficulty modifiers --
    // ODINEYE-23. Writing them is handled entirely separately, by
    // Tristan/ValheimRcon's `consoleCommand` passthrough calling Valheim's
    // own `setworldmodifier`/`setworldpreset` console commands -- this
    // endpoint exists purely because nothing in that plugin's command set
    // reads current values back, and OdinEye (already in-process) can
    // read them directly off the world's own global keys instead.
    //
    // Ground truth here is EMPIRICAL, not guessed from disassembly: an
    // earlier version of this controller called `World.GetModifier()`/
    // `GetWorldModifierSummary()` directly, guessed from `strings`/`monodis`
    // symbol names alone -- real CI proved that guess wrong (CS1061: both
    // methods actually live on `ServerOptionsGUI`, a Unity-scene-coupled
    // settings-menu UI class needing its own GameObject hierarchy and
    // private static arrays populated by its own Awake(), not a clean
    // instance API on World at all). Rather than compound that risk with
    // more disassembly-derived guesses about Unity-UI-internal state, this
    // version was built by observing the REAL live server directly:
    // setting a real modifier via RCON (`setworldmodifier portals casual`)
    // and diffing the exact GlobalKeys this endpoint's own sibling
    // (WorldDetailsController) already exposes, before vs. after. Result: a
    // single global key literally named "preset" appears (entirely ABSENT
    // when nothing has ever been customized on this world) whose value is
    // a ':'-joined list of "<category>_<value>" pairs covering all five
    // categories every time, e.g. "combat_default:deathpenalty_default:
    // resources_default:raids_default:portals_casual" -- the same
    // "<enum>_<enum>" split-by-':' then split-by-'_' shape
    // `ServerOptionsGUI.SetPreset(World, string)` itself parses on the way
    // in (confirmed via IL disassembly), just read back here instead of
    // written. Uses the exact same `ZoneSystem.instance.m_globalKeysValues`
    // field WorldDetailsController already reads in production -- no new
    // access pattern, no reflection, no dependency on any Unity-scene UI
    // object existing.
    public class WorldModifiersController : IController
    {
        public string Route => "/worldModifiers";

        private static readonly string[] Categories =
            { "combat", "deathpenalty", "resources", "raids", "portals" };

        public void OnGet(HttpRequestEventArgs requestArguments)
        {
            var modifiers = new List<WorldModifierValue>();
            string presetValue = null;

            if (ZoneSystem.instance != null &&
                ZoneSystem.instance.m_globalKeysValues.TryGetValue("preset", out presetValue) &&
                !string.IsNullOrEmpty(presetValue))
            {
                foreach (var pair in presetValue.Split(':'))
                {
                    var parts = pair.Split('_');
                    if (parts.Length != 2)
                    {
                        continue;
                    }

                    modifiers.Add(new WorldModifierValue { Category = parts[0], Value = parts[1] });
                }
            }
            else
            {
                // No "preset" key at all means nothing has ever been
                // customized on this world -- every category is at its
                // untouched, vanilla default. Reported explicitly (not
                // left as an empty list) so a caller doesn't have to
                // special-case "no key yet" differently from "every
                // category explicitly confirmed default".
                foreach (var category in Categories)
                {
                    modifiers.Add(new WorldModifierValue { Category = category, Value = "default" });
                }
            }

            var nonDefault = modifiers.Where(m => m.Value != "default").ToList();
            var summary = nonDefault.Count == 0
                ? "Default"
                : string.Join(", ", nonDefault.Select(m => $"{m.Category}: {m.Value}"));

            requestArguments.Response.Ok(new WorldModifiersDetails
            {
                Modifiers = modifiers,
                Summary = summary
            });
        }
    }
}
