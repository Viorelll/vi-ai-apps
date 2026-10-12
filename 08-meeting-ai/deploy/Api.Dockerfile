FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY .build/api/ .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "MeetingAI.Api.dll"]
