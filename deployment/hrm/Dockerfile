# syntax=docker/dockerfile:1
# Mightyfin ERP — HRM API (.NET 10) production image
# Multi-stage build mirroring the Go erp-api convention: build stage on full SDK,
# runtime stage on distroless ASP.NET base image running as nonroot.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY backend/hrm-api/ ./
RUN dotnet test src/Mightyfin.Erp.Hrm.Tests \
    && dotnet publish src/Mightyfin.Erp.Hrm.Api -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
COPY --from=build /app/publish .
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "Mightyfin.Erp.Hrm.Api.dll"]
