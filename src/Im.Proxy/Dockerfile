FROM microsoft/dotnet:2.1-aspnetcore-runtime AS base
WORKDIR /app
EXPOSE 4023
EXPOSE 44332

FROM microsoft/dotnet:2.1-sdk AS build
WORKDIR /src
COPY Im.Proxy/Im.Proxy.csproj Im.Proxy/
RUN dotnet restore Im.Proxy/Im.Proxy.csproj
COPY . .
WORKDIR /src/Im.Proxy
RUN dotnet build Im.Proxy.csproj -c Release -o /app

FROM build AS publish
RUN dotnet publish Im.Proxy.csproj -c Release -o /app

FROM base AS final
WORKDIR /app
COPY --from=publish /app .
ENTRYPOINT ["dotnet", "Im.Proxy.dll"]
