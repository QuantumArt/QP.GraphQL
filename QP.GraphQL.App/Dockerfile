#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:5.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
WORKDIR /src
COPY ["QP.GraphQL.App/QP.GraphQL.App.csproj", "QP.GraphQL.App/"]
COPY ["QP.GraphQL.Interfaces/QP.GraphQL.Interfaces.csproj", "QP.GraphQL.Interfaces/"]
COPY ["QP.GraphQL.DAL/QP.GraphQL.DAL.csproj", "QP.GraphQL.DAL/"]
RUN dotnet restore "QP.GraphQL.App/QP.GraphQL.App.csproj"
COPY . .
COPY ["QP.GraphQL.App/nlog.Docker.config", "QP.GraphQL.App/nlog.config"]
WORKDIR "/src/QP.GraphQL.App"
RUN dotnet build "QP.GraphQL.App.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "QP.GraphQL.App.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "QP.GraphQL.App.dll"]