using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace DuckiDuckiCastingLib.Tests;

/// <summary>Runs <see cref="DuckCastGenerator"/> through a <see cref="GeneratorDriver"/> over a set of source files.</summary>
internal static class GeneratorTestHost
{
    public static readonly string CasesRoot = Path.Combine(AppContext.BaseDirectory, "Cases");

    private static readonly CSharpParseOptions ParseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);

    private static readonly CSharpCompilationOptions CompilationOptions =
        new(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable);

    private static readonly ImmutableArray<MetadataReference> RuntimeReferences = Directory
        .GetFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dll")
        .Where(path =>
        {
            string name = Path.GetFileName(path);
            return name.StartsWith("System", StringComparison.Ordinal) || name is "netstandard.dll" or "mscorlib.dll" or "Microsoft.CSharp.dll";
        })
        .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
        .ToImmutableArray();

    public static IEnumerable<string> CaseNames()
        => Directory.GetDirectories(CasesRoot).Select(dir => Path.GetFileName(dir)).OrderBy(name => name, StringComparer.Ordinal);

    public sealed record RunResult(GeneratorDriver Driver, CSharpCompilation Input, Compilation Output, ImmutableArray<Diagnostic> GeneratorDiagnostics)
    {
        public IEnumerable<Diagnostic> AllDiagnostics => GeneratorDiagnostics.Concat(Output.GetDiagnostics());

        public ImmutableArray<GeneratedSourceResult> GeneratedSources => Driver.GetRunResult().Results[0].GeneratedSources;

        /// <summary>Ids of everything at Warning or above, sorted, for comparison with expected.txt.</summary>
        public List<string> ReportedIds => AllDiagnostics
            .Where(d => d.Severity >= DiagnosticSeverity.Warning)
            .Select(d => d.Id)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();
    }

    public static SyntaxTree Parse(string path)
        => CSharpSyntaxTree.ParseText(File.ReadAllText(path), ParseOptions, path: path);

    public static SyntaxTree ParseText(string text, string path)
        => CSharpSyntaxTree.ParseText(text, ParseOptions, path: path);

    public static RunResult Run(string assemblyName, IEnumerable<string> files, IEnumerable<MetadataReference>? extraReferences = null)
    {
        var input = CSharpCompilation.Create(
            assemblyName,
            files.Select(Parse),
            RuntimeReferences.Concat(extraReferences ?? Enumerable.Empty<MetadataReference>()),
            CompilationOptions);

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: new[] { new DuckCastGenerator().AsSourceGenerator() },
            parseOptions: ParseOptions,
            driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));

        driver = driver.RunGeneratorsAndUpdateCompilation(input, out Compilation output, out ImmutableArray<Diagnostic> diagnostics);
        return new RunResult(driver, input, output, diagnostics);
    }

    public static byte[] Emit(Compilation compilation)
    {
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        Assert.True(result.Success, "Emit failed:\n" + string.Join("\n", result.Diagnostics));
        return stream.ToArray();
    }

    /// <summary>Loads the compiled case and invokes <c>Test.Run()</c>; null when the case has no such method.</summary>
    public static string? InvokeTestRun(byte[] image, IReadOnlyDictionary<string, byte[]> dependencies)
    {
        var context = new ImageLoadContext(dependencies);
        Assembly assembly = context.LoadFromStream(new MemoryStream(image));
        Type? test = assembly.GetTypes().FirstOrDefault(t => t.Name == "Test");
        MethodInfo? run = test?.GetMethod("Run", BindingFlags.Public | BindingFlags.Static, Type.EmptyTypes);
        if (run is null)
        {
            return null;
        }

        try
        {
            return (string?)run.Invoke(null, null);
        }
        catch (TargetInvocationException e)
        {
            return "EXCEPTION: " + e.InnerException;
        }
    }

    /// <summary>Run reasons of every output of the tracked steps whose name starts with "Duck", plus the output steps.</summary>
    public static Dictionary<string, List<IncrementalStepRunReason>> StepReasons(GeneratorDriver driver)
    {
        GeneratorRunResult result = driver.GetRunResult().Results[0];
        var reasons = new Dictionary<string, List<IncrementalStepRunReason>>(StringComparer.Ordinal);
        foreach (var step in result.TrackedSteps.Where(kv => kv.Key.StartsWith("Duck", StringComparison.Ordinal)).Concat(result.TrackedOutputSteps))
        {
            reasons[step.Key] = step.Value.SelectMany(s => s.Outputs).Select(o => o.Reason).ToList();
        }

        return reasons;
    }

    private sealed class ImageLoadContext : AssemblyLoadContext
    {
        private readonly IReadOnlyDictionary<string, byte[]> _dependencies;

        public ImageLoadContext(IReadOnlyDictionary<string, byte[]> dependencies) => _dependencies = dependencies;

        protected override Assembly? Load(AssemblyName name)
            => name.Name is not null && _dependencies.TryGetValue(name.Name, out byte[]? bytes)
                ? LoadFromStream(new MemoryStream(bytes))
                : null;
    }
}

/// <summary>One directory under Cases: main sources, an optional lib/ compiled first, expected.txt and an optional edit.cs.</summary>
internal sealed class TestCase
{
    public string Name { get; }
    public string Directory { get; }
    public string[] MainFiles { get; }
    public string[] LibFiles { get; }
    public string? EditFile { get; }
    public List<string> ExpectedIds { get; }

    private TestCase(string name)
    {
        Name = name;
        Directory = Path.Combine(GeneratorTestHost.CasesRoot, name);
        MainFiles = System.IO.Directory.GetFiles(Directory, "*.cs").Where(f => Path.GetFileName(f) != "edit.cs").OrderBy(f => f, StringComparer.Ordinal).ToArray();
        string libDir = Path.Combine(Directory, "lib");
        LibFiles = System.IO.Directory.Exists(libDir) ? System.IO.Directory.GetFiles(libDir, "*.cs") : Array.Empty<string>();
        string edit = Path.Combine(Directory, "edit.cs");
        EditFile = File.Exists(edit) ? edit : null;
        string expected = Path.Combine(Directory, "expected.txt");
        ExpectedIds = File.Exists(expected)
            ? File.ReadAllLines(expected).Select(l => l.Trim()).Where(l => l.Length > 0).OrderBy(l => l, StringComparer.Ordinal).ToList()
            : new List<string>();
    }

    public static TestCase Load(string name) => new(name);

    /// <summary>Compiles lib/ (with the generator) and returns it as a reference plus its image for run-time loading.</summary>
    public (List<MetadataReference> References, Dictionary<string, byte[]> Images) BuildLib()
    {
        var references = new List<MetadataReference>();
        var images = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        if (LibFiles.Length == 0)
        {
            return (references, images);
        }

        string libName = Name + ".Lib";
        var lib = GeneratorTestHost.Run(libName, LibFiles);
        Assert.DoesNotContain(lib.AllDiagnostics, d => d.Severity >= DiagnosticSeverity.Warning);
        byte[] image = GeneratorTestHost.Emit(lib.Output);
        references.Add(MetadataReference.CreateFromImage(image));
        images[libName] = image;
        return (references, images);
    }
}
