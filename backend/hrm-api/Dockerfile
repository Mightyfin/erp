# ---------- HRM API — multi-stage build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json ./
COPY src src
RUN dotnet publish src/Mightyfin.Erp.Hrm.Api \
    -c Release -o /out \
    --no-self-contained

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /out .
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --retries=3 \
  CMD curl -fsS http://localhost:8080/api/hrm/health || exit 1
ENTRYPOINT ["dotnet", "Mightyfin.Erp.Hrm.Api.dll"]
