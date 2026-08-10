# Phase 12: containerizes only the Web app - the Functions project stays on
# its own Consumption plan, unrelated to this image.

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy just the Web project's .csproj first and restore - so editing app
# code later doesn't invalidate this layer's cache, only editing the
# .csproj itself does.
COPY ["src/AzureInventoryPlatform.Web/AzureInventoryPlatform.Web.csproj", "src/AzureInventoryPlatform.Web/"]
RUN dotnet restore "src/AzureInventoryPlatform.Web/AzureInventoryPlatform.Web.csproj"

COPY . .
WORKDIR /src/src/AzureInventoryPlatform.Web
RUN dotnet publish -c Release -o /app/publish --no-restore

# Runtime stage - the ASP.NET image alone, not the much larger SDK image
# used above, since running the app needs no compiler/build tools at all.
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "AzureInventoryPlatform.Web.dll"]
