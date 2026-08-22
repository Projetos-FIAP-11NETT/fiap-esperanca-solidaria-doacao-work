# Construido a partir da RAIZ do repositorio, porque o Worker referencia os projetos das
# outras camadas:
#   docker build -t doacao-worker -f src/EsperancaSolidaria.Doacao.Worker/Dockerfile .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore em camada propria: so refaz o download de pacotes quando um .csproj muda.
COPY Directory.Build.props ./
COPY src/EsperancaSolidaria.Doacao.Domain/*.csproj              src/EsperancaSolidaria.Doacao.Domain/
COPY src/EsperancaSolidaria.Doacao.Application/*.csproj         src/EsperancaSolidaria.Doacao.Application/
COPY src/EsperancaSolidaria.Doacao.Infrastructure/*.csproj      src/EsperancaSolidaria.Doacao.Infrastructure/
COPY src/EsperancaSolidaria.Doacao.Worker/*.csproj              src/EsperancaSolidaria.Doacao.Worker/
RUN dotnet restore src/EsperancaSolidaria.Doacao.Worker/EsperancaSolidaria.Doacao.Worker.csproj

COPY src/ src/
RUN dotnet publish src/EsperancaSolidaria.Doacao.Worker/EsperancaSolidaria.Doacao.Worker.csproj \
    -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# O Npgsql tenta GSSAPI ao abrir a conexao e, sem esta lib, escreve um erro direto no stderr
# do processo - por fora do ILogger, portanto sem a etiqueta [doacao-worker]. A conexao
# funciona do mesmo jeito; a lib e o que mantem a saida do container sob um formato so.
USER root
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

# Usuario nao-root ja vem definido na imagem base (uid 1654).
USER $APP_UID

# Porta das probes /health/live e /health/ready.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app .
ENTRYPOINT ["dotnet", "EsperancaSolidaria.Doacao.Worker.dll"]
