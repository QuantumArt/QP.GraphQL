# QP GraphQL Server

## Table of contents
* [1. Introduction](#introduction)
* [2. Dependencies](#dependencies)
* [3. Configuration](#configuration)
* [4. Structured logging](#structuredlogging)
* [5. Trace SQL](#tracesql)

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

