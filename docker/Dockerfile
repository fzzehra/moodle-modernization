# Yeni sistemin imajı. Bağlam deponun kökü: docker build -f docker/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Moodle.slnx ./
COPY src/ src/
COPY tests/ tests/
RUN dotnet restore Moodle.slnx
RUN dotnet publish src/Moodle.Api/Moodle.Api.csproj -c Release -o /out --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /out .
# AUTH-NFR-03 (aday): oturum çerezini şifreleyen Data Protection anahtarları (docker-compose: modern-dp-keys birimi).
# Anahtarlar Linux'ta dosyada şifresiz durduğundan dizin yalnızca uygulama kullanıcısına açıktır.
RUN mkdir -p /var/moodle-keys && chmod 700 /var/moodle-keys
EXPOSE 8080
ENTRYPOINT ["dotnet", "Moodle.Api.dll"]
