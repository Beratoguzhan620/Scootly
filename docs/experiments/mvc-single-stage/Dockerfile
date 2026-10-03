# DENEY: tek aşamalı imaj. Üretimde kullanılmaz; yalnızca boyut karşılaştırması içindir.
FROM mcr.microsoft.com/dotnet/sdk:10.0
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props ./
COPY src/ src/
RUN dotnet publish src/Scootly.Mvc/Scootly.Mvc.csproj -c Release -o /app /p:UseAppHost=false
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
ENTRYPOINT ["dotnet", "Scootly.Mvc.dll"]