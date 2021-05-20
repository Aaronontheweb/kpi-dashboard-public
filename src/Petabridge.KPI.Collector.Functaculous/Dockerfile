FROM mcr.microsoft.com/dotnet/sdk:3.1 AS base
WORKDIR /app

ENV InfluxDb__ConnectionString ""
ENV InfluxDb__Token ""
ENV InfluxDb__Bucket ""
ENV InfluxDb__Org ""

COPY ./bin/Release/netcoreapp3.1/publish/ /app

FROM mcr.microsoft.com/dotnet/runtime:3.1 AS app
WORKDIR /app

COPY --from=base /app /app

CMD ["dotnet", "Petabridge.KPI.Collector.dll"]