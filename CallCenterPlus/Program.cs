using CallCenterPlus.Core;
using CallCenterPlus.Core.Data;
using CallCenterPlus.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var mvcBuilder = builder.Services.AddControllersWithViews(options =>
{
	options.ModelBindingMessageProvider.SetValueMustNotBeNullAccessor(
		value => "Este campo es requerido.");
	options.ModelBindingMessageProvider.SetMissingKeyOrValueAccessor(
		() => "Este campo es requerido.");
	options.ModelBindingMessageProvider.SetValueIsInvalidAccessor(
		value => "Este valor no es válido.");
	options.ModelBindingMessageProvider.SetAttemptedValueIsInvalidAccessor(
		(value, field) => $"El valor '{value}' no es válido.");
});
if (builder.Environment.IsDevelopment())
{
	mvcBuilder.AddRazorRuntimeCompilation();
}
builder.Services.AddScoped<ISqlConnectionFactory, SqlConnectionFactory>();
builder.Services.AddScoped<IEmployees, Employees>();
builder.Services.AddScoped<IServiceAreas, ServiceAreas>();
builder.Services.AddScoped<ITickets, Tickets>();
builder.Services.AddScoped<IGeography, Geography>();
builder.Services.AddScoped<ISecurity, Security>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSignalR();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
	options.IdleTimeout = TimeSpan.FromMinutes(30);
	options.Cookie.HttpOnly = true;
	options.Cookie.IsEssential = true;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
	app.UseExceptionHandler("/Home/Error");
}
app.UseRouting();

app.UseSession();

app.UseAuthorization();

app.UseStaticFiles();

app.MapControllerRoute(
	name: "default",
	pattern: "{controller=Account}/{action=Login}/{id?}");

app.MapHub<TicketsHub>("/hubs/tickets");


app.Run();
