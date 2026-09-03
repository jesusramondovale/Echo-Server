FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY Echo.sln ./
COPY src/Echo.Domain/Echo.Domain.csproj src/Echo.Domain/
COPY src/Echo.Application/Echo.Application.csproj src/Echo.Application/
COPY src/Echo.Infrastructure/Echo.Infrastructure.csproj src/Echo.Infrastructure/
COPY src/Echo.Api/Echo.Api.csproj src/Echo.Api/
RUN dotnet restore src/Echo.Api/Echo.Api.csproj
COPY src/ src/
RUN dotnet publish src/Echo.Api/Echo.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Echo.Api.dll"]
