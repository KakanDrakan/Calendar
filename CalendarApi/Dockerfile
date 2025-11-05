# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore
RUN dotnet publish -c Release -o /app/publish

RUN apt-get update && \
    apt-get install -y \
        libfontconfig1 libfreetype6 libx11-6 libxext6 \
        libxrender1 libxi6 libxrandr2 libxinerama1 libxfixes3 \
        libxcomposite1 libxdamage1 libxcursor1 libpango-1.0-0 \
        libatk1.0-0 libharfbuzz0b libepoxy0 \
    && rm -rf /var/lib/apt/lists/*

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .

# Copy the certificate into the container
COPY GraphWebhookEncryption.pfx /app/GraphWebhookEncryption.pfx

RUN ls -l /app
ENV ASPNETCORE_URLS=http://+:7248
EXPOSE 7248
ENTRYPOINT ["dotnet", "CalendarApi.dll"]
