# The Hexen Sharp matchmaker, with the browser version of the game at /play/ (both from this one commit, so they
# always match). For Fly.io (see fly.toml at the repo root) or any container host. Build from the repo's root:
#   docker build -f server/Matchmaker/Dockerfile --build-arg GAME_VERSION=1.2.0 -t hexen-matchmaker .
# GAME_VERSION is the game version the browser version reports: online rooms only match the same version, so set it
# to the desktop release's (the release workflow does). Left out, it's "dev", which only matches other dev builds.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG GAME_VERSION=dev
WORKDIR /src
COPY server/Matchmaker/HexenSharp.Matchmaker.csproj server/Matchmaker/
RUN dotnet restore server/Matchmaker/HexenSharp.Matchmaker.csproj
COPY web/HexenSharp.Web.csproj web/
RUN dotnet restore web/HexenSharp.Web.csproj
COPY . .
RUN dotnet publish server/Matchmaker/HexenSharp.Matchmaker.csproj -c Release -o /app --no-restore
RUN dotnet publish web/HexenSharp.Web.csproj -c Release -o /web --no-restore -p:Version=$GAME_VERSION \
    && cp -r /web/wwwroot /app/wwwroot/play

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080
# the image's unprivileged user
USER app
ENTRYPOINT ["dotnet", "HexenSharp.Matchmaker.dll"]
