ARG PROJECT=ProjectOrigin.Vault

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:9.0.306 AS build
ARG PROJECT

WORKDIR /builddir

COPY Makefile Makefile
COPY .config .config
COPY Directory.Build.props Directory.Build.props
COPY protos protos
COPY src src

RUN dotnet tool restore
RUN dotnet publish src/ProjectOrigin.Vault -c Release -p:CustomAssemblyName=Vault -o /app/publish

# ------- production image -------
FROM mcr.microsoft.com/dotnet/aspnet:9.0.11-noble-chiseled-extra AS production

WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 5000
EXPOSE 5001

USER $APP_UID

ENTRYPOINT ["dotnet", "Vault.dll"]
