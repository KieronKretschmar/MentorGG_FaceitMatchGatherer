# ===============
# BUILD IMAGE
# ===============
FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS build
WORKDIR /app

# Copy csproj and restore as distinct layers

WORKDIR /app/rabbitcommunicationlib
COPY ./rabbitcommunicationlib ./
RUN dotnet restore

WORKDIR /app/Entities
COPY ./Entities/*.csproj ./
RUN dotnet restore

WORKDIR /app/Database
COPY ./Database/*.csproj ./
RUN dotnet restore

WORKDIR /app/FaceitMatchGatherer
COPY ./FaceitMatchGatherer/*.csproj ./
RUN dotnet restore

# Copy everything else and build
WORKDIR /app
COPY ./FaceitMatchGatherer/ ./FaceitMatchGatherer
COPY ./Database/ ./Database
COPY ./Entities ./Entities
COPY ./rabbitcommunicationlib ./rabbitcommunicationlib

RUN dotnet publish FaceitMatchGatherer/ -c Release -o out

# ===============
# RUNTIME IMAGE
# ===============
FROM mcr.microsoft.com/dotnet/core/aspnet:3.1 AS runtime
WORKDIR /app

COPY --from=build /app/out .
ENTRYPOINT ["dotnet", "FaceitMatchGatherer.dll"]
