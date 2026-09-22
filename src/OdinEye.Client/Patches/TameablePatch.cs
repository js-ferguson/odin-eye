namespace OdinEye.Client.Patches
{
    using HarmonyLib;
    using OdinEye.Client.Counters;

    // ODINEYE-38: Farmer Joe. Tameable.Tame() (private) is where the game
    // itself finalizes a tame -- confirmed via IL it already runs only on
    // the taming creature's ZDO owner (gated by m_nview.IsOwner() inside
    // the method), the same ownership shape already accepted for
    // CharacterDeathPatch/EnemyKilled's kill attribution. Whichever
    // client's game process actually runs this postfix is, by definition,
    // this mod instance's own local player, so no extra "is this me" check
    // is needed the way pickup/eat/hit patches need one.
    //
    // The species is read from the tamed Character's prefab name (via
    // assembly_utils' Utils.GetPrefabName(GameObject), the same helper the
    // game's own code uses to turn a GameObject into its prefab's string
    // name) rather than any display name -- that's the same raw token the
    // game's own EnemyKill:<name> stat already keys off (Fuck the Police /
    // Mangey Dog use "Boar"/"Wolf" directly), so this sidesteps
    // localization entirely and stays consistent with those.
    [HarmonyPatch(typeof(Tameable), "Tame")]
    public static class TameablePatch
    {
        [HarmonyPostfix]
        private static void Postfix(Character ___m_character) =>
            ClientRuntime.Guard("tame count", () =>
            {
                if (ClientRuntime.Counters == null || ___m_character == null)
                {
                    return;
                }

                var prefabName = Utils.GetPrefabName(___m_character.gameObject);
                var counterKey = CounterRules.TamedSpeciesCounterKey(prefabName);
                if (counterKey != null)
                {
                    ClientRuntime.Counters.Increment(counterKey);
                }
            });
    }
}
