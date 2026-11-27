# syntax=docker/dockerfile:1
# Сборка Wallets Host. Контекст — корень репозитория.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props NuGet.config ./

COPY Wallets/src/Otus.PaymentSystem.Wallets.Domain/Otus.PaymentSystem.Wallets.Domain.csproj Wallets/src/Otus.PaymentSystem.Wallets.Domain/
COPY Wallets/src/Otus.PaymentSystem.Wallets.Application/Otus.PaymentSystem.Wallets.Application.csproj Wallets/src/Otus.PaymentSystem.Wallets.Application/
COPY Wallets/src/Otus.PaymentSystem.Wallets.Infrastructure/Otus.PaymentSystem.Wallets.Infrastructure.csproj Wallets/src/Otus.PaymentSystem.Wallets.Infrastructure/
COPY Wallets/src/Otus.PaymentSystem.Wallets.Host/Otus.PaymentSystem.Wallets.Host.csproj Wallets/src/Otus.PaymentSystem.Wallets.Host/

ARG NUGET_USERNAME=PoyarkinES

RUN --mount=type=secret,id=github_token \
    export NuGetPackageSourceCredentials_github="Username=${NUGET_USERNAME};Password=$(cat /run/secrets/github_token);ValidAuthenticationTypes=Basic" \
    && dotnet restore Wallets/src/Otus.PaymentSystem.Wallets.Host/Otus.PaymentSystem.Wallets.Host.csproj \
        --configfile NuGet.config \
    && unset NuGetPackageSourceCredentials_github

COPY Wallets/src/ Wallets/src/

RUN dotnet publish Wallets/src/Otus.PaymentSystem.Wallets.Host/Otus.PaymentSystem.Wallets.Host.csproj \
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

ENTRYPOINT ["dotnet", "Otus.PaymentSystem.Wallets.Host.dll"]