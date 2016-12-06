FROM microsoft/dotnet:latest

RUN apt-get update
RUN wget -qO- https://deb.nodesource.com/setup_4.x | bash -
RUN apt-get install -y git

WORKDIR .
RUN ["dotnet", "restore"]
RUN ["dotnet", "build"]

EXPOSE 443/tcp
EXPOSE 80/tcp

ENTRYPOINT ["dotnet", "run"]