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
        // Always all five categories (combat, deathpenalty, resources,
        // raids, portals) -- see WorldModifiersController's own comment
        // for exactly where this data comes from (the world's own
        // "preset" global key, empirically confirmed, not guessed).
        public IEnumerable<WorldModifierValue> Modifiers { get; set; } = Enumerable.Empty<WorldModifierValue>();

        // A short human-readable line built from Modifiers itself (e.g.
        // "portals: casual", or "Default" when every category is
        // untouched) -- not Valheim's own F2 overlay text, which would
        // need calling into ServerOptionsGUI's Unity-UI-coupled internals
        // (see WorldModifiersController's comment on why that was dropped).
        public string Summary { get; set; }
    }
}
