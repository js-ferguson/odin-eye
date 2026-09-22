namespace OdinEye.Client.Stats
{
    using System.Collections.Generic;

    // ODINEYE-38: which pieces are crafting stations or station upgrades
    // (workbench, forge, chopping block, tanning rack, forge extensions...).
    // Found by looking at what the game's own prefabs are, not from a list
    // kept here, so a station added by a future update counts without a code
    // change. Read once the game world has loaded, then cached.
    public static class StationNames
    {
        private static HashSet<string> cached;

        public static ISet<string> Get()
        {
            if (cached != null)
            {
                return cached;
            }

            var scene = ZNetScene.instance;
            if (scene == null || scene.m_prefabs == null || scene.m_prefabs.Count == 0)
            {
                return null; // not loaded yet: try again next time
            }

            var names = new HashSet<string>();
            foreach (var prefab in scene.m_prefabs)
            {
                if (prefab == null)
                {
                    continue;
                }

                var piece = prefab.GetComponent<Piece>();
                if (piece != null && !string.IsNullOrEmpty(piece.m_name) &&
                    (prefab.GetComponent<CraftingStation>() != null || prefab.GetComponent<StationExtension>() != null))
                {
                    names.Add(piece.m_name);
                }
            }

            if (names.Count > 0)
            {
                cached = names;
            }

            return names.Count > 0 ? names : null;
        }
    }
}
