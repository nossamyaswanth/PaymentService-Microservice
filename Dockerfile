# STEP 1: Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore
RUN dotnet publish -c Release -o /app

# STEP 2: Runtime Image
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app .
EXPOSE 5258
ENV ASPNETCORE_URLS=http://0.0.0.0:5258
ENTRYPOINT ["dotnet", "PaymentService.dll"]