# Enumeration

[![.NET](https://github.com/linkdotnet/Enumeration/actions/workflows/dotnet.yml/badge.svg)](https://github.com/linkdotnet/Enumeration/actions/workflows/dotnet.yml)
[![Nuget](https://img.shields.io/nuget/dt/LinkDotNet.Enumeration)](https://www.nuget.org/packages/LinkDotNet.Enumeration/)
[![GitHub tag](https://img.shields.io/github/v/tag/linkdotnet/Enumeration?include_prereleases&logo=github&style=flat-square)](https://github.com/linkdotnet/BuildInformation/releases)

Source code generated string Enumeration with completeness!

## What is in the box?

This source code generator let's you easily create string based enumerations with a lot of features.

```csharp
[Enumeration("Red", "Green", "Blue")]
public sealed partial class Color;
```

That's all you need to do to create a string based enumeration. You can either use it like this:

```csharp
var color = Color.Red;

// Create it by a string key:
var color = Color.Create("Red");
```

## Exhaustiveness

The great benefit of the library is that you have support for exhaustiveness:


```csharp

var color = Color.Create("Red");

color.Match(
    red => Console.WriteLine("It's red!"),
    green => Console.WriteLine("It's green!"),
    blue => Console.WriteLine("It's blue!")
);
```

Or return a value:

```csharp
var color = Color.Create("Red");

var colorCode = color.Match(
    red => "#FF0000",
    green => "#00FF00",
    blue => "#0000FF"
);
```

### Limitations

* Your code should run at least `net8.0` or later, as the library uses things like `FrozenSet`.