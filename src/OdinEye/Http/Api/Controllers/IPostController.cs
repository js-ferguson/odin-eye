namespace OdinEye.Http.Api.Controllers
{
    using WebSocketSharp.Server;

    // Separate from IController (GET-only, exact-match routing) rather than
    // extending it -- this is the plugin's first mutating/parameterized
    // route, and reusing IController's plain string Route equality check
    // can't express a path segment. RoutePrefix/RouteSuffix keeps matching
    // just as simple (no regex/routing framework) while allowing exactly
    // one path parameter between them.
    public interface IPostController
    {
        string RoutePrefix { get; }
        string RouteSuffix { get; }
        void OnPost(HttpRequestEventArgs requestArguments, string routeParameter);
    }
}
