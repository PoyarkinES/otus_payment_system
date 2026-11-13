# Сборка Gateway (YARP + JWT). Контекст — корень репозитория.
# Shared пакуется внутри build-стадии, чтобы образ не зависел от локального feed.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props NuGet.config ./
COPY Shared/ Shared/

RUN mkdir -p /src/artifacts/packages \
    && dotnet pack Shared/src/Otus.PaymentSystem.AspNetCore/Otus.PaymentSystem.AspNetCore.csproj -c Release -o /src/artifacts/packages

COPY Gateway/src/Otus.PaymentSystem.Gateway.Host/Otus.PaymentSystem.Gateway.Host.csproj Gateway/src/Otus.PaymentSystem.Gateway.Host/

RUN dotnet restore Gateway/src/Otus.PaymentSystem.Gateway.Host/Otus.PaymentSystem.Gateway.Host.csproj

COPY Gateway/src/ Gateway/src/

RUN dotnet publish Gateway/src/Otus.PaymentSystem.Gateway.Host/Otus.PaymentSystem.Gateway.Host.csproj \
    --configuration Release --no-restore --output /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# curl нужен healthcheck'у compose: в aspnet-образе его нет.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app ./

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Otus.PaymentSystem.Gateway.Host.dll"]
