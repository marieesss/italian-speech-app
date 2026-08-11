FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore first, on the project files only, so a code change doesn't invalidate the layer.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/Api/ItalianApp.Api.csproj src/Api/
RUN dotnet restore src/Api/ItalianApp.Api.csproj

COPY src/ src/
RUN dotnet publish src/Api/ItalianApp.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .

# Behind the reverse proxy: plain HTTP, TLS is terminated by nginx.
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_NOLOGO=true

EXPOSE 8080
USER app

ENTRYPOINT ["dotnet", "ItalianApp.Api.dll"]
