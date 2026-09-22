namespace OdinEye.Events
{
    using System.Collections.Generic;
    using System.Linq;

    // ODINEYE-31: the structured details of an EnemyKilled event, so the
    // achievements engine can count "solo" and "2-star" kills without
    // parsing a message string. Pure (no game types) so it can be tested.
    //
    // Solo means exactly ONE player is credited on the creature. Vanilla
    // flags a player as an attacker (ZDOVars.s_attackers + player name) on
    // every hit they land, so this is "only one player damaged it".
    // Level is 1-indexed as Valheim's Character.GetLevel(): 1 = no stars,
    // 2 = one star, 3 = two stars.
    public static class EnemyKillDetails
    {
        public static Dictionary<string, object> Build(string enemyName, int level, bool isBoss, IEnumerable<string> attackers)
        {
            var names = (attackers ?? Enumerable.Empty<string>()).Where(n => !string.IsNullOrEmpty(n)).Distinct().ToList();
            return new Dictionary<string, object>
            {
                ["Enemy"] = enemyName,
                ["Level"] = level,
                ["Boss"] = isBoss,
                ["Attackers"] = names,
                ["Solo"] = names.Count == 1
            };
        }
    }
}
