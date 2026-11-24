// BoltWebControl — placeholder, wordt ingevuld na BoltCatControl/BoltCat.Core
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
app.MapGet("/", () => "BoltWebControl — coming soon");
app.Run();
