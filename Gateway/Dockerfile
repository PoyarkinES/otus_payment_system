# Сборка Gateway (YARP + JWT). Контекст — корень репозитория: нужны Shared и Directory.Build.props.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Сначала только манифесты: слой restore кэшируется, пока не менялись csproj.
COPY Directory.Build.props ./
COPY Shared/src/Otus.PaymentSystem.SharedKernel/Otus.PaymentSystem.SharedKernel.csproj Shared/src/Otus.PaymentSystem.SharedKernel/
COPY Shared/src/Otus.PaymentSystem.AspNetCore/Otus.PaymentSystem.AspNetCore.csproj Shared/src/Otus.PaymentSystem.AspNetCore/
COPY Gateway/src/Otus.PaymentSystem.Gateway.Host/Otus.PaymentSystem.Gateway.Host.csproj Gateway/src/Otus.PaymentSystem.Gateway.Host/

RUN dotnet restore Gateway/src/Otus.PaymentSystem.Gateway.Host/Otus.PaymentSystem.Gateway.Host.csproj

COPY Shared/src/ Shared/src/
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
