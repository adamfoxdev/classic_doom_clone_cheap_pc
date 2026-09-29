# The Hexen Sharp matchmaker, for Fly.io (see fly.toml) or any container host.
# Build from this folder:  docker build -t hexen-matchmaker .
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY HexenSharp.Matchmaker.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080
# the image's unprivileged user
USER app
ENTRYPOINT ["dotnet", "HexenSharp.Matchmaker.dll"]
