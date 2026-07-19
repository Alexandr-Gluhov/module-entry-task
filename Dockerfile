FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ModuleEntryTask/ModuleEntryTask.csproj ModuleEntryTask/
RUN dotnet restore ModuleEntryTask/ModuleEntryTask.csproj

COPY ModuleEntryTask/ ModuleEntryTask/
RUN dotnet publish ModuleEntryTask/ModuleEntryTask.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends libgssapi-krb5-2 && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080

ENTRYPOINT ["dotnet", "ModuleEntryTask.dll"]
