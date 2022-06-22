# Microsoft.AspNetCore.Authentication.Negotiate

This project contains an implementation of [Negotiate Authentication](https://docs.microsoft.com/aspnet/core/security/authentication/windowsauth#kestrel) for ASP.NET Core. It's designed to work cross platform with Kestrel. When using IIS or HttpSys servers this will delegate to their built in implementation instead.

## Development Setup

### Build

To build this specific project from source, you can follow the instructions [on building a subset of the code](https://github.com/dotnet/aspnetcore/blob/main/docs/BuildFromSource.md#building-a-subset-of-the-code).

Or for the less detailed explanation, run the following command inside the parent `security` directory.
```powershell
> ./build.cmd
```

### Test

To run the tests for this project, you can [run the tests on the command line](https://github.com/dotnet/aspnetcore/blob/main/docs/BuildFromSource.md#running-tests-on-command-line) in this directory.

Or for the less detailed explanation, run the following command inside the parent `security` directory.
```powershell
> ./build.cmd -t
```

You can also run project specific tests by running `dotnet test` in the `tests` directory next to the `src` directory of the project.

## More Information

For more information, see the [Security README](../../../README.md) and the [ASP.NET Core README](../../../../../README.md).
