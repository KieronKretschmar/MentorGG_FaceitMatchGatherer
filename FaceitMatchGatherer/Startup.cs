using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Database;
using FaceitMatchGatherer.Middleware;
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

                services.AddDbContext<Database.FaceitContext>(o => 
                {
                    o.UseMySql(
                        connString,
                        options =>
                        {
                            options.EnableRetryOnFailure();
                        });
                }, ServiceLifetime.Transient, ServiceLifetime.Transient);
            }
            else
            {
                Console.WriteLine("WARNING: Using in memory database! Is `MYSQL_CONNECTION_STRING` set?");
                services.AddDbContext<Database.FaceitContext>(options =>
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


            string MENTORINTERFACE_BASE_ADDRESS = GetRequiredEnvironmentVariable<string>(Configuration, "MENTORINTERFACE_BASE_ADDRESS");
            int MATCHES_LOOKER_MAX_USERS = GetOptionalEnvironmentVariable<int>(Configuration, "MATCHES_LOOKER_MAX_USERS", 20);
            TimeSpan MATCHES_LOOKER_PERIOD_DAYS = TimeSpan.FromDays(GetOptionalEnvironmentVariable<double>(Configuration, "MATCHES_LOOKER_PERIOD_DAYS", 7));
            TimeSpan MATCHES_LOOKER_ACTIVITY_TIMESPAN = TimeSpan.FromDays(GetOptionalEnvironmentVariable<double>(Configuration, "MATCHES_LOOKER_ACTIVITY_TIMESPAN", 21));

            services.AddSingleton<IFaceitApiCommunicator, FaceitApiCommunicator>();
            services.AddSingleton<IFaceitOAuthCommunicator, FaceitOAuthCommunicator>();
            services.AddTransient<IFaceitMatchesWorker, FaceitMatchesWorker>();

            //services.AddTransient<IMatchLooker>(services =>
            //{
            //    return new MatchLooker(MATCHES_LOOKER_ACTIVITY_TIMESPAN, MATCHES_LOOKER_MAX_USERS, services.GetRequiredService<ILogger<MatchLooker>>(), services.GetRequiredService<FaceitContext>(), services.GetRequiredService<FaceitMatchesWorker>());
            //});
            //services.AddTransient<IPeriodicMatchLooker>(services =>
            //{
            //    return new PeriodicMatchLooker(MATCHES_LOOKER_PERIOD_DAYS, services.GetRequiredService<IMatchLooker>(), services.GetRequiredService<ILogger<PeriodicMatchLooker>>());
            //});

            services.AddHttpClient("mentor-interface", c =>
            {
                c.BaseAddress = new Uri(MENTORINTERFACE_BASE_ADDRESS);
            });
            services.AddTransient<IUserIdentityRetriever, UserIdentityRetriever>();

            #region RabbitMQ

            var AMQP_URI = GetRequiredEnvironmentVariable<string>(Configuration, "AMQP_URI");

            var AMQP_FACEIT_QUEUE = GetRequiredEnvironmentVariable<string>(Configuration, "AMQP_FACEIT_QUEUE");

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

            app.UseMiddleware(typeof(ErrorHandlingMiddleware));

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

        /// <summary>
        /// Attempt to retrieve an Environment Variable
        /// Throws ArgumentNullException is not found.
        /// </summary>
        /// <typeparam name="T">Type to retreive</typeparam>
        private static T GetRequiredEnvironmentVariable<T>(IConfiguration config, string key)
        {
            T value = config.GetValue<T>(key);
            if (value == null)
            {
                throw new ArgumentNullException(
                    $"{key} is missing, Configure the `{key}` environment variable.");
            }
            else
            {
                return value;
            }
        }

        /// <summary>
        /// Attempt to retrieve an Environment Variable
        /// Returns default value if not found.
        /// </summary>
        /// <typeparam name="T">Type to retreive</typeparam>
        private static T GetOptionalEnvironmentVariable<T>(IConfiguration config, string key, T defaultValue)
        {
            var stringValue = config.GetSection(key).Value;
            try
            {
                T value = (T)Convert.ChangeType(stringValue, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
                return value;
            }
            catch (InvalidCastException e)
            {
                Console.WriteLine($"Env var [ {key} ] not specified. Defaulting to [ {defaultValue} ]");
                return defaultValue;
            }
        }
    }
}
