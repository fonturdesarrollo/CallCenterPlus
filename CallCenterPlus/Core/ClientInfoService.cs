namespace CallCenterPlus.Core
{
	using CallCenterPlus.Models;
	using UAParser;

	public class ClientInfoService
	{
		private readonly IHttpContextAccessor _httpContextAccessor;
		private readonly Parser _uaParser;

		public ClientInfoService(IHttpContextAccessor httpContextAccessor)
		{
			_httpContextAccessor = httpContextAccessor;
			_uaParser = Parser.GetDefault();
		}

		public ClientDetails GetClientDetails()
		{
			var userAgent = _httpContextAccessor.HttpContext?.Request.Headers["User-Agent"].ToString();
			if (string.IsNullOrEmpty(userAgent))
			{
				return new ClientDetails { DeviceType = "Desconocido" };
			}

			ClientInfo clientInfo = _uaParser.Parse(userAgent);

			string deviceType;

			if (clientInfo.Device.Family.Contains("iPad") || clientInfo.Device.Family.Contains("Tablet"))
			{
				deviceType = "Tableta";
			}

			else if (clientInfo.Device.Family != "Other")
			{
				deviceType = "Móvil";
			}
			else
			{
				deviceType = "Escritorio";
			}

			return new ClientDetails
			{
				Browser = $"{clientInfo.UA.Family} {clientInfo.UA.Major}",
				OperatingSystem = $"{clientInfo.OS.Family}",
				DeviceType = deviceType
			};
		}
	}
}
