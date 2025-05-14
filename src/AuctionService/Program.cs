using AuctionService.Consumers;
using AuctionService.Data;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;

// using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddDbContext<AuctionDbContext>(opt =>
{
     //Configura Postgress
     opt.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
});

//Indica que AutoMapper debe buscar y registrar automáticamente todos los perfiles (Profile) definidos en los ensamblados actualmente cargados en el dominio de la aplicación.
//Esto significa que si tienes clases que heredan de Profile en cualquier parte de tu proyecto, AutoMapper las detectará y las usará automáticamente.
builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

//Open-source .NET library for building message-based distributed systems using message brokers like RabbitMQ
builder.Services.AddMassTransit(x =>
{
     //Agrega un Entity Framework Outbox a MassTransit para garantizar que los mensajes se envíen solo cuando la transacción de la base de datos se haya confirmado 
     // correctamente. Esto ayuda a evitar la duplicación de mensajes y garantiza la consistencia en sistemas distribuidos.
     x.AddEntityFrameworkOutbox<AuctionDbContext>(o =>
     {
          o.QueryDelay = TimeSpan.FromSeconds(10);
          o.UsePostgres();
          o.UseBusOutbox();
     });

     x.AddConsumersFromNamespaceContaining<AuctionCreatedFaultConsumer>();
     x.SetEndpointNameFormatter(new KebabCaseEndpointNameFormatter("auction", false));

     //Configuración inicial para indicarle a MassTransit que usaremos RabbitMq
     x.UsingRabbitMq((context, cfg) =>
     {
          cfg.ConfigureEndpoints(context);
     });
});
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
         options.Authority = builder.Configuration["IdentityServiceUrl"];
         options.RequireHttpsMetadata = false;
         options.TokenValidationParameters.ValidateAudience = false;
         options.TokenValidationParameters.NameClaimType = "username";
    });



var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

try
{
     DbInitializer.InitDb(app);
}
catch (Exception ex)
{
     Console.WriteLine(ex.Message);
}

app.Run();
