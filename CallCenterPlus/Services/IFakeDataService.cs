using CallCenterPlus.Models;

namespace CallCenterPlus.Services;

public interface IFakeDataService
{
    List<FakeTicket> GetTicketsByEmployeeIdNumber(int employeeIdNumber);
    FakeTicket? GetTicketByNumber(string number);
    string GenerateTicketNumber();
}
