using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace DuckiDuckiCastingLib
{
    [Generator]
    internal class DuckCastGenerator : IIncrementalGenerator
    {
        public const string ANNOTATIONS = "DuckiDuckiCastingLib.Annotations";
        public const string SupportsDuckCastAttributeName = "SupportsDuckCastAttribute";

        /// <summary>
        /// Fully qualified type names with nullable reference type annotations preserved.
        /// </summary>
        private static readonly SymbolDisplayFormat TypeFormat = SymbolDisplayFormat.FullyQualifiedFormat
            .WithMiscellaneousOptions(SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions
                | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            context.RegisterPostInitializationOutput(ctx =>
            {
                ctx.AddSource($"{SupportsDuckCastAttributeName}.g.cs", $$"""
            namespace {{ANNOTATIONS}};

            [global::System.AttributeUsage(global::System.AttributeTargets.Interface)]
            [global::System.Diagnostics.Conditional("DUCKCAST_KEEP_ATTRIBUTES")]
            internal sealed class {{SupportsDuckCastAttributeName}} : global::System.Attribute
            {
            } 
            """);
            });
            var pipeline = context.SyntaxProvider.ForAttributeWithMetadataName(
                fullyQualifiedMetadataName: ANNOTATIONS + "." + SupportsDuckCastAttributeName,
                predicate: static (syntaxNode, cancellationToken) => syntaxNode is InterfaceDeclarationSyntax,
                transform: static (context, cancellationToken) =>
                {
                    var iface = (INamedTypeSymbol) context.TargetSymbol;
                    var methods = new EquatableList<DuckMethod>();
                    var properties = new EquatableList<DuckProperty>();

                    // Own members plus everything inherited from base interfaces:
                    // the adapter has to implement all of them.
                    IEnumerable<ISymbol> members = iface.GetMembers()
                        .Concat(iface.AllInterfaces.SelectMany(baseIface => baseIface.GetMembers()));

                    foreach (ISymbol member in members)
                    {
                        // Static members cannot be forwarded through an instance,
                        // default interface implementations already have a body.
                        if (member.IsStatic || !member.IsAbstract)
                        {
                            continue;
                        }

                        switch (member)
                        {
                            case IMethodSymbol { MethodKind: MethodKind.Ordinary } method:
                                methods.Add(ToDuckMethod(method));
                                break;
                            case IPropertySymbol property:
                                properties.Add(ToDuckProperty(property));
                                break;
                        }
                    }

                    return new Model(
                        Namespace: iface.ContainingNamespace.IsGlobalNamespace ? null : iface.ContainingNamespace.ToDisplayString(),
                        InterfaceName: iface.Name,
                        Accessibility: iface.DeclaredAccessibility,
                        TypeParameters: new EquatableList<string>(iface.TypeParameters.Select(tp => tp.Name)),
                        Methods: methods,
                        Properties: properties);
                }
            );

            //context.RegisterSourceOutput(pipeline, static (context, model) =>
            //{
            //    var exclusiveParameters = model.FuncParameters.Where(param => !model.FactoryParameters.Contains(param)).ToList();
            //    var sourceText = SourceText.From($$"""
            //    namespace {{model.Namespace}};
            //    {{SyntaxFacts.GetText(model.ClassAccessibility)}} partial class {{model.ClassName}}
            //    {
            //        {{SyntaxFacts.GetText(model.MethodAccessibility)}} System.Func<{{string.Join(", ", exclusiveParameters.Select(param => param.Type))}}, {{model.ReturnType}}> Get{{model.MethodName}}Factory({{string.Join(", ", model.FactoryParameters.Select(param => param.Type + " " + param.Name))}})
            //        {
            //            return ({{string.Join(", ", exclusiveParameters.Select(param => param.Name))}}) => {{model.MethodName}}({{string.Join(", ", model.FuncParameters.Select(param => param.Name))}});
            //        }
            //    }
            //    """, Encoding.UTF8);

            //    context.AddSource($"{model.ClassName}_{model.MethodName}.g.cs", sourceText);
            //});
        }

        private static DuckMethod ToDuckMethod(IMethodSymbol method) => new(
            Name: method.Name,
            ReturnType: method.ReturnType.ToDisplayString(TypeFormat),
            ReturnsVoid: method.ReturnsVoid,
            TypeParameters: new EquatableList<string>(method.TypeParameters.Select(tp => tp.Name)),
            Parameters: ToDuckParameters(method.Parameters));

        private static DuckProperty ToDuckProperty(IPropertySymbol property) => new(
            Name: property.Name,
            Type: property.Type.ToDisplayString(TypeFormat),
            HasGetter: property.GetMethod is not null,
            HasSetter: property.SetMethod is not null,
            IsInitOnly: property.SetMethod?.IsInitOnly == true,
            Parameters: ToDuckParameters(property.Parameters));

        private static EquatableList<DuckParameter> ToDuckParameters(ImmutableArray<IParameterSymbol> parameters) => new(
            parameters.Select(p => new DuckParameter(p.Type.ToDisplayString(TypeFormat), p.Name, p.RefKind, p.IsParams)));

        private sealed record Model(
            string? Namespace,
            string InterfaceName,
            Accessibility Accessibility,
            EquatableList<string> TypeParameters,
            EquatableList<DuckMethod> Methods,
            EquatableList<DuckProperty> Properties);

        private sealed record DuckMethod(
            string Name,
            string ReturnType,
            bool ReturnsVoid,
            EquatableList<string> TypeParameters,
            EquatableList<DuckParameter> Parameters);

        private sealed record DuckParameter(string Type, string Name, RefKind RefKind, bool IsParams);

        private sealed record DuckProperty(
            string Name,
            string Type,
            bool HasGetter,
            bool HasSetter,
            bool IsInitOnly,                          // set vs init
            EquatableList<DuckParameter> Parameters); // пусто для обычного свойства, заполнено для индексатора
    }
}
