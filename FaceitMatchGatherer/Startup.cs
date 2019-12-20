using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using RabbitTransfer.Interfaces;
using RabbitTransfer.Producer;
using RabbitTransfer.Queues;
using RabbitTransfer.TransferModels;

namespace FaceitMatchGatherer
{
    /// <summary>
    /// 
    /// Requires environment variables: ["MYSQL_CONNECTION_STRING", "AMQP_URI", "AMQP_FACEIT_QUEUE"]
    /// </summary>
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();
            services.AddLogging(x => x.AddConsole().AddDebug());

            services.AddSingleton<IFaceitApiCommunicator, FaceitApiCommunicator>();
            services.AddSingleton<IFaceitOAuthCommunicator, FaceitOAuthCommunicator>();
            services.AddTransient<IFaceitMatchesWorker, FaceitMatchesWorker>();


            // Create producer
            var connection = new QueueConnection(
                Configuration.GetValue<string>("AMQP_URI"),
                Configuration.GetValue<string>("AMQP_FACEIT_QUEUE"));

            services.AddSingleton<IProducer<GathererTransferModel>>(sp =>
            {
                return new Producer<GathererTransferModel>(connection);
            });

            // if a connectionString is set use mysql, else use InMemory
            var connString = Configuration.GetValue<string>("MYSQL_CONNECTION_STRING");
            if (connString != null)
            {
                services.AddDbContext<Database.FaceitContext>(o => { o.UseMySql(connString); });
            }
            else
            {
                services.AddEntityFrameworkInMemoryDatabase()
                    .AddDbContext<Database.FaceitContext>((sp, options) =>
                    {
                        options.UseInMemoryDatabase(databaseName: "MyInMemoryDatabase").UseInternalServiceProvider(sp);
                    });
            }

            // Add Swagger for API documentation
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "FaceitMatchGatherer API",
                    Version = "v1"
                });
                
            });

        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });

            // Add Swagger for API documentation
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "Example API v1");
            });
        }
    }
}
