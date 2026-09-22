namespace OdinEye.Http
{
    using Api.Controllers;
    using Extensions;
    using Logging;
    using ProtoBuf;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using WebSockets;
    using WebSocketSharp.Server;

    public class HttpWebServer
    {
        private const string DefaultWebSocketPath = "/activity";
        private readonly HttpServer httpServer;
        private readonly ILogger logger;
        private WebSocketSessionManager defaultWebSocketSessionManager;

        // object, not IController -- every controller so far has
        // implemented IController (a GET route) alongside IPostController
        // where it also accepts POSTs, but ODINEYE-33's
        // PlayerNotifyController is the first POST-only controller (there's
        // nothing to GET back), so this list can no longer be typed to the
        // GET-only interface. Both loops below already check "is
        // IController"/"is IPostController" per entry rather than assuming
        // every entry is both.
        private IEnumerable<object> controllers = new object[]
        {
            new PlayersController(),
            new ServerDetailsController(),
            new WorldDetailsController(),
            new BossDetailsController(),
            new WorldModifiersController(),
            new CharacterStatsController(),
            new CheatStatusController(),
            new PlayerNotifyController(),
            new EventsController(),
            new PlayersMetaController(),
            new PlayerEventsController()
        };

        public HttpWebServer(string address, ILogger logger)
        {
            this.logger = logger;
            logger.LogInfo($"Starting Http Server at {address}");

            httpServer = new HttpServer(address);
            ConfigureHttpApi(); // maybe move this to a later stage like OnGameWorldLoaded
            ConfigureWebSocketServices();
            httpServer.Start();
        }

        private void ConfigureHttpApi()
        {
            httpServer.OnGet += (sender, args) =>
            {
                foreach (var controller in controllers)
                {
                    if (!(controller is IController getController))
                    {
                        continue;
                    }

                    if (string.Equals(getController.Route, RoutePath(args.Request.RawUrl)))
                    {
                        try
                        {
                            getController.OnGet(args);
                        }
                        catch (Exception ex)
                        {
                            logger.LogError($"Unhandled exception in {controller.GetType().Name} for {args.Request.RawUrl}: {ex}");
                            args.Response.Error();
                        }

                        break;
                    }
                }
            };

            httpServer.OnPost += (sender, args) =>
            {
                foreach (var controller in controllers)
                {
                    if (!(controller is IPostController postController))
                    {
                        continue;
                    }

                    var rawUrl = args.Request.RawUrl;
                    var minimumLengthForNonEmptyParameter = postController.RoutePrefix.Length + postController.RouteSuffix.Length;
                    if (rawUrl.Length <= minimumLengthForNonEmptyParameter ||
                        !rawUrl.StartsWith(postController.RoutePrefix) ||
                        !rawUrl.EndsWith(postController.RouteSuffix))
                    {
                        continue;
                    }

                    var routeParameter = rawUrl.Substring(
                        postController.RoutePrefix.Length,
                        rawUrl.Length - postController.RoutePrefix.Length - postController.RouteSuffix.Length);

                    try
                    {
                        postController.OnPost(args, routeParameter);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError($"Unhandled exception in {controller.GetType().Name} for {args.Request.RawUrl}: {ex}");
                        args.Response.Error();
                    }

                    break;
                }
            };
        }
        
        // The request path without its query string, so a route like "/events"
        // still matches "/events?after=12&limit=500". Existing routes take no
        // query, so they are unaffected.
        internal static string RoutePath(string rawUrl)
        {
            var queryStart = rawUrl?.IndexOf('?') ?? -1;
            return queryStart < 0 ? rawUrl : rawUrl.Substring(0, queryStart);
        }

        private void ConfigureWebSocketServices()
        {
            httpServer.AddWebSocketService<ActivityWebSocketService>(DefaultWebSocketPath);
            httpServer.KeepClean = true;
            defaultWebSocketSessionManager = httpServer.WebSocketServices[DefaultWebSocketPath].Sessions;
        }

        public void BroadcastWebSocketMessage<TInstance>(TInstance instance)
        {
            using (var stream = new MemoryStream())
            {
                Serializer.Serialize(stream, instance);
                stream.Seek(0, SeekOrigin.Begin);
                defaultWebSocketSessionManager.Broadcast(stream, (int)stream.Length);
            }
        }
    }
}