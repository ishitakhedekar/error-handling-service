using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using LogAnalysisApi; 
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc; 
using Microsoft.AspNetCore.Mvc.NewtonsoftJson;

var builder = WebApplication.CreateBuilder(args);

// Set the URL for the web host
builder.WebHost.UseUrls("http://localhost:5262");

// Add controllers with NewtonsoftJson support
builder.Services.AddControllers()
    .AddNewtonsoftJson();

// Register services
builder.Services.AddSingleton<LogAnalysisService>(); 

// Register the ErrorRatioCheckService as a hosted service
builder.Services.AddSingleton<ErrorRatioCheckService>(); 

// Register the ErrorRatioPredictor if needed
builder.Services.AddSingleton<ErrorRatioPredictor>();

// Configure EmailSettings from appsettings.json
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));

// Register IEmailService with its implementation
builder.Services.AddSingleton<IEmailService>(sp =>
{
    var emailSettings = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<EmailSettings>>().Value;
    var logger = sp.GetRequiredService<ILogger<EmailService>>();
    return new EmailService(emailSettings.SmtpServer, emailSettings.SmtpPort, emailSettings.FromEmail, emailSettings.FromPassword, logger);
});

// Configure CORS policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", builder =>
    {
        builder.WithOrigins("http://localhost:3000")
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});

var app = builder.Build();

// Use CORS policy
app.UseCors("AllowReactApp");

// Configure middleware
app.UseRouting();
app.UseAuthorization();

// Map controllers
app.MapControllers();

// Run the application
app.Run();
