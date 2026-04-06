# Changelog

All notable changes to **Enumeration** will be documented in this file. The project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

<!-- The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/) -->

## [Unreleased]

### Added

- Support for `partial class` declarations in addition to `partial record`.

## [1.1.0] - 2026-04-06

### Added

- `TryCreate(string? key, out {TypeName}? value)` factory method returning `bool` with `[NotNullWhen(true)]` on the out parameter for null-safe pattern matching.
- `Casing` enum (`PascalCase` \| `Preserve`) accepted as an optional first argument to `[Enumeration]` to control how static member names are derived from the string values.

## [1.0.1] - 2026-04-05

### Fixed

- Field names are pascal case

## [1.0.0] - 2026-04-05

### Added

- Initial release of `Enumeration` source code generator.

[unreleased]: https://github.com/linkdotnet/Enumeration/compare/1.1.0...HEAD
[1.1.0]: https://github.com/linkdotnet/Enumeration/compare/1.0.1...1.1.0
[1.0.1]: https://github.com/linkdotnet/Enumeration/compare/1.0.0...1.0.1
[1.0.0]: https://github.com/linkdotnet/Enumeration/compare/8d85f242bf1652588c7b544b297c6734e1044e3d...1.0.0
