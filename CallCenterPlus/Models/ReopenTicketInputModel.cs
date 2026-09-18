using System.ComponentModel.DataAnnotations;

namespace CallCenterPlus.Models;

public class ReopenTicketInputModel
{
    public int TicketId { get; set; }

    [Required(ErrorMessage = "Cuéntanos por qué quieres reabrir esta solicitud.")]
    public string Observaciones { get; set; } = string.Empty;

    // Display-only, populated on GET for the confirmation screen.
    public string RequestTypeName { get; set; } = string.Empty;
    public string StatusName { get; set; } = string.Empty;
}
