FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Oficina.API/Oficina.API.csproj", "Oficina.API/"]
COPY ["Oficina.Application/Oficina.Application.csproj", "Oficina.Application/"]
COPY ["Oficina.Domain/Oficina.Domain.csproj", "Oficina.Domain/"]
COPY ["Oficina.Infrastructure/Oficina.Infrastructure.csproj", "Oficina.Infrastructure/"]
RUN dotnet restore "Oficina.API/Oficina.API.csproj"
COPY . .
RUN dotnet publish "Oficina.API/Oficina.API.csproj" -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Oficina.API.dll"]
