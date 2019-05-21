FROM microsoft/dotnet:2.2-sdk AS build
WORKDIR /build
COPY . ./
WORKDIR /build/q2g-con-hypercube-grpc
RUN dotnet restore
RUN dotnet build --no-restore
RUN dotnet publish --output "../dist" --no-restore --no-build

FROM microsoft/dotnet:2.2-runtime AS base
WORKDIR /app
COPY --from=build ./build/dist ./
ENTRYPOINT ["dotnet", "q2g-con-hypercube-grpc.dll"]