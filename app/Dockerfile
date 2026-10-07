# Build stage: the SDK image compiles the app. Nobody needs the .NET SDK installed locally.
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /src
COPY TrainingApp.csproj .
RUN dotnet restore -a $TARGETARCH
COPY . .
RUN dotnet publish -c Release -a $TARGETARCH --no-restore -o /out

# Runtime stage: only the ASP.NET Core runtime, no SDK, no source code.
FROM mcr.microsoft.com/dotnet/aspnet:10.0
ARG APP_VERSION=dev
ARG REQUIRE_DATABASE=false
ENV APP_VERSION=$APP_VERSION \
    App__RequireDatabase=$REQUIRE_DATABASE
WORKDIR /app
COPY --from=build /out .
# The base image listens on 8080 (ASPNETCORE_HTTP_PORTS) and ships a non-root user.
USER $APP_UID
ENTRYPOINT ["dotnet", "TrainingApp.dll"]
