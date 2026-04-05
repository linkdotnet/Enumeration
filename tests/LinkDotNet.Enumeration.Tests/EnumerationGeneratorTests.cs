using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Shouldly;

namespace LinkDotNet.Enumeration.Tests;

public sealed class EnumerationGeneratorTests
{
    [Fact]
    public void EmitsEnumerationAttribute()
    {
        var result = RunGenerator(string.Empty);

        var attributeFile = result.GeneratedTrees.SingleOrDefault(t => t.FilePath.EndsWith("EnumerationAttribute.g.cs"));
        attributeFile.ShouldNotBeNull();

        var text = attributeFile.GetText(Xunit.TestContext.Current.CancellationToken).ToString();
        text.ShouldContain("internal sealed class EnumerationAttribute");
        text.ShouldContain("params string[] values");
    }

    [Fact]
    public void GeneratesStaticFieldsForEachValue()
    {
        var source = """
            using LinkDotNet.Enumeration;

            [Enumeration("One", "Two", "Three")]
            public sealed partial record TestEnum;
            """;

        var text = GetGeneratedText(source, "TestEnum");

        text.ShouldContain("public static readonly TestEnum One = new(\"One\");");
        text.ShouldContain("public static readonly TestEnum Two = new(\"Two\");");
        text.ShouldContain("public static readonly TestEnum Three = new(\"Three\");");
    }

    [Fact]
    public void GeneratesAllPropertyWithFrozenSet()
    {
        var source = """
            using LinkDotNet.Enumeration;

            [Enumeration("Alpha", "Beta")]
            public sealed partial record Sample;
            """;

        var text = GetGeneratedText(source, "Sample");

        text.ShouldContain("public static FrozenSet<Sample> All { get; }");
        text.ShouldContain("new Sample[] { Alpha, Beta }.ToFrozenSet()");
    }

    [Fact]
    public void GeneratesCreateFactoryMethod()
    {
        var source = """
            using LinkDotNet.Enumeration;

            [Enumeration("A", "B")]
            public sealed partial record Foo;
            """;

        var text = GetGeneratedText(source, "Foo");

        text.ShouldContain("public static Foo Create(string key)");
        text.ShouldContain("ArgumentException.ThrowIfNullOrWhiteSpace(key)");
        text.ShouldContain("throw new InvalidOperationException");
        text.ShouldNotContain("SingleOrDefault");
    }

    [Fact]
    public void GeneratesCreateWithSwitchExpression()
    {
        var source = """
            using LinkDotNet.Enumeration;

            [Enumeration("Alpha", "Beta", "Gamma")]
            public sealed partial record Color;
            """;

        var text = GetGeneratedText(source, "Color");

        text.ShouldContain("return key switch");
        text.ShouldContain("\"Alpha\" => Alpha,");
        text.ShouldContain("\"Beta\" => Beta,");
        text.ShouldContain("\"Gamma\" => Gamma,");
        text.ShouldContain("_ => throw new InvalidOperationException");
    }

    [Fact]
    public void GeneratesXmlDocsForPublicMembers()
    {
        var source = """
            using LinkDotNet.Enumeration;

            [Enumeration("One", "Two")]
            public sealed partial record Documented;
            """;

        var text = GetGeneratedText(source, "Documented");

        // Key property
        text.ShouldContain("/// <summary>Gets the string key that identifies this enumeration value.</summary>");

        // Static fields
        text.ShouldContain("/// <summary>Gets the <see cref=\"Documented\"/> instance for <c>One</c>.</summary>");
        text.ShouldContain("/// <summary>Gets the <see cref=\"Documented\"/> instance for <c>Two</c>.</summary>");

        // All property
        text.ShouldContain("/// <summary>Gets a frozen set of all valid <see cref=\"Documented\"/> instances.");

        // Create method
        text.ShouldContain("/// <param name=\"key\">The key to look up.");
        text.ShouldContain("/// <returns>The matching <see cref=\"Documented\"/> instance.</returns>");
        text.ShouldContain("/// <exception cref=\"InvalidOperationException\">Thrown when <paramref name=\"key\"/> does not match any known value.</exception>");

        // Match methods
        text.ShouldContain("/// <param name=\"onOne\">Invoked when the current value is <see cref=\"One\"/>.</param>");
        text.ShouldContain("/// <param name=\"onTwo\">Invoked when the current value is <see cref=\"Two\"/>.</param>");
        text.ShouldContain("/// <typeparam name=\"T\">The return type.</typeparam>");
        text.ShouldContain("/// <returns>The value returned by the matched function.</returns>");
    }

    [Fact]
    public void GeneratesStringComparisonOperators()
    {
        var source = """
            using LinkDotNet.Enumeration;

            [Enumeration("X")]
            public sealed partial record Bar;
            """;

        var text = GetGeneratedText(source, "Bar");

        text.ShouldContain("public static bool operator ==(Bar? a, string? b)");
        text.ShouldContain("public static bool operator !=(Bar? a, string? b) => !(a == b);");
    }

    [Fact]
    public void GeneratesMatchWithReturnValue()
    {
        var source = """
            using LinkDotNet.Enumeration;

            [Enumeration("SqlServer", "Sqlite", "MongoDB")]
            public sealed partial record Provider;
            """;

        var text = GetGeneratedText(source, "Provider");

        text.ShouldContain("public T Match<T>(Func<T> onSqlServer, Func<T> onSqlite, Func<T> onMongoDB)");
        text.ShouldContain("if (Key == SqlServer.Key) return onSqlServer();");
        text.ShouldContain("if (Key == Sqlite.Key) return onSqlite();");
        text.ShouldContain("if (Key == MongoDB.Key) return onMongoDB();");
    }

    [Fact]
    public void GeneratesMatchWithAction()
    {
        var source = """
            using LinkDotNet.Enumeration;

            [Enumeration("SqlServer", "Sqlite", "MongoDB")]
            public sealed partial record Provider;
            """;

        var text = GetGeneratedText(source, "Provider");

        text.ShouldContain("public void Match(Action onSqlServer, Action onSqlite, Action onMongoDB)");
        text.ShouldContain("if (Key == SqlServer.Key) { onSqlServer(); return; }");
    }

    [Fact]
    public void RespectsNamespace()
    {
        var source = """
            using LinkDotNet.Enumeration;

            namespace My.Custom.Namespace;

            [Enumeration("A", "B")]
            public sealed partial record MyEnum;
            """;

        var text = GetGeneratedText(source, "MyEnum");

        text.ShouldContain("namespace My.Custom.Namespace;");
    }

    [Fact]
    public void GeneratesPrivateConstructorWithValidation()
    {
        var source = """
            using LinkDotNet.Enumeration;

            [Enumeration("Val")]
            public sealed partial record Single;
            """;

        var text = GetGeneratedText(source, "Single");

        text.ShouldContain("private Single(string key)");
        text.ShouldContain("ArgumentException.ThrowIfNullOrWhiteSpace(key);");
    }

    [Fact]
    public void GeneratesToStringOverride()
    {
        var source = """
            using LinkDotNet.Enumeration;

            [Enumeration("Val")]
            public sealed partial record MyType;
            """;

        var text = GetGeneratedText(source, "MyType");

        text.ShouldContain("public override string ToString() => Key;");
    }

    [Fact]
    public void ProducesNoDiagnosticsForValidInput()
    {
        var source = """
            using LinkDotNet.Enumeration;

            [Enumeration("One", "Two")]
            public sealed partial record Clean;
            """;

        var result = RunGenerator(source);

        var diagnostics = result.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToImmutableArray();

        diagnostics.ShouldBeEmpty();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string GetGeneratedText(string source, string typeName)
    {
        var result = RunGenerator(source);
        var tree = result.GeneratedTrees.SingleOrDefault(t => t.FilePath.EndsWith($"{typeName}.g.cs"));
        tree.ShouldNotBeNull($"Expected generated file '{typeName}.g.cs' was not found.");
        return tree.GetText().ToString();
    }

    private static GeneratorDriverRunResult RunGenerator(string source)
    {
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            [CSharpSyntaxTree.ParseText(source)],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new EnumerationGenerator();
        var driver = CSharpGeneratorDriver
            .Create(generator)
            .RunGenerators(compilation);

        return driver.GetRunResult();
    }
}
