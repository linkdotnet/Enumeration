using System.Collections.Immutable;
using System.IO;
using System.Reflection;
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

        var text = attributeFile.GetText(TestContext.Current.CancellationToken).ToString();
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
    public void GeneratesMatchWithValueDirectly()
    {
        var source = """
            using LinkDotNet.Enumeration;

            [Enumeration("SqlServer", "Sqlite")]
            public sealed partial record Provider;
            """;

        var text = GetGeneratedText(source, "Provider");

        text.ShouldContain("public T Match<T>(T onSqlServer, T onSqlite)");
        text.ShouldContain("return Key switch");
        text.ShouldContain("\"SqlServer\" => onSqlServer,");
        text.ShouldContain("\"Sqlite\" => onSqlite,");
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
        text.ShouldContain("return Key switch");
        text.ShouldContain("\"SqlServer\" => onSqlServer(),");
        text.ShouldContain("\"Sqlite\" => onSqlite(),");
        text.ShouldContain("\"MongoDB\" => onMongoDB(),");
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
        text.ShouldContain("switch (Key)");
        text.ShouldContain("case \"SqlServer\": onSqlServer(); return;");
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

    [Fact]
    public void GeneratesPascalCaseMemberNamesForAllCapsValues()
    {
        var source = """
            using LinkDotNet.Enumeration;

            [Enumeration("OK", "DANGER")]
            public sealed partial record Status;
            """;

        var text = GetGeneratedText(source, "Status");

        text.Contains("public static readonly Status Ok = new(\"OK\");").ShouldBeTrue(text);
        text.Contains("public static readonly Status Danger = new(\"DANGER\");").ShouldBeTrue(text);

        text.Contains("\"OK\" => Ok,").ShouldBeTrue(text);
        text.Contains("\"DANGER\" => Danger,").ShouldBeTrue(text);

        text.Contains("new Status[] { Ok, Danger }").ShouldBeTrue(text);

        text.Contains("Func<T> onOk").ShouldBeTrue(text);
        text.Contains("Func<T> onDanger").ShouldBeTrue(text);
    }

    [Fact]
    public void GeneratesTryCreateMethod()
    {
        var source = """
            using LinkDotNet.Enumeration;

            [Enumeration("A", "B")]
            public sealed partial record Foo;
            """;

        var text = GetGeneratedText(source, "Foo");

        text.ShouldContain("using System.Diagnostics.CodeAnalysis;");
        text.ShouldContain("public static bool TryCreate(string? key, [NotNullWhen(true)] out Foo? value)");
        text.ShouldContain("\"A\" => A,");
        text.ShouldContain("\"B\" => B,");
        text.ShouldContain("_ => null");
        text.ShouldContain("return value is not null;");
    }

    [Fact]
    public void GeneratesTryCreateXmlDocs()
    {
        var source = """
            using LinkDotNet.Enumeration;

            [Enumeration("One", "Two")]
            public sealed partial record Documented;
            """;

        var text = GetGeneratedText(source, "Documented");

        text.ShouldContain("/// <summary>Tries to create the <see cref=\"Documented\"/> instance matching <paramref name=\"key\"/>.</summary>");
        text.ShouldContain("/// <param name=\"value\">When this method returns <see langword=\"true\"/>, contains the matching <see cref=\"Documented\"/> instance; otherwise <see langword=\"null\"/>.</param>");
        text.ShouldContain("/// <returns><see langword=\"true\"/> if a matching instance was found; otherwise <see langword=\"false\"/>.</returns>");
    }

    [Fact]
    public void GeneratedTryCreateCompilesWithoutErrors()
    {
        var source = """
            using LinkDotNet.Enumeration;

            [Enumeration("Alpha", "Beta")]
            public sealed partial record Color;
            """;

        var generatorResult = RunGenerator(source);
        var allTrees = generatorResult.GeneratedTrees.Append(
            CSharpSyntaxTree.ParseText(source, cancellationToken: TestContext.Current.CancellationToken)).ToArray();

        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(static a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(static a => MetadataReference.CreateFromFile(a.Location))
            .ToArray();

        var fullCompilation = CSharpCompilation.Create(
            "FullTestAssembly",
            allTrees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var errors = fullCompilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToImmutableArray();

        errors.ShouldBeEmpty();
    }

    [Fact]
    public void GeneratesPreservedMemberNamesWhenCasingIsPreserve()
    {
        var source = """
            using LinkDotNet.Enumeration;

            [Enumeration(Casing.Preserve, "OK", "in_progress", "DANGER")]
            public sealed partial record Status;
            """;

        var text = GetGeneratedText(source, "Status");

        text.Contains("public static readonly Status OK = new(\"OK\");").ShouldBeTrue(text);
        text.Contains("public static readonly Status in_progress = new(\"in_progress\");").ShouldBeTrue(text);
        text.Contains("public static readonly Status DANGER = new(\"DANGER\");").ShouldBeTrue(text);
    }

    [Fact]
    public void DefaultCasingIsPascalCase()
    {
        var source = """
            using LinkDotNet.Enumeration;

            [Enumeration("OK", "in_progress")]
            public sealed partial record Status;
            """;

        var text = GetGeneratedText(source, "Status");

        text.Contains("public static readonly Status Ok = new(\"OK\");").ShouldBeTrue(text);
        text.Contains("public static readonly Status InProgress = new(\"in_progress\");").ShouldBeTrue(text);
    }

    [Fact]
    public void EmitsCasingEnumInAttributeSource()
    {
        var result = RunGenerator(string.Empty);

        var attributeFile = result.GeneratedTrees.SingleOrDefault(t => t.FilePath.EndsWith("EnumerationAttribute.g.cs"));
        attributeFile.ShouldNotBeNull();

        var text = attributeFile.GetText(TestContext.Current.CancellationToken).ToString();
        text.ShouldContain("internal enum Casing");
        text.ShouldContain("PascalCase,");
        text.ShouldContain("Preserve");
        text.ShouldContain("public Casing MemberCasing { get; }");
        text.ShouldContain("public EnumerationAttribute(Casing casing, params string[] values)");
    }

    [Fact]
    public void GeneratesClassInsteadOfRecord()
    {
        var source = """
            using LinkDotNet.Enumeration;

            [Enumeration("A", "B")]
            public sealed partial class MyClass;
            """;

        var text = GetGeneratedText(source, "MyClass");

        text.ShouldContain("public sealed partial class MyClass");
        text.ShouldNotContain("public sealed partial record MyClass");
    }

    [Fact]
    public void GeneratesIParsableImplementations()
    {
        var source = """
            using LinkDotNet.Enumeration;

            [Enumeration("A", "B")]
            public sealed partial record MyEnum;
            """;

        var text = GetGeneratedText(source, "MyEnum");

        text.ShouldContain("public sealed partial record MyEnum : IParsable<MyEnum>, ISpanParsable<MyEnum>");
        text.ShouldContain("public static MyEnum Parse(string s, IFormatProvider? provider) => Create(s);");
        text.ShouldContain("public static bool TryParse(string? s, IFormatProvider? provider, [NotNullWhen(true)] out MyEnum? result) => TryCreate(s, out result);");
        text.ShouldContain("public static MyEnum Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => Create(s.ToString());");
    }

    [Fact]
    public void GeneratesImplicitAndExplicitConversions()
    {
        var source = """
            using LinkDotNet.Enumeration;

            [Enumeration("A", "B")]
            public sealed partial record MyEnum;
            """;

        var text = GetGeneratedText(source, "MyEnum");

        text.ShouldContain("public static implicit operator string(MyEnum value) => value.Key;");
        text.ShouldContain("public static explicit operator MyEnum(string key) => Create(key);");
    }

    [Fact]
    public void GeneratesIEquatableForClasses()
    {
        var source = """
            using LinkDotNet.Enumeration;

            [Enumeration("A", "B")]
            public sealed partial class MyClass;
            """;

        var text = GetGeneratedText(source, "MyClass");

        text.ShouldContain("public sealed partial class MyClass : IParsable<MyClass>, ISpanParsable<MyClass>, ISpanFormattable, IEquatable<MyClass>");
        text.ShouldContain("public bool Equals(MyClass? other) => other is not null && Key.Equals(other.Key, StringComparison.Ordinal);");
        text.ShouldContain("public override bool Equals(object? obj) => obj is MyClass other && Equals(other);");
        text.ShouldContain("public override int GetHashCode() => Key.GetHashCode();");
    }

    [Fact]
    public void DoesNotGenerateIEquatableForRecords()
    {
        var source = """
            using LinkDotNet.Enumeration;

            [Enumeration("A", "B")]
            public sealed partial record MyRecord;
            """;

        var text = GetGeneratedText(source, "MyRecord");

        text.ShouldContain("public sealed partial record MyRecord : IParsable<MyRecord>, ISpanParsable<MyRecord>");
        text.ShouldNotContain("IEquatable<MyRecord>");
        text.ShouldNotContain("public bool Equals(MyRecord? other)");
    }

    [Fact]
    public void RespectsInternalAccessibility()
    {
        var source = """
            using LinkDotNet.Enumeration;

            [Enumeration("A", "B")]
            internal sealed partial record MyInternalEnum;
            """;

        var text = GetGeneratedText(source, "MyInternalEnum");

        text.ShouldContain("internal sealed partial record MyInternalEnum");
        text.ShouldNotContain("public sealed partial record MyInternalEnum");
    }

    [Fact]
    public void HandlesNestedTypes()
    {
        var source = """
            using LinkDotNet.Enumeration;

            namespace MyNamespace;

            public partial class Outer
            {
                [Enumeration("Value")]
                internal sealed partial class Inner;
            }
            """;

        var text = GetGeneratedText(source, "Inner");

        text.ShouldContain("namespace MyNamespace;");
        text.ShouldContain("partial class Outer");
        text.ShouldContain("internal sealed partial class Inner");
    }

    [Fact]
    public void RespectsProtectedInternalAccessibility()
    {
        var source = """
            using LinkDotNet.Enumeration;

            public partial class Outer
            {
                [Enumeration("A", "B")]
                protected internal sealed partial record MyEnum;
            }
            """;

        var text = GetGeneratedText(source, "MyEnum");

        text.ShouldContain("protected internal sealed partial record MyEnum");
    }

    [Fact]
    public void RespectsPrivateProtectedAccessibility()
    {
        var source = """
            using LinkDotNet.Enumeration;

            public partial class Outer
            {
                [Enumeration("A", "B")]
                private protected sealed partial record MyEnum;
            }
            """;

        var text = GetGeneratedText(source, "MyEnum");

        text.ShouldContain("private protected sealed partial record MyEnum");
    }

    [Fact]
    public void EmitsGenerateJsonConverterPropertyInAttributeSource()
    {
        var result = RunGenerator(string.Empty);

        var attributeFile = result.GeneratedTrees.SingleOrDefault(t => t.FilePath.EndsWith("EnumerationAttribute.g.cs"));
        attributeFile.ShouldNotBeNull();

        var text = attributeFile.GetText(TestContext.Current.CancellationToken).ToString();
        text.ShouldContain("public bool GenerateJsonConverter { get; init; }");
    }

    [Fact]
    public void DoesNotGenerateJsonConverter_WhenNotEnabled()
    {
        var source = """
            using LinkDotNet.Enumeration;

            [Enumeration("Red", "Green")]
            public sealed partial record Color;
            """;

        var text = GetGeneratedText(source, "Color");

        text.ShouldNotContain("JsonConverter");
        text.ShouldNotContain("System.Text.Json");
    }

    [Fact]
    public void GeneratesJsonConverterAttribute_WhenEnabled()
    {
        var source = """
            using LinkDotNet.Enumeration;

            [Enumeration("Red", "Green", "Blue", GenerateJsonConverter = true)]
            public sealed partial record Color;
            """;

        var text = GetGeneratedText(source, "Color");

        text.ShouldContain("[JsonConverter(typeof(ColorJsonConverter))]");
    }

    [Fact]
    public void GeneratesJsonConverterClass_WhenEnabled()
    {
        var source = """
            using LinkDotNet.Enumeration;

            [Enumeration("Red", "Green", GenerateJsonConverter = true)]
            public sealed partial record Color;
            """;

        var text = GetGeneratedText(source, "Color");

        text.ShouldContain("using System.Text.Json;");
        text.ShouldContain("using System.Text.Json.Serialization;");
        text.ShouldContain("public sealed class ColorJsonConverter : JsonConverter<Color>");
        text.ShouldContain("public override Color Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)");
        text.ShouldContain("if (reader.TokenType != JsonTokenType.String)");
        text.ShouldContain("throw new JsonException(");
        text.ShouldContain("Color.TryCreate(key, out var result)");
        text.ShouldContain("public override void Write(Utf8JsonWriter writer, Color value, JsonSerializerOptions options)");
        text.ShouldContain("writer.WriteStringValue(value.Key);");
    }

    [Fact]
    public void JsonConverter_CompilesWithoutErrors()
    {
        var source = """
            using LinkDotNet.Enumeration;

            [Enumeration("Red", "Green", "Blue", GenerateJsonConverter = true)]
            public sealed partial record Color;
            """;

        var generatorResult = RunGenerator(source);
        var allTrees = generatorResult.GeneratedTrees
            .Append(CSharpSyntaxTree.ParseText(source, cancellationToken: TestContext.Current.CancellationToken))
            .ToArray();

        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(static a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(static a => MetadataReference.CreateFromFile(a.Location))
            .ToArray();

        var fullCompilation = CSharpCompilation.Create(
            "JsonConverterTestAssembly",
            allTrees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var errors = fullCompilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToImmutableArray();

        errors.ShouldBeEmpty();
    }

    [Fact]
    public void JsonConverter_Integration_SerializesAndDeserializesCorrectly()
    {
        var source = """
            using LinkDotNet.Enumeration;

            [Enumeration("Red", "Green", "Blue", GenerateJsonConverter = true)]
            public sealed partial record Color;
            """;

        var testHelperSource = """
            using System.Text.Json;
            public static class TestHelper
            {
                public static string Serialize(Color value) => JsonSerializer.Serialize(value);
                public static Color? Deserialize(string json) => JsonSerializer.Deserialize<Color>(json);
            }
            """;

        var assembly = BuildAndLoad(source, testHelperSource);
        var colorType = assembly.GetType("Color")!;
        var testHelper = assembly.GetType("TestHelper")!;

        var redField = colorType.GetField("Red")!.GetValue(null)!;
        var greenField = colorType.GetField("Green")!.GetValue(null)!;

        var serializeMethod = testHelper.GetMethod("Serialize")!;
        var deserializeMethod = testHelper.GetMethod("Deserialize")!;

        // Serialize: Color.Red => exactly "\"Red\""
        var serialized = (string)serializeMethod.Invoke(null, [redField])!;
        serialized.ShouldBe("\"Red\"");

        // Deserialize: "\"Green\"" => Color.Green (same instance / equal)
        var deserialized = deserializeMethod.Invoke(null, ["\"Green\""])!;
        deserialized.ShouldBe(greenField);
    }

    [Fact]
    public void JsonConverter_Integration_ThrowsJsonException_ForUnknownKey()
    {
        var source = """
            using LinkDotNet.Enumeration;

            [Enumeration("Red", "Green", GenerateJsonConverter = true)]
            public sealed partial record Color;
            """;

        var testHelperSource = """
            using System.Text.Json;
            public static class TestHelper
            {
                public static Color? Deserialize(string json) => JsonSerializer.Deserialize<Color>(json);
            }
            """;

        var assembly = BuildAndLoad(source, testHelperSource);
        var deserializeMethod = assembly.GetType("TestHelper")!.GetMethod("Deserialize")!;

        var ex = Should.Throw<TargetInvocationException>(() => deserializeMethod.Invoke(null, ["\"Purple\""]));
        ex.InnerException.ShouldBeOfType<System.Text.Json.JsonException>();
    }

    [Fact]
    public void JsonConverter_Integration_ThrowsJsonException_ForNonStringToken()
    {
        var source = """
            using LinkDotNet.Enumeration;

            [Enumeration("Red", "Green", GenerateJsonConverter = true)]
            public sealed partial record Color;
            """;

        var testHelperSource = """
            using System.Text.Json;
            public static class TestHelper
            {
                public static Color? Deserialize(string json) => JsonSerializer.Deserialize<Color>(json);
            }
            """;

        var assembly = BuildAndLoad(source, testHelperSource);
        var deserializeMethod = assembly.GetType("TestHelper")!.GetMethod("Deserialize")!;

        // Number token instead of string
        var ex = Should.Throw<TargetInvocationException>(() => deserializeMethod.Invoke(null, ["42"]));
        ex.InnerException.ShouldBeOfType<System.Text.Json.JsonException>();
    }

    [Fact]
    public void GeneratesDebuggerDisplayAttribute()
    {
        var source = """
            using LinkDotNet.Enumeration;

            [Enumeration("Red", "Green")]
            public sealed partial record Color;
            """;

        var text = GetGeneratedText(source, "Color");

        text.ShouldContain("[DebuggerDisplay(\"{Key}\")]");
        text.ShouldContain("using System.Diagnostics;");
    }

    [Fact]
    public void GeneratesSpanTryCreate()
    {
        var source = """
            using LinkDotNet.Enumeration;

            [Enumeration("Red", "Green", "Blue")]
            public sealed partial record Color;
            """;

        var text = GetGeneratedText(source, "Color");

        text.ShouldContain("public static bool TryCreate(ReadOnlySpan<char> key, [NotNullWhen(true)] out Color? value)");
        text.ShouldContain("value = key switch");
        text.ShouldContain("\"Red\" => Red,");
        text.ShouldContain("\"Green\" => Green,");
        text.ShouldContain("\"Blue\" => Blue,");
    }

    [Fact]
    public void GeneratesISpanFormattableInterface()
    {
        var source = """
            using LinkDotNet.Enumeration;

            [Enumeration("Red", "Green")]
            public sealed partial record Color;
            """;

        var text = GetGeneratedText(source, "Color");

        text.ShouldContain("ISpanFormattable");
        text.ShouldContain("public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)");
        text.ShouldContain("Key.AsSpan().TryCopyTo(destination)");
        text.ShouldContain("charsWritten = Key.Length;");
        text.ShouldContain("public string ToString(string? format, IFormatProvider? provider) => Key;");
    }

    [Fact]
    public void SpanFeatures_Integration_TryCreateWithSpan()
    {
        var source = """
            using LinkDotNet.Enumeration;

            [Enumeration("Red", "Green", "Blue")]
            public sealed partial record Color;
            """;

        var testHelperSource = """
            using System;
            public static class TestHelper
            {
                public static object? TryCreateFromSpan(string key)
                {
                    Color.TryCreate(key.AsSpan(), out var result);
                    return result;
                }
                public static bool TryCreateUnknown()
                {
                    return Color.TryCreate("Purple".AsSpan(), out _);
                }
                public static string TryFormatColor(object colorObj)
                {
                    var color = (Color)colorObj;
                    Span<char> buf = stackalloc char[32];
                    color.TryFormat(buf, out var written, default, null);
                    return new string(buf.Slice(0, written));
                }
            }
            """;

        var assembly = BuildAndLoad(source, testHelperSource);
        var testHelper = assembly.GetType("TestHelper")!;
        var colorType = assembly.GetType("Color")!;

        var redField = colorType.GetField("Red")!.GetValue(null)!;

        // TryCreate(ReadOnlySpan<char>) finds known value
        var result = testHelper.GetMethod("TryCreateFromSpan")!.Invoke(null, ["Red"])!;
        result.ShouldBe(redField);

        // TryCreate(ReadOnlySpan<char>) returns false for unknown
        var found = (bool)testHelper.GetMethod("TryCreateUnknown")!.Invoke(null, [])!;
        found.ShouldBeFalse();

        // TryFormat writes the key into a span
        var formatted = (string)testHelper.GetMethod("TryFormatColor")!.Invoke(null, [redField])!;
        formatted.ShouldBe("Red");
    }

    [Fact]
    public void GeneratesIsDefinedMethods()
    {
        var source = """
            using LinkDotNet.Enumeration;

            [Enumeration("A", "B")]
            public sealed partial record Foo;
            """;

        var text = GetGeneratedText(source, "Foo");

        text.ShouldContain("public static bool IsDefined(string? key) => TryCreate(key, out _);");
        text.ShouldContain("public static bool IsDefined(ReadOnlySpan<char> key) => TryCreate(key, out _);");
    }

    [Fact]
    public void IsDefined_Integration_ReturnsCorrectResults()
    {
        var source = """
            using LinkDotNet.Enumeration;

            [Enumeration("Red", "Green")]
            public sealed partial record Color;
            """;

        var testHelperSource = """
            using System;
            public static class TestHelper
            {
                public static bool IsDefinedString(string key) => Color.IsDefined(key);
                public static bool IsDefinedSpan(string key) => Color.IsDefined(key.AsSpan());
            }
            """;

        var assembly = BuildAndLoad(source, testHelperSource);
        var testHelper = assembly.GetType("TestHelper")!;

        var isDefinedString = testHelper.GetMethod("IsDefinedString")!;
        var isDefinedSpan = testHelper.GetMethod("IsDefinedSpan")!;

        ((bool)isDefinedString.Invoke(null, ["Red"])!).ShouldBeTrue();
        ((bool)isDefinedString.Invoke(null, ["Blue"])!).ShouldBeFalse();
        ((bool)isDefinedSpan.Invoke(null, ["Green"])!).ShouldBeTrue();
        ((bool)isDefinedSpan.Invoke(null, ["Yellow"])!).ShouldBeFalse();
    }

    [Fact]
    public void GeneratesMatchWithState()
    {
        var source = """
            using LinkDotNet.Enumeration;

            [Enumeration("A", "B")]
            public sealed partial record Foo;
            """;

        var text = GetGeneratedText(source, "Foo");

        text.ShouldContain("public T Match<T, TState>(TState state, Func<TState, T> onA, Func<TState, T> onB)");
        text.ShouldContain("public void Match<TState>(TState state, Action<TState> onA, Action<TState> onB)");
    }

    [Fact]
    public void MatchWithState_Integration_PassesStateAndReturnsValue()
    {
        var source = """
            using LinkDotNet.Enumeration;

            [Enumeration("A", "B")]
            public sealed partial record Foo;
            """;

        var testHelperSource = """
            using System;
            public static class TestHelper
            {
                public static string MatchWithState(object fooObj, string state)
                {
                    var foo = (Foo)fooObj;
                    return foo.Match(state, 
                        s => "Got A with " + s,
                        s => "Got B with " + s);
                }
            }
            """;

        var assembly = BuildAndLoad(source, testHelperSource);
        var testHelper = assembly.GetType("TestHelper")!;
        var fooType = assembly.GetType("Foo")!;

        var aField = fooType.GetField("A")!.GetValue(null)!;
        var bField = fooType.GetField("B")!.GetValue(null)!;

        var matchMethod = testHelper.GetMethod("MatchWithState")!;

        ((string)matchMethod.Invoke(null, [aField, "hello"])!).ShouldBe("Got A with hello");
        ((string)matchMethod.Invoke(null, [bField, "world"])!).ShouldBe("Got B with world");
    }

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

    private static Assembly BuildAndLoad(string source, params string[] additionalSources)
    {
        var generatorResult = RunGenerator(source);
        var allTrees = generatorResult.GeneratedTrees
            .Append(CSharpSyntaxTree.ParseText(source, cancellationToken: TestContext.Current.CancellationToken))
            .Concat(additionalSources.Select(s =>
                CSharpSyntaxTree.ParseText(s, cancellationToken: TestContext.Current.CancellationToken)))
            .ToArray();

        // Explicitly include assemblies the generated code may reference so the result is
        // independent of which assemblies other tests happen to have loaded first.
        var explicitAssemblies = new[]
        {
            typeof(System.Text.Json.JsonSerializer).Assembly,
            typeof(System.Text.Json.Serialization.JsonConverter<>).Assembly,
        };

        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Concat(explicitAssemblies)
            .Where(static a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Distinct()
            .Select(static a => MetadataReference.CreateFromFile(a.Location))
            .ToArray();

        var compilation = CSharpCompilation.Create(
            "IntegrationTestAssembly_" + Guid.NewGuid().ToString("N"),
            allTrees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var ms = new MemoryStream();
        var emitResult = compilation.Emit(ms, cancellationToken: TestContext.Current.CancellationToken);
        emitResult.Success.ShouldBeTrue(
            string.Join("\n", emitResult.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => d.ToString())));

        return Assembly.Load(ms.ToArray());
    }
}
