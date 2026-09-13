using System.ComponentModel.DataAnnotations;

namespace CallCenterPlus.Models;

public class NewRequestViewModel
{
    [Required(ErrorMessage = "Selecciona el tipo de solicitud.")]
    public string RequestTypeId { get; set; } = string.Empty;

    [StringLength(600, ErrorMessage = "La descripción no puede superar los 600 caracteres.")]
    public string? Description { get; set; } = string.Empty;

    public List<ServiceArea> AvailableServiceAreas { get; set; } = new();

    public bool ServiceAreasLoadFailed { get; set; }
}
