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

        private IEnumerable<IController> controllers = new IController[]
        {
            new PlayersController(),
            new ServerDetailsController(),
            new WorldDetailsController(),
            new BossDetailsController(),
            new CharacterStatsController()
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
                    if (string.Equals(controller.Route, args.Request.RawUrl))
                    {
                        try
                        {
                            controller.OnGet(args);
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