FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/VaporTrail/VaporTrail.csproj src/VaporTrail/
RUN dotnet restore src/VaporTrail/VaporTrail.csproj

COPY src/VaporTrail/ src/VaporTrail/
RUN dotnet publish src/VaporTrail/VaporTrail.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /app/publish .
RUN mkdir -p /app/App_Data && chown -R $APP_UID:$APP_UID /app/App_Data

USER $APP_UID

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_HTTP_PORTS=
ENV PORT=10000

EXPOSE 10000
ENTRYPOINT ["dotnet", "VaporTrail.dll"]
