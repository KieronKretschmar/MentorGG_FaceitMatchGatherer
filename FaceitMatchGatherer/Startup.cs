using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Database;
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
using RabbitCommunicationLib.Interfaces;
using RabbitCommunicationLib.Producer;
using RabbitCommunicationLib.Queues;
using RabbitCommunicationLib.TransferModels;

namespace FaceitMatchGatherer
{
    /// <summary>
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

            services.AddLogging(services =>
            {
                services.AddConsole(o =>
                {
                    o.TimestampFormat = "[yyyy-MM-dd HH:mm:ss zzz] ";
                });
                services.AddDebug();
            });

            #region database

            // if a connectionString is set use mysql, else use InMemory
            var connString = Configuration.GetValue<string>("MYSQL_CONNECTION_STRING");
            if (connString != null)
            {
                services.AddDbContext<Database.FaceitContext>(o => { o.UseMySql(connString); });
            }
            else
            {
                Console.WriteLine("WARNING: Using in memory database! Is `MYSQL_CONNECTION_STRING` set?");
                services.AddDbContext<Database.FaceitContext>( options =>
                    {
                        options.UseInMemoryDatabase(databaseName: "MyInMemoryDatabase");
                    });
            }
			#endregion

            if (Configuration.GetValue<bool>("IS_MIGRATING"))
            {
                Console.WriteLine("WARNING: Migrating!");
                return;
            }

            services.AddSingleton<IFaceitApiCommunicator, FaceitApiCommunicator>();
            services.AddSingleton<IFaceitOAuthCommunicator, FaceitOAuthCommunicator>();
            services.AddTransient<IFaceitMatchesWorker, FaceitMatchesWorker>();

            #region RabbitMQ

            var AMQP_URI = Configuration.GetValue<string>("AMQP_URI");
            if (AMQP_URI == null)
                throw new ArgumentException("AMQP_URI is missing, configure the `AMQP_URI` enviroment variable.");

            var AMQP_FACEIT_QUEUE = Configuration.GetValue<string>("AMQP_FACEIT_QUEUE");
            if (AMQP_FACEIT_QUEUE == null)
                throw new ArgumentException("AMQP_FACEIT_QUEUE is missing, configure the `AMQP_FACEIT_QUEUE` enviroment variable.");

            // Create producer
            var connection = new QueueConnection(AMQP_URI, AMQP_FACEIT_QUEUE);

            services.AddSingleton<IProducer<DemoInsertInstruction>>(sp =>
            {
                return new Producer<DemoInsertInstruction>(connection);
            });
            #endregion

            #region Swagger
            services.AddSwaggerGen(options =>
            {
                OpenApiInfo interface_info = new OpenApiInfo { Title = "FaceitMatchGatherer", Version = "v1", };
                options.SwaggerDoc("v1", interface_info);

                // Generate documentation based on the XML Comments provided.
                var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                options.IncludeXmlComments(xmlPath);

                // Optionally, if installed, enable annotations
                options.EnableAnnotations();
            });
            #endregion
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IServiceProvider services)
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

            #region Swagger
            // Add Swagger for API documentation
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "FaceitMatchGatherer");
            });
            #endregion

            // migrate if this is not an inmemory database
            if (services.GetRequiredService<FaceitContext>().Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory")
            {
                services.GetRequiredService<FaceitContext>().Database.Migrate();
            }

        }
    }
}
