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
    // instance API on World at all). Built instead by observing the real
    // live server directly, diffing WorldDetailsController's own already-
    // shipped GlobalKeys output before/after a real RCON command.
    //
    // The single global key literally named "preset" (absent entirely
    // when nothing has ever been customized) does NOT have one fixed
    // shape, confirmed by two separate live observations that initially
    // looked contradictory:
    //  - `setworldmodifier <category> <value>` (an individual dial) writes
    //    a ':'-joined "<category>_<value>" list covering all five
    //    categories every time, e.g. "combat_default:deathpenalty_default:
    //    resources_default:raids_default:portals_casual".
    //  - `setworldpreset <name>` (a named preset button) writes the bare
    //    preset name instead, e.g. just "hardcore" -- confirmed via IL
    //    disassembly of `ServerOptionsGUI.SetPreset(World, string)`: it
    //    tries `Enum.TryParse<WorldPresets>` on the WHOLE string first,
    //    and only falls back to the per-category split if that fails, so
    //    a real preset name is stored verbatim, never expanded.
    // A first version of this controller only handled the first shape --
    // live-tested against a real `setworldpreset hardcore` call, it
    // silently produced an EMPTY Modifiers list (every pair failed the
    // "_"-split-into-2 check) and, worse, a Summary of "Default" -- an
    // actively wrong answer (claiming vanilla defaults while Hardcore was
    // really active), caught before merge by the same "verify against the
    // real server, don't trust IL alone" discipline used throughout this
    // endpoint's history. Now: a per-category pair is only trusted when
    // ALL FIVE categories parse cleanly; otherwise (a bare preset name, or
    // any other shape not currently understood) every category reports
    // "unknown" rather than guessing "default" -- see NamedPresetCategories
    // below for the one case this can still resolve precisely.
    public class WorldModifiersController : IController
    {
        public string Route => "/worldModifiers";

        private static readonly string[] Categories =
            { "combat", "deathpenalty", "resources", "raids", "portals" };

        public void OnGet(HttpRequestEventArgs requestArguments)
        {
            var modifiers = new List<WorldModifierValue>();
            string presetValue = null;
            string namedPreset = null;

            if (ZoneSystem.instance != null &&
                ZoneSystem.instance.m_globalKeysValues.TryGetValue("preset", out presetValue) &&
                !string.IsNullOrEmpty(presetValue))
            {
                var parsed = new Dictionary<string, string>();
                foreach (var pair in presetValue.Split(':'))
                {
                    var parts = pair.Split('_');
                    if (parts.Length == 2)
                    {
                        parsed[parts[0]] = parts[1];
                    }
                }

                if (Categories.All(parsed.ContainsKey))
                {
                    // The expanded, per-category shape -- every category
                    // parsed cleanly, so this is trustworthy in full.
                    foreach (var category in Categories)
                    {
                        modifiers.Add(new WorldModifierValue { Category = category, Value = parsed[category] });
                    }
                }
                else
                {
                    // Doesn't match the expanded shape -- almost certainly
                    // a bare preset name (e.g. "hardcore") written by
                    // setworldpreset. Report it as what it plainly is
                    // rather than guessing a wrong per-category
                    // breakdown: every category is "unknown" (NOT
                    // "default" -- that would be actively wrong, exactly
                    // the bug this rewrite fixes), and the raw preset
                    // name carries the real information via Summary.
                    namedPreset = presetValue;
                    foreach (var category in Categories)
                    {
                        modifiers.Add(new WorldModifierValue { Category = category, Value = "unknown" });
                    }
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

            string summary;
            if (namedPreset != null)
            {
                summary = $"Preset: {namedPreset}";
            }
            else
            {
                var nonDefault = modifiers.Where(m => m.Value != "default").ToList();
                summary = nonDefault.Count == 0
                    ? "Default"
                    : string.Join(", ", nonDefault.Select(m => $"{m.Category}: {m.Value}"));
            }

            requestArguments.Response.Ok(new WorldModifiersDetails
            {
                Modifiers = modifiers,
                Summary = summary
            });
        }
    }
}
