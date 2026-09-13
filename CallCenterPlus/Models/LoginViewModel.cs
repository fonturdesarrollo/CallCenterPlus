using System.ComponentModel.DataAnnotations;

namespace CallCenterPlus.Models;

public class LoginViewModel
{
    [Required(ErrorMessage = "Debes ingresar tu número de cédula.")]
    [Display(Name = "Número de cédula")]
    public string NationalId { get; set; } = string.Empty;
}
