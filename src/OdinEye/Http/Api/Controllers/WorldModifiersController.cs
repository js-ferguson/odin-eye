namespace OdinEye.Http.Api.Controllers
{
    using Extensions;
    using Models.Api;
    using System;
    using System.Collections.Generic;
    using WebSocketSharp.Server;

    // Read-only report of the world's CURRENT difficulty modifiers --
    // ODINEYE-23. Writing them is handled entirely separately, by
    // Tristan/ValheimRcon's `consoleCommand` passthrough calling Valheim's
    // own `setworldmodifier`/`setworldpreset` console commands -- this
    // endpoint exists purely because nothing in that plugin's command set
    // reads current values back, and OdinEye (already in-process) can
    // read them directly off the game's own World object instead.
    //
    // `World.GetModifier`/`GetWorldModifierSummary`'s exact signatures are
    // unconfirmed without a real build -- this project has no local build
    // tooling (old-style .csproj, Windows/Unity managed-assembly
    // references), only `build-pr.yaml`'s Windows CI. If this doesn't
    // compile as written, that's the actual verification step surfacing a
    // wrong guess at the API shape, not a sign the underlying approach
    // (read World's own live modifier state, not RCON) is wrong -- see
    // ODINEYE-23's own description for the `strings`/`monodis` evidence
    // this was based on (confirmed: World, WorldModifiers,
    // WorldModifierOption types and GetModifier/GetModifiers/m_modifiers/
    // GetWorldModifierSummary member names all exist in the assembly;
    // NOT confirmed: which type each member actually lives on, or exact
    // parameter/return types).
    public class WorldModifiersController : IController
    {
        public string Route => "/worldModifiers";

        public void OnGet(HttpRequestEventArgs requestArguments)
        {
            var world = ZNet.m_world;
            var modifiers = new List<WorldModifierValue>();
            string summary = null;

            if (world != null)
            {
                // Enumerate the game's own category enum at runtime rather
                // than hardcoding which categories exist -- keeps this
                // correct even if the exact member list (combat,
                // deathpenalty, resources, raids, portals, playerevents,
                // ...) isn't quite what research assumed.
                foreach (global::WorldModifiers category in Enum.GetValues(typeof(global::WorldModifiers)))
                {
                    modifiers.Add(new WorldModifierValue
                    {
                        Category = category.ToString(),
                        Value = world.GetModifier(category).ToString()
                    });
                }

                // Matches whatever text Valheim's own F2 overlay shows --
                // a low-risk fallback alongside the structured list above.
                summary = world.GetWorldModifierSummary();
            }

            requestArguments.Response.Ok(new WorldModifiersDetails
            {
                Modifiers = modifiers,
                Summary = summary
            });
        }
    }
}
