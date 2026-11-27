# syntax=docker/dockerfile:1
# Сборка Gateway (YARP + JWT). Контекст — корень репозитория.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props NuGet.config ./
COPY artifacts/packages/ artifacts/packages/

COPY Gateway/src/Otus.PaymentSystem.Gateway.Host/Otus.PaymentSystem.Gateway.Host.csproj Gateway/src/Otus.PaymentSystem.Gateway.Host/

ARG NUGET_USERNAME=PoyarkinES

RUN if ! dotnet nuget list source --configfile NuGet.config | grep -Fq "shared-packages"; then \
        dotnet nuget add source artifacts/packages --name shared-packages --configfile NuGet.config; \
    fi

RUN --mount=type=secret,id=github_token \
    export NuGetPackageSourceCredentials_github="Username=${NUGET_USERNAME};Password=$(cat /run/secrets/github_token);ValidAuthenticationTypes=Basic" \
    && dotnet restore Gateway/src/Otus.PaymentSystem.Gateway.Host/Otus.PaymentSystem.Gateway.Host.csproj \
        --configfile NuGet.config \
    && unset NuGetPackageSourceCredentials_github

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
