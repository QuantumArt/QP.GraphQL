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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QP.GraphQL.App.Schema;
using QP.GraphQL.DAL;
using QP.GraphQL.Interfaces.Articles;
using QP.GraphQL.Interfaces.DAL;
using QP.GraphQL.Interfaces.Metadata;
using System.Data.Common;

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

            // setup graphql dataloaders
            services.AddSingleton<IDataLoaderContextAccessor, DataLoaderContextAccessor>();
            services.AddSingleton<DataLoaderDocumentListener>();

            services.AddSingleton<SqlClientListener>();

            // qp dal
            services.Configure<ConnectionSettings>(Configuration.GetConnectionSection());
            services.AddSingleton<IConnectionFactory, ConnectionFactory>();
            services.AddTransient<DbConnection>(s => s.GetRequiredService<IConnectionFactory>().GetConnection());
            services.AddTransient<IQpMetadataAccessor, QpMetadataAccessor>();

            if (Configuration.GetDatabaseType() == DatabaseType.Postgres)
            {
                services.AddTransient<IQpArticlesAccessor, QpArticlesAccessorPostgres>();                
            }
            else if(Configuration.GetDatabaseType() == DatabaseType.SqlServer)
            {                
                services.AddTransient<IQpArticlesAccessor, QpArticlesAccessorSqlServer>();                              
            }

            // add schema
            services.Configure<QpMetadataSettings>(Configuration);
            services.AddSingleton<ISchema, QpContentsSchemaDynamic>();

            services.Configure<GraphQLSettings>(Configuration);
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

            app.UseMiddleware<GraphQLMiddleware>();
            app.UseGraphQLPlayground();
            app.UseGraphQLVoyager();
            app.UseGraphQLAltair();
            app.UseGraphQLGraphiQL();
        }
    }
}
