# QP GraphQL Server

## Table of contents
* [1. Introduction](#introduction)
* [2. Dependencies](#dependencies)
* [3. Configuration](#configuration)
* [4. Structured logging](#structuredlogging)
* [5. Trace SQL](#tracesql)
* [6. Reload Schema](#reloadschema)
* [7. Deployment](#deployment)

## 1. Introduction <a name="introduction"></a>

QP GraphQL Server is a web application on dotnet core 5.0. It serves to perform [GraphQL](https://graphql.org/learn/) queries over QP database. GraphQL [Specification](https://github.com/graphql/graphql-spec) is also available.

## 2. Dependencies <a name="dependencies"></a>
The application depens on [NuGet](https://www.nuget.org/) packages:

### 2.1 GraphQL

* [Documentation](https://graphql-dotnet.github.io/docs/getting-started/introduction)
* [NuGet](https://www.nuget.org/packages/GraphQL/)
* [GitHub](https://github.com/graphql-dotnet/graphql-dotnet)

### 2.2 Microsoft.Data.SqlClient

### 2.3 Npgsql

### 2.4 Dapper

### 2.5 NLog.Web.AspNetCore

* [Documentation](https://github.com/NLog/NLog/wiki/Getting-started-with-ASP.NET-Core-5)
* [NuGet](https://www.nuget.org/packages/NLog.Web.AspNetCore/)
* [GitHub](https://github.com/NLog/NLog.Web)


## 3. Configuration <a name="configuration"></a>
Configuration is available on `appsettings.json` file.

Postgres settings
```json
  "ConnectionStrings": {
    "QPConnectionString": "Server=dbserver;Database=dbname;User Id=user;Password=password",
    "Type": "Postgres"
  },
```

SqlServer settings
```json
  "ConnectionStrings": {
    "QPConnectionString": "Initial Catalog=dbname;Data Source=dbserver;User ID=user;Password=password",
    "Type": "SqlServer"
  },
```

## 4. Structured logging <a name="structuredlogging"></a>
 * [Message Templates](https://messagetemplates.org/)
 * [How to use structured logging](https://github.com/NLog/NLog/wiki/How-to-use-structured-logging)

## 5. Trace SQL <a name="tracesql"></a>
 * [Enable event tracing in SqlClient](https://docs.microsoft.com/en-us/sql/connect/ado-net/enable-eventsource-tracing?view=sql-server-ver15)
 * [An in-depth guide to event listeners](https://www.audero.it/blog/2018/04/18/in-depth-guide-event-listeners/)

 ## 6. Reload Schema <a name="reloadschema"></a>
Configuration is available on `appsettings.json` file.
```json
  "SchemaAutoReload": true,
  "SchemaReloadInterval": "00:02:00",
```
Reload API

* ```POST /api/schema/reload``` reload schema
* ```GET /api/schema/context``` get current schema context

One can reload the schema directly in the browser console:
```javascript
await (await fetch('/api/schema/reload', { method: 'POST' })).json();
```

## 4. Deployment <a name="deployment"></a>
### 4.1 Docker

One can build and run docker containers manually. It's the easiest way to run the application. Here basic commands for docker.

Build image
```console
docker build -t qp.graphql -f QP.GraphQL.App/Dockerfile .
```
Run the application in docker container on port 8889
```console
docker run -it -p 8889:80 -e ASPNETCORE_ConnectionStrings__QPConnectionString="{db connection}" -e ASPNETCORE_ConnectionStrings__Type="{db type}" --rm --name=qp.graphql qp.graphql
```
where
* `{db connection}` is the database connection
* `{db type}` is the database type whether `SqlServer` or `Postgres` 

Stop container if running
```console
docker stop qp.graphql
```


Application is available on
[Localhost](http://localhost:8889/ui/playground)


### 4.2 Docker registry

#### 4.2.1 Updating git repository
For publishing the image with new tag one should push to git a label with corresponding name. Generally it's a product version.

#### 4.2.2 Running build
To buld the image and place it to registry one should run the build
[Images.Backend](https://tfs.dev.qsupport.ru/tfs/QuantumartCollection/QP/_build?definitionId=1535)

#### 4.2.3 Checking the registry

After build finishes the image `qp-graphql-service` must be in registry images list:
[Docker registry](http://spbdocker03:5000/v2/_catalog).
Also avaliable image tags can be seen here:
[Image tags](http://spbdocker03:5000/v2/qp-graphql-service/tags/list).

#### 4.2.3 Running the application
```console
docker run -it -p 8890:80 -e ASPNETCORE_ConnectionStrings__QPConnectionString="{db connection}" -e ASPNETCORE_ConnectionStrings__Type="{db type}" --rm --name=smartcode spbdocker03:5000/qp-graphql-service:{tag}
```
where
* `{tag}` is application version
* `{db connection}` is the database connection
* `{db type}` is the database type whether `SqlServer` or `Postgres` 

Application is available on
[Localhost](http://localhost:8890/api/)

### 4.3 Kubernetis
