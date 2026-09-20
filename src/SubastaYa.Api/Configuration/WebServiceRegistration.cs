using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SubastaYa.Api.BackgroundJobs;
using SubastaYa.Api.Hubs;
using SubastaYa.Api.Security;
using SubastaYa.Application.Abstractions.RealTime;
using SubastaYa.Application.Abstractions.Security;
using SubastaYa.Infrastructure.Security;

namespace SubastaYa.Api.Configuration;

/// <summary>
/// Registra los servicios propios de la capa de presentación.
/// Se extrae de <c>Program</c> para que el arranque siga leyéndose de un vistazo a medida que la
/// configuración crece.
/// </summary>
public static class WebServiceRegistration
{
    public const string CorsPolicyName = "SubastaYaFrontend";

    public static IServiceCollection AddWebLayer(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddControllers()
            .AddJsonOptions(options => options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();

        // El contexto del hub es seguro para uso concurrente, de ahí que el notificador pueda
        // ser singleton aunque lo consuman servicios con alcance de petición.
        services.AddSignalR();
        services.AddSingleton<IAuctionNotifier, SignalRAuctionNotifier>();

        ConfigureCors(services, configuration);
        ConfigureBackgroundJobs(services, configuration);
        ConfigureAuthentication(services, configuration);
        ConfigureDocumentation(services);

        return services;
    }

    /// <summary>
    /// Registra el proceso que resuelve las subastas vencidas. Vive en el host de la API por
    /// comodidad de despliegue; la lógica que ejecuta pertenece a la capa de aplicación.
    /// </summary>
    /// <summary>
    /// Habilita el consumo desde el frontend. <c>AllowCredentials</c> es obligatorio para que
    /// SignalR pueda negociar la conexión, y obliga a enumerar los orígenes: con credenciales
    /// habilitadas el navegador rechaza un comodín.
    /// </summary>
    private static void ConfigureCors(IServiceCollection services, IConfiguration configuration)
    {
        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                             ?? new[] { "http://localhost:5173" };

        services.AddCors(options => options.AddPolicy(CorsPolicyName, policy => policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()));
    }

    private static void ConfigureBackgroundJobs(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AuctionWorkerOptions>(configuration.GetSection(AuctionWorkerOptions.SectionName));
        services.AddHostedService<AuctionClosingWorker>();
    }

    private static void ConfigureAuthentication(IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration
            .GetSection(JwtOptions.SectionName)
            .Get<JwtOptions>() ?? new JwtOptions();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(bearer =>
            {
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = options.Issuer,
                    ValidAudience = options.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Key)),

                    // Sin tolerancia de reloj: la expiración de la sesión debe ser exacta.
                    ClockSkew = TimeSpan.Zero
                };
            });

        services.AddAuthorization();
    }

    private static void ConfigureDocumentation(IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "SubastaYa API",
                Version = "v1",
                Description = "API REST de la plataforma de subastas en tiempo real SubastaYa."
            });

            // Habilita el botón "Authorize" de Swagger UI para probar los endpoints protegidos.
            var jwtScheme = new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Token JWT obtenido en POST /api/v1/sessions.",
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = JwtBearerDefaults.AuthenticationScheme
                }
            };

            options.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, jwtScheme);
            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                [jwtScheme] = Array.Empty<string>()
            });
        });
    }
}
