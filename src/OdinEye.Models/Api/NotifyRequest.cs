namespace OdinEye.Models.Api
{
    // The POST body for /players/{steamId}/notify (ODINEYE-33) -- a short
    // HUD message shown to exactly one currently-connected player, e.g.
    // valheim_server's "your sub is due" nudge.
    public class NotifyRequest
    {
        public string Message { get; set; }
    }
}
