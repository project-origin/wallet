ARG PROJECT=ProjectOrigin.Vault

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:9.0.301 AS build
ARG PROJECT

WORKDIR /builddir

COPY Makefile Makefile
COPY .config .config
COPY Directory.Build.props Directory.Build.props
COPY protos protos
COPY src src

RUN dotnet tool restore
RUN dotnet publish src/ProjectOrigin.Vault -c Release -p:CustomAssemblyName=Vault -o /app/publish

# ------- Healthcheck static binary -------
FROM --platform=$BUILDPLATFORM golang:1.24-alpine3.22 AS health-probe

WORKDIR /src
ENV CGO_ENABLED=0
RUN <<EOF
printf '%s\n' \
  'package main' \
  'import ("net/http"; "os")' \
  'func main() {' \
  '  if len(os.Args) < 2 { os.Exit(1) }' \
  '  r, err := http.Get(os.Args[1]);' \
  '  if err != nil || r.StatusCode >= 400 { os.Exit(1) }' \
  '}' > main.go
EOF
RUN go build -ldflags="-s -w" -o /healthprobe .

# ------- production image -------
FROM mcr.microsoft.com/dotnet/aspnet:9.0.6-noble-chiseled AS production

WORKDIR /app
COPY --from=build /app/publish .
COPY --from=health-probe /healthprobe /healthprobe
HEALTHCHECK CMD ["/healthprobe","http://localhost:5000/healthz"]

EXPOSE 5000

ENTRYPOINT ["dotnet", "Vault.dll"]
