# Use the official ASP.NET Core runtime image as a base image
FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

# Use the official SDK image to build the application
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["ResumeParserApi/ResumeParserApi.csproj", "ResumeParserApi/"]
RUN dotnet restore "ResumeParserApi/ResumeParserApi.csproj"
COPY . .
WORKDIR "/src/ResumeParserApi"
RUN dotnet build "ResumeParserApi.csproj" -c Release -o /app/build

# Publish the app
FROM build AS publish
RUN dotnet publish "ResumeParserApi.csproj" -c Release -o /app/publish

# Copy the build output from the publish stage and set it up to run the application
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ResumeParserApi.dll"]
