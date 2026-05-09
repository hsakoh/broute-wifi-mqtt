dotnet restore "src/BRouteWifiMqttApp/BRouteWifiMqttApp.csproj"
dotnet publish "src/BRouteWifiMqttApp/BRouteWifiMqttApp.csproj" -r linux-musl-arm64 -p:PublishSingleFile=true --self-contained -c Release -o "./_compile_self/aarch64" --no-restore
dotnet publish "src/BRouteWifiMqttApp/BRouteWifiMqttApp.csproj" -r linux-musl-x64 -p:PublishSingleFile=true --self-contained -c Release -o "./_compile_self/amd64" --no-restore
