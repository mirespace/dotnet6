## Blazor WASM benchmarks

These projects assist in Benchmarking Components.
See https://github.com/aspnet/Benchmarks#benchmarks for usage guidance on using the Benchmarking tool with your application

### Running the benchmarks

The TestApp is a regular BlazorWASM project and can be run using `dotnet run`. The Driver is an app that connects against an existing Selenium server, and speaks the Benchmark protocol. You generally do not need to run the Driver locally, but if you were to do so, you can either start a selenium-server instance and run using `dotnet run [<selenium-server-port>]` or run it inside a Linux-based docker container.

Here are the commands you would need to run it locally inside docker:

1. `dotnet publish -c Release Driver/Wasm.Performance.Driver.csproj`
2. `docker build -t blazor-local -f ./local.dockerfile . `
3. `docker run -it blazor-local`

To run the benchmark app in the Benchmark server, run

```
dotnet run -- --config aspnetcore/src/Components/benchmarkapps/Wasm.Performance/benchmarks.compose.json application.endpoints <BenchmarkServerUri> --scenario blazorwasmbenchmark
```

If you have local changes that you'd like to benchmark, the easiest way is to push your local changes and tell the server to use your branch:

```
dotnet run -- --config aspnetcore/src/Components/benchmarkapps/Wasm.Performance/benchmarks.compose.json application.endpoints <BenchmarkServerUri> --scenario blazorwasmbenchmark --application.buildArguments "gitBranch=mylocalchanges"
```