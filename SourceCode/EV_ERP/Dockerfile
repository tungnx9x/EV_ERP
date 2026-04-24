# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY EV_ERP/EV_ERP.csproj EV_ERP/
RUN dotnet restore EV_ERP/EV_ERP.csproj

COPY EV_ERP/ EV_ERP/
WORKDIR /src/EV_ERP
RUN dotnet publish -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# Create directory for file uploads
RUN mkdir -p /app/FileStorage

COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "EV_ERP.dll"]
