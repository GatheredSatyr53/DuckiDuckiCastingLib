using Microsoft.CodeAnalysis;

namespace DuckiDuckiCastingLib.Tests;

public class DuckCastGeneratorTests
{
    public static TheoryData<string> Cases
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (string name in GeneratorTestHost.CaseNames())
            {
                data.Add(name);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Case_reports_expected_diagnostics_and_passes_its_runtime_check(string caseName)
    {
        TestCase testCase = TestCase.Load(caseName);
        var (references, images) = testCase.BuildLib();

        var run = GeneratorTestHost.Run(caseName, testCase.MainFiles, references);

        Assert.Equal(testCase.ExpectedIds, run.ReportedIds);

        if (run.AllDiagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
        {
            return; // an error case: nothing to execute
        }

        string? result = GeneratorTestHost.InvokeTestRun(GeneratorTestHost.Emit(run.Output), images);
        if (result is not null)
        {
            Assert.Equal("ok", result);
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Unrelated_edit_leaves_every_pipeline_step_cached(string caseName)
    {
        TestCase testCase = TestCase.Load(caseName);
        var (references, _) = testCase.BuildLib();
        var run = GeneratorTestHost.Run(caseName, testCase.MainFiles, references);

        SyntaxTree unrelated = GeneratorTestHost.ParseText("class Unrelated { void M() => System.Console.WriteLine(1); }", "Unrelated.cs");
        GeneratorDriver driver = run.Driver.RunGenerators(run.Input.AddSyntaxTrees(unrelated));

        foreach (var (step, reasons) in GeneratorTestHost.StepReasons(driver))
        {
            Assert.All(reasons, reason => Assert.True(
                reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged,
                $"step '{step}' re-ran with reason {reason} after an unrelated edit"));
        }
    }

    [Fact]
    public void New_cast_site_regenerates_only_the_interface_it_belongs_to()
    {
        TestCase testCase = TestCase.Load("02_generic");
        Assert.NotNull(testCase.EditFile);
        var run = GeneratorTestHost.Run(testCase.Name, testCase.MainFiles);

        GeneratorDriver driver = run.Driver.RunGenerators(run.Input.AddSyntaxTrees(GeneratorTestHost.Parse(testCase.EditFile!)));
        var reasons = GeneratorTestHost.StepReasons(driver);

        // Three duck interfaces; edit.cs adds a cast to IReader<string> only.
        Assert.Equal(new[] { IncrementalStepRunReason.Modified, IncrementalStepRunReason.Unchanged, IncrementalStepRunReason.Unchanged },
            reasons[TrackingNames.Units].OrderBy(r => r));
        Assert.Equal(1, reasons["SourceOutput"].Count(r => r == IncrementalStepRunReason.Modified));
        Assert.Equal(2, reasons["SourceOutput"].Count(r => r == IncrementalStepRunReason.Cached));

        var reader = driver.GetRunResult().Results[0].GeneratedSources.Single(s => s.HintName == "Store.IReader_1.DuckCast.g.cs");
        Assert.Contains("typeof(T) == typeof(string) && typeof(TSource) == typeof(global::Store.OtherReader)", reader.SourceText.ToString());
    }

    [Fact]
    public void Generated_files_have_one_file_per_interface_with_stable_hint_names()
    {
        TestCase testCase = TestCase.Load("03_nested");
        var run = GeneratorTestHost.Run(testCase.Name, testCase.MainFiles);

        Assert.Equal(
            new[] { "Deep.Nest.Holder.IGreeter.DuckCast.g.cs", "Outer_1.Inner.IThing.DuckCast.g.cs", "SupportsDuckCastAttribute.g.cs" },
            run.GeneratedSources.Select(s => s.HintName).OrderBy(n => n, StringComparer.Ordinal));
    }
}
