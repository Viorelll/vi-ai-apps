FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/FeedbackAI.Ai/ ./
RUN dotnet publish FeedbackAI.Ai.csproj -c Release -o /out
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /out ./
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "FeedbackAI.Ai.dll"]
