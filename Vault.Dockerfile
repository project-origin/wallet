ARG PROJECT=ProjectOrigin.Vault

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:9.0.301-noble AS build
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
FROM mcr.microsoft.com/dotnet/aspnet:9.0.6-azurelinux3.0-distroless-extra AS production

WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 5000
EXPOSE 5001

ENTRYPOINT ["dotnet", "Vault.dll"]
