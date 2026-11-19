# https://hub.docker.com/_/microsoft-dotnet
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# copy csproj and restore as distinct layers. This helps speed up subsequent rebuilds.
COPY EmojiWaterfall.Api/EmojiWaterfall.Api.csproj src/EmojiWaterfall.Api/EmojiWaterfall.Api.csproj
RUN dotnet restore src/EmojiWaterfall.Api/EmojiWaterfall.Api.csproj 

# copy everything else
COPY EmojiWaterfall.Api/ src/EmojiWaterfall.Api/
RUN dotnet publish -c release -o /api --no-restore src/EmojiWaterfall.Api/EmojiWaterfall.Api.csproj

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /api
COPY --from=build /api /api
# Run as the non-root user that the .NET base image provides.
USER $APP_UID
ENTRYPOINT [ "dotnet", "EmojiWaterfall.Api.dll" ]