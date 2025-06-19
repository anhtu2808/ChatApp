
using Microsoft.AspNetCore.Http.Features;

namespace ChatServer
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddSignalR();
            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            builder.Logging.SetMinimumLevel(LogLevel.Debug);
            builder.WebHost.UseUrls("http://0.0.0.0:5262", "https://0.0.0.0:7165");
            builder.Services.Configure<FormOptions>(options =>
            {
                options.MultipartBodyLengthLimit = 1000000000;
            });

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }
            app.MapHub<ChatHub>("/chathub");
            app.Use(async (context, next) =>
            {
                context.Features.Get<IHttpMaxRequestBodySizeFeature>().MaxRequestBodySize = null;
                await next();
            });
            app.UseAuthorization();
            app.UseStaticFiles(); 
            app.MapControllers();

            app.Run();
        }
    }
}
