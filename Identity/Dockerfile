# syntax=docker/dockerfile:1
# Сборка Identity Host. Контекст — корень репозитория.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props NuGet.config ./
COPY artifacts/packages/ artifacts/packages/

COPY Identity/src/Otus.PaymentSystem.Identity.Domain/Otus.PaymentSystem.Identity.Domain.csproj Identity/src/Otus.PaymentSystem.Identity.Domain/
COPY Identity/src/Otus.PaymentSystem.Identity.Application/Otus.PaymentSystem.Identity.Application.csproj Identity/src/Otus.PaymentSystem.Identity.Application/
COPY Identity/src/Otus.PaymentSystem.Identity.Infrastructure/Otus.PaymentSystem.Identity.Infrastructure.csproj Identity/src/Otus.PaymentSystem.Identity.Infrastructure/
COPY Identity/src/Otus.PaymentSystem.Identity.Host/Otus.PaymentSystem.Identity.Host.csproj Identity/src/Otus.PaymentSystem.Identity.Host/

ARG NUGET_USERNAME=PoyarkinES

RUN if ! dotnet nuget list source --configfile NuGet.config | grep -Fq "shared-packages"; then \
        dotnet nuget add source artifacts/packages --name shared-packages --configfile NuGet.config; \
    fi

RUN --mount=type=secret,id=github_token \
    export NuGetPackageSourceCredentials_github="Username=${NUGET_USERNAME};Password=$(cat /run/secrets/github_token);ValidAuthenticationTypes=Basic" \
    && dotnet restore Identity/src/Otus.PaymentSystem.Identity.Host/Otus.PaymentSystem.Identity.Host.csproj \
        --configfile NuGet.config \
    && unset NuGetPackageSourceCredentials_github

COPY Identity/src/ Identity/src/

RUN dotnet publish Identity/src/Otus.PaymentSystem.Identity.Host/Otus.PaymentSystem.Identity.Host.csproj \
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

ENTRYPOINT ["dotnet", "Otus.PaymentSystem.Identity.Host.dll"]
