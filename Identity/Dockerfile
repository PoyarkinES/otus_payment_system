# Сборка Identity Host. Контекст — корень репозитория.
# Shared пакуется внутри build-стадии, чтобы образ не зависел от локального feed.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props NuGet.config ./
COPY Shared/ Shared/

RUN mkdir -p /src/artifacts/packages \
    && dotnet pack Shared/src/Otus.PaymentSystem.SharedKernel/Otus.PaymentSystem.SharedKernel.csproj -c Release -o /src/artifacts/packages \
    && dotnet pack Shared/src/Otus.PaymentSystem.AspNetCore/Otus.PaymentSystem.AspNetCore.csproj -c Release -o /src/artifacts/packages

COPY Identity/src/Otus.PaymentSystem.Identity.Domain/Otus.PaymentSystem.Identity.Domain.csproj Identity/src/Otus.PaymentSystem.Identity.Domain/
COPY Identity/src/Otus.PaymentSystem.Identity.Application/Otus.PaymentSystem.Identity.Application.csproj Identity/src/Otus.PaymentSystem.Identity.Application/
COPY Identity/src/Otus.PaymentSystem.Identity.Infrastructure/Otus.PaymentSystem.Identity.Infrastructure.csproj Identity/src/Otus.PaymentSystem.Identity.Infrastructure/
COPY Identity/src/Otus.PaymentSystem.Identity.Host/Otus.PaymentSystem.Identity.Host.csproj Identity/src/Otus.PaymentSystem.Identity.Host/

RUN dotnet restore Identity/src/Otus.PaymentSystem.Identity.Host/Otus.PaymentSystem.Identity.Host.csproj

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
