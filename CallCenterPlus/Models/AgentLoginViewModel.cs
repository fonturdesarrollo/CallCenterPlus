using System.ComponentModel.DataAnnotations;

namespace CallCenterPlus.Models;

public class AgentLoginViewModel
{
    [Required(ErrorMessage = "Ingresa tu usuario.")]
    [Display(Name = "Usuario")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingresa tu contraseña.")]
    [Display(Name = "Contraseña")]
    public string Password { get; set; } = string.Empty;
}
