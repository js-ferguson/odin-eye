namespace OdinEye.Models.Api
{
    // Minimal 200 OK body for POST /players/{id}/stats -- OdinEye's server
    // side is a stateless relay here (see ODINEYE-12), so there's nothing
    // richer to report back than "accepted".
    public class AcceptedResponse
    {
        public bool Accepted { get; set; } = true;
    }
}
