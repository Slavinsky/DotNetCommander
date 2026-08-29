# OpenMcdf upstream snapshot

- Repository: https://github.com/openmcdf/openmcdf
- Commit: `2c2ad0e9f360d90bc9bb726a1a07ba5d6647d809`
- Retrieved: 2026-08-29
- License: Mozilla Public License 2.0 (`LICENSE.txt`)
- Included: the `OpenMcdf/*.cs` core library sources only
- Excluded: tests, benchmarks, samples, UI projects, CI configuration and the compatibility shims used only by the upstream `netstandard2.0` target

The snapshot is compiled directly into `DotNetCommander.dll` by `Commander.NET.csproj`; it does not produce or load a separate `OpenMcdf.dll`. DotNetCommander does not consume the OpenMcdf NuGet package and does not contain a nested Git repository. Upstream `AssemblyInfo.cs` is excluded because its assembly-level COM metadata belongs to the standalone library, not to the application assembly.

Local integration code lives outside this directory. Keep upstream source modifications separate and record any unavoidable changes below.

## Local source modifications

None.
