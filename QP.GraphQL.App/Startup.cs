using GraphQL;
using GraphQL.DataLoader;
using GraphQL.Execution;
using GraphQL.SystemTextJson;
using GraphQL.Types;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using QP.GraphQL.App.Schema;
using QP.GraphQL.App.Types;
using QP.GraphQL.DAL;
using QP.GraphQL.Interfaces.Articles;
using QP.GraphQL.Interfaces.DAL;
using QP.GraphQL.Interfaces.Metadata;
using System.Data.Common;
using System.Text.Json.Serialization;
using QA.DotNetCore.Caching.Configuration;

namespace QP.GraphQL.App
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            // add graphql execution components
            services.AddSingleton<IDocumentExecuter, DocumentExecuter>();
            services.AddSingleton<IDocumentWriter, DocumentWriter>();
            services.AddSingleton<IErrorInfoProvider>(services =>
            {
                var settings = services.GetRequiredService<IOptions<GraphQLSettings>>();
                return new ErrorInfoProvider(new ErrorInfoProviderOptions { ExposeExceptionStackTrace = settings.Value.ExposeExceptions });
            });
            services.Configure<GraphQLSettings>(Configuration);

            // setup graphql dataloaders
            services.AddSingleton<IDataLoaderContextAccessor, DataLoaderContextAccessor>();
            services.AddSingleton<DataLoaderDocumentListener>();

            // qp dal
            services.Configure<ConnectionSettings>(Configuration.GetConnectionSection());
            services.Configure<QpArticlesAccessorSettings>(Configuration.GetSection("QpArticlesAccessor"));
            services.AddSingleton<IConnectionFactory, ConnectionFactory>();
            services.AddTransient<DbConnection>(s => s.GetRequiredService<IConnectionFactory>().GetConnection());
            services.AddTransient<IQpMetadataAccessor, QpMetadataAccessor>();

            if (Configuration.GetDatabaseType() == DatabaseType.Postgres)
            {
                services.AddTransient<IQueryService, QueryServicePostgres>();
                services.AddTransient<IQpArticlesAccessor, QpArticlesAccessorPostgres>();
            }
            else if(Configuration.GetDatabaseType() == DatabaseType.SqlServer)
            {
                services.AddTransient<IQueryService, QueryServiceSqlServer>();
                services.AddTransient<IQpArticlesAccessor, QpArticlesAccessorSqlServer>();
            }

            // add schema
            services.AddSingleton<TimeGraphType>();
            services.Configure<SchemaSettings>(Configuration);
            services.AddTransient<QpContentsSchemaDynamic>();
            services.AddSingleton<ISchemaFactory, SchemaFactory>();
            services.AddTransient<ISchema, SchemaDecorator>();
            services.AddTransient<IQpMetadataValidator, QpMetadataValidator>();

            if (Configuration.SchemaAutoReload())
            {
                services.AddHostedService<SchemaBackgroundService>();
            }

            services.TryAddMemoryCacheServices();

            // setup cors
            var origins = Configuration.GetSection("CorsOrigins").Get<string[]>();
            services.AddCors(options =>
            {
                options.AddDefaultPolicy(
                    builder =>
                    {
                        if (origins == null)
                        {
                            builder.AllowAnyOrigin();
                        }
                        else
                        {
                            builder.WithOrigins(origins);
                        }

                        builder.AllowAnyMethod()
                        .AllowAnyHeader();
                    });
            });

            // mvc controllers
            services.AddControllers().AddJsonOptions(opts =>
            {
                opts.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

            // setup sql logging
            services.AddSingleton<SqlClientListener>();

            // setup for service as QP8 plugin
            services.Configure<QpPluginSettings>(Configuration.GetSection("QpPluginSettings"));
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            //Warm up the listener
            app.ApplicationServices.GetService<SqlClientListener>();

            app.UseGraphQLPlayground();
            app.UseGraphQLVoyager();
            app.UseGraphQLAltair();
            app.UseGraphQLGraphiQL();

            app.UseRouting();
            app.UseCors();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapGraphQL(Configuration);
            });
        }
    }
}
