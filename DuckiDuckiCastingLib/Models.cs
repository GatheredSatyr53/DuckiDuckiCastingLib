using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace DuckiDuckiCastingLib
{
    // Everything that flows between pipeline stages lives here. All of it is plain data with
    // value equality (records, strings, enums, EquatableList) and never references symbols,
    // syntax nodes or the compilation: that is what lets Roslyn cache the downstream stages.

    /// <summary>Type parameter of a type declaration, with its variance (partial parts must repeat it).</summary>
    internal sealed record DuckTypeParameter(string Name, VarianceKind Variance);

    /// <summary>A type that contains the duck interface (outermost first); re-opened as a partial part.</summary>
    internal sealed record ContainingType(string Keyword, string Name, EquatableList<DuckTypeParameter> TypeParameters);

    /// <summary>An interface marked with [SupportsDuckCast]. Produced by the attribute stage.</summary>
    internal sealed record InterfaceModel(
        string Key,                                   // original definition, fully qualified: global::Ns.IFoo<T>
        string? Namespace,
        string Name,
        EquatableList<DuckTypeParameter> TypeParameters,
        EquatableList<ContainingType> ContainingTypes,
        string AdaptersClassName,                     // DuckiDuckiCastingLib.Generated.<AdaptersClassName> holds the adapters
        string HintName,
        EquatableList<DiagnosticInfo> Diagnostics,
        bool CanGenerate);

    /// <summary>One adapter: a concrete source type adapted to a (possibly constructed) duck interface.</summary>
    internal sealed record AdapterModel(
        string InterfaceKey,
        string InterfaceType,                         // constructed, fully qualified: global::Ns.IFoo<int>
        EquatableList<string> InterfaceTypeArguments, // for typeof(T) == typeof(int) checks
        string SourceType,                            // fully qualified, without nullable annotation
        string AdapterName,
        EquatableList<DuckMethod> Methods,
        EquatableList<DuckProperty> Properties,
        EquatableList<DuckEvent> Events);

    internal sealed record DuckMethod(
        string DeclaringInterface,
        string Name,
        string ReturnType,
        bool ReturnsVoid,
        EquatableList<string> TypeParameters,
        EquatableList<DuckParameter> Parameters);

    internal sealed record DuckParameter(string Type, string Name, RefKind RefKind);

    internal sealed record DuckProperty(
        string DeclaringInterface,
        string Name,
        string Type,
        bool HasGetter,
        bool HasSetter,
        bool IsInitOnly,                              // set vs init
        bool IsIndexer,
        EquatableList<DuckParameter> Parameters);     // empty for a plain property, filled for an indexer

    internal sealed record DuckEvent(string DeclaringInterface, string Name, string Type);

    /// <summary>Result of analysing one Cast call site: an adapter to generate or a diagnostic to report.</summary>
    internal sealed record CastSiteResult(AdapterModel? Adapter, DiagnosticInfo? Diagnostic);

    /// <summary>Input of the output stage: an interface together with every adapter it needs.</summary>
    internal sealed record GenerationUnit(InterfaceModel Interface, EquatableList<AdapterModel> Adapters);

    /// <summary>Equatable stand-in for <see cref="Diagnostic"/>; materialised only in the output stage.</summary>
    internal sealed record DiagnosticInfo(string Id, LocationInfo? Location, EquatableList<string> MessageArgs)
    {
        public static DiagnosticInfo Create(DiagnosticDescriptor descriptor, Location location, params string[] messageArgs)
            => new(descriptor.Id, LocationInfo.From(location), new EquatableList<string>(messageArgs));

        public Diagnostic ToDiagnostic()
            => Diagnostic.Create(
                DuckDiagnostics.Get(Id),
                Location?.ToLocation() ?? Microsoft.CodeAnalysis.Location.None,
                MessageArgs.ToArray());
    }

    /// <summary>Equatable stand-in for <see cref="Location"/> (a real Location pins the syntax tree).</summary>
    internal sealed record LocationInfo(
        string FilePath,
        int Start,
        int Length,
        int StartLine,
        int StartCharacter,
        int EndLine,
        int EndCharacter)
    {
        public static LocationInfo? From(Location location)
        {
            if (location.SourceTree is null)
            {
                return null;
            }

            FileLinePositionSpan lineSpan = location.GetLineSpan();
            return new LocationInfo(
                location.SourceTree.FilePath,
                location.SourceSpan.Start,
                location.SourceSpan.Length,
                lineSpan.StartLinePosition.Line,
                lineSpan.StartLinePosition.Character,
                lineSpan.EndLinePosition.Line,
                lineSpan.EndLinePosition.Character);
        }

        public Location ToLocation()
            => Location.Create(
                FilePath,
                new TextSpan(Start, Length),
                new LinePositionSpan(
                    new LinePosition(StartLine, StartCharacter),
                    new LinePosition(EndLine, EndCharacter)));
    }
}
