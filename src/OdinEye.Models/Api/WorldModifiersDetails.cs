namespace OdinEye.Models.Api
{
    using System.Collections.Generic;
    using System.Linq;

    // Named *Details (matching WorldDetails/BossDetails/ServerDetails'
    // existing convention here) rather than plain "WorldModifiers" --
    // the game itself already has a global `WorldModifiers` enum (the
    // category enum this endpoint reads via reflection below), and reusing
    // that exact name for this DTO would collide the moment both are in
    // scope in the same file.
    public class WorldModifiersDetails
    {
        public IEnumerable<WorldModifierValue> Modifiers { get; set; } = Enumerable.Empty<WorldModifierValue>();

        // Valheim's own F2 in-game overlay renders this exact text --
        // included as a low-risk fallback alongside the structured
        // Modifiers list above, in case any individual category's enum
        // reflection below doesn't resolve to something meaningful the
        // first time this actually compiles/runs (see ODINEYE-23).
        public string Summary { get; set; }
    }
}
