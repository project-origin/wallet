ARG PROJECT=ProjectOrigin.Vault

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0.401 AS build
ARG PROJECT

WORKDIR /builddir

COPY Makefile Makefile
COPY .config .config
COPY Directory.Build.props Directory.Build.props
COPY protos protos
COPY src src

RUN dotnet tool restore
RUN dotnet publish src/ProjectOrigin.Vault -c Release -p:CustomAssemblyName=App -o /app/publish

# ------- production image -------
FROM mcr.microsoft.com/dotnet/aspnet:8.0.8-jammy-chiseled-extra AS production

WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 5000

ENTRYPOINT ["dotnet", "App.dll"]
