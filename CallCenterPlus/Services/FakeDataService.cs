using CallCenterPlus.Models;

namespace CallCenterPlus.Services;

/// <summary>
/// In-memory demo data for tickets. The employee lookup and the service-area
/// catalog now come from the real Core repositories (Employees, ServiceAreas);
/// only the ticket data remains fake until the real backend is connected.
/// </summary>
public class FakeDataService : IFakeDataService
{
    // Demo ticket data keyed by EmployeeIdNumber. Update these numbers to match
    // real employees in your Employee table if you want to see sample tickets.
    private static readonly List<FakeTicket> _tickets = new()
    {
        new FakeTicket
        {
            Number = "TCK-2026-000101",
            EmployeeIdNumber = 1001,
            RequestTypeName = "Equipo lento o congelado",
            RequestTypeIcon = "bi-cpu",
            Description = "Mi computadora tarda mucho en abrir Excel y se congela varias veces al día.",
            Status = "Resuelto",
            CreatedAt = DateTime.Now.AddDays(-9),
            UpdatedAt = DateTime.Now.AddDays(-6),
            AssignedTechnician = "Rafael Núñez",
            MinutesSpent = 45,
            Comments = new()
            {
                new TicketComment { Author = "Rafael Núñez", Date = DateTime.Now.AddDays(-8), Text = "Buen día, reviso el equipo de forma remota esta tarde.", IsTechnician = true },
                new TicketComment { Author = "Rafael Núñez", Date = DateTime.Now.AddDays(-6), Text = "Se liberó espacio en disco y se desactivaron programas de inicio innecesarios. Equipo funcionando con normalidad.", IsTechnician = true },
            }
        },
        new FakeTicket
        {
            Number = "TCK-2026-000114",
            EmployeeIdNumber = 1001,
            RequestTypeName = "Correo electrónico",
            RequestTypeIcon = "bi-envelope",
            Description = "No me están llegando los correos de un proveedor específico.",
            Status = "En Proceso",
            CreatedAt = DateTime.Now.AddDays(-2),
            UpdatedAt = DateTime.Now.AddHours(-5),
            AssignedTechnician = "Yohanna Suárez",
            MinutesSpent = 20,
            Comments = new()
            {
                new TicketComment { Author = "Yohanna Suárez", Date = DateTime.Now.AddHours(-5), Text = "Estamos revisando las reglas de filtrado y la lista de remitentes bloqueados.", IsTechnician = true },
            }
        },
        new FakeTicket
        {
            Number = "TCK-2026-000129",
            EmployeeIdNumber = 1001,
            RequestTypeName = "Restablecer contraseña",
            RequestTypeIcon = "bi-key",
            Description = "Necesito restablecer la contraseña del sistema de nómina.",
            Status = "Abierto",
            CreatedAt = DateTime.Now.AddHours(-3),
            UpdatedAt = DateTime.Now.AddHours(-3),
            AssignedTechnician = null,
            MinutesSpent = null,
            Comments = new()
        },
        new FakeTicket
        {
            Number = "TCK-2026-000087",
            EmployeeIdNumber = 1002,
            RequestTypeName = "Accesos y permisos de sistema",
            RequestTypeIcon = "bi-shield-lock",
            Description = "Requiero acceso al módulo de reportes del sistema de RRHH.",
            Status = "Cerrado",
            CreatedAt = DateTime.Now.AddDays(-20),
            UpdatedAt = DateTime.Now.AddDays(-18),
            AssignedTechnician = "Rafael Núñez",
            MinutesSpent = 15,
            Comments = new()
            {
                new TicketComment { Author = "Rafael Núñez", Date = DateTime.Now.AddDays(-18), Text = "Acceso otorgado y verificado con el usuario. Se cierra el ticket.", IsTechnician = true },
                new TicketComment { Author = "María Rodríguez", Date = DateTime.Now.AddDays(-18), Text = "Confirmado, ya puedo ver los reportes. ¡Gracias!", IsTechnician = false },
            }
        },
        new FakeTicket
        {
            Number = "TCK-2026-000142",
            EmployeeIdNumber = 1002,
            RequestTypeName = "Impresora o escáner",
            RequestTypeIcon = "bi-printer",
            Description = "La impresora del segundo piso no escanea a color.",
            Status = "Resuelto",
            CreatedAt = DateTime.Now.AddDays(-4),
            UpdatedAt = DateTime.Now.AddDays(-3),
            AssignedTechnician = "Yohanna Suárez",
            MinutesSpent = 30,
            Comments = new()
            {
                new TicketComment { Author = "Yohanna Suárez", Date = DateTime.Now.AddDays(-3), Text = "Se reinstaló el driver de escaneo a color. Ya está operativa.", IsTechnician = true },
            }
        },
        new FakeTicket
        {
            Number = "TCK-2026-000155",
            EmployeeIdNumber = 1003,
            RequestTypeName = "Conexión a internet o red",
            RequestTypeIcon = "bi-wifi",
            Description = "La conexión wifi se cae constantemente en la sala de ventas.",
            Status = "Abierto",
            CreatedAt = DateTime.Now.AddHours(-8),
            UpdatedAt = DateTime.Now.AddHours(-8),
            AssignedTechnician = null,
            MinutesSpent = null,
            Comments = new()
        },
        new FakeTicket
        {
            Number = "TCK-2026-000163",
            EmployeeIdNumber = 1004,
            RequestTypeName = "Solicitud de equipo o accesorio",
            RequestTypeIcon = "bi-laptop",
            Description = "Se necesita un mouse y teclado inalámbrico para el puesto de soporte nivel 2.",
            Status = "En Proceso",
            CreatedAt = DateTime.Now.AddDays(-1),
            UpdatedAt = DateTime.Now.AddHours(-2),
            AssignedTechnician = "Rafael Núñez",
            MinutesSpent = 10,
            Comments = new()
            {
                new TicketComment { Author = "Rafael Núñez", Date = DateTime.Now.AddHours(-2), Text = "Solicitud de compra enviada a almacén, en espera de disponibilidad.", IsTechnician = true },
            }
        },
    };

    public List<FakeTicket> GetTicketsByEmployeeIdNumber(int employeeIdNumber) =>
        _tickets
            .Where(t => t.EmployeeIdNumber == employeeIdNumber)
            .OrderByDescending(t => t.CreatedAt)
            .ToList();

    public FakeTicket? GetTicketByNumber(string number) =>
        _tickets.FirstOrDefault(t => t.Number.Equals(number, StringComparison.OrdinalIgnoreCase));

    public string GenerateTicketNumber()
    {
        var sequence = Random.Shared.Next(1000, 9999);
        return $"TCK-{DateTime.Now:yyyy}-{sequence:000000}";
    }
}
