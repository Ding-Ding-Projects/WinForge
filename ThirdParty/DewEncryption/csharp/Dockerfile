FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore csharp/DewEncryption.sln
RUN dotnet build csharp/DewEncryption.sln --configuration Release --no-restore
