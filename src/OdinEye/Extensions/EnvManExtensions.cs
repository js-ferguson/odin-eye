namespace OdinEye.Extensions
{
    using System.Reflection;

    public static class EnvManExtensions
    {
        // EnvMan.IsDay()/IsNight() (and, empirically, likely IsAfternoon() too)
        // throw MissingMethodException at runtime on current Valheim builds
        // (l-1.0.12 confirmed) even though methods with those exact names and
        // signatures genuinely exist on EnvMan -- the compiled MethodRef tokens
        // from this project's stale ValheimGameLibs reference package don't
        // resolve against the live game assembly. GetCurrentDay(),
        // GetDayFraction(), etc. are unaffected, so this isn't a wholesale
        // EnvMan resolution failure -- see ODINEYE-5. Resolving these specific
        // members by name via reflection at call time sidesteps the stale
        // token instead of relying on it.
        private static readonly MethodInfo IsDayMethod = GetBoolMethod("IsDay");
        private static readonly MethodInfo IsNightMethod = GetBoolMethod("IsNight");
        private static readonly MethodInfo IsAfternoonMethod = GetBoolMethod("IsAfternoon");

        public static bool IsDaySafe(this EnvMan env) => Invoke(IsDayMethod, env);

        public static bool IsNightSafe(this EnvMan env) => Invoke(IsNightMethod, env);

        public static bool IsAfternoonSafe(this EnvMan env) => Invoke(IsAfternoonMethod, env);

        private static MethodInfo GetBoolMethod(string name) =>
            typeof(EnvMan).GetMethod(name, BindingFlags.Public | BindingFlags.Instance);

        private static bool Invoke(MethodInfo method, EnvMan env) =>
            method != null && (bool)method.Invoke(env, null);
    }
}
