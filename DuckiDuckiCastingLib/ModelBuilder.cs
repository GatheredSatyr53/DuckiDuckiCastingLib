using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;

namespace DuckiDuckiCastingLib
{
    /// <summary>
    /// Symbol analysis for both pipeline inputs. Everything here runs inside incremental transforms:
    /// it reads symbols but only ever returns equatable models (see <c>Models.cs</c>).
    /// </summary>
    internal static class ModelBuilder
    {
        /// <summary>Fully qualified names (global::), special types as keywords, no nullable annotations.</summary>
        public static readonly SymbolDisplayFormat PlainTypeFormat = SymbolDisplayFormat.FullyQualifiedFormat;

        /// <summary>Same, with nullable reference type annotations preserved. Used for member signatures.</summary>
        public static readonly SymbolDisplayFormat AnnotatedTypeFormat = PlainTypeFormat
            .WithMiscellaneousOptions(PlainTypeFormat.MiscellaneousOptions
                | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

        /// <summary>A namespace as written in a namespace declaration: no global::, keywords escaped.</summary>
        private static readonly SymbolDisplayFormat NamespaceFormat = new(
            globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);

        /// <summary>Short human readable form for diagnostics.</summary>
        private static readonly SymbolDisplayFormat MessageFormat = SymbolDisplayFormat.MinimallyQualifiedFormat;

        /// <summary>Same, without the nullable annotation (a flow-analysed argument of type T reads as "T?").</summary>
        private static readonly SymbolDisplayFormat SourceTypeMessageFormat = MessageFormat
            .WithMiscellaneousOptions(MessageFormat.MiscellaneousOptions & ~SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

        // ------------------------------------------------------------------
        // Stage 1: interfaces marked with [SupportsDuckCast]
        // ------------------------------------------------------------------

        public static InterfaceModel BuildInterface(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
        {
            var iface = (INamedTypeSymbol)context.TargetSymbol;
            var declaration = (InterfaceDeclarationSyntax)context.TargetNode;
            Compilation compilation = context.SemanticModel.Compilation;
            Location identifierLocation = declaration.Identifier.GetLocation();
            string displayName = iface.ToDisplayString(MessageFormat);
            var diagnostics = new EquatableList<DiagnosticInfo>();

            // Adapters live in DuckiDuckiCastingLib.Generated, so the interface has to be visible assembly-wide.
            if (!compilation.IsSymbolAccessibleWithin(iface, compilation.Assembly))
            {
                diagnostics.Add(DiagnosticInfo.Create(DuckDiagnostics.InterfaceNotAccessible, identifierLocation, displayName));
            }

            // Cast<T> is emitted into another partial part of the interface, so the user's part must be partial.
            if (!declaration.Modifiers.Any(SyntaxKind.PartialKeyword))
            {
                diagnostics.Add(DiagnosticInfo.Create(DuckDiagnostics.InterfaceMustBePartial, identifierLocation, displayName));
            }

            // Nested interfaces are re-opened through a chain of partial containing types (outermost first).
            var containingTypes = new EquatableList<ContainingType>();
            for (INamedTypeSymbol? outer = iface.ContainingType; outer is not null; outer = outer.ContainingType)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!IsDeclaredPartial(outer, cancellationToken))
                {
                    diagnostics.Add(DiagnosticInfo.Create(
                        DuckDiagnostics.ContainingTypeMustBePartial, identifierLocation, outer.ToDisplayString(MessageFormat), displayName));
                }

                containingTypes.Insert(0, new ContainingType(TypeKeyword(outer), outer.Name, ToTypeParameters(outer.TypeParameters)));
            }

            // Members the adapter could not implement make the whole interface unusable.
            foreach (ISymbol member in iface.GetMembers().Concat(iface.AllInterfaces.SelectMany(baseInterface => baseInterface.GetMembers())))
            {
                cancellationToken.ThrowIfCancellationRequested();
                string? reason = UnsupportedReason(member, iface);
                if (reason is not null)
                {
                    Location location = member.Locations.FirstOrDefault(l => l.IsInSource) ?? identifierLocation;
                    diagnostics.Add(DiagnosticInfo.Create(
                        DuckDiagnostics.UnsupportedMember, location, member.ToDisplayString(MessageFormat), displayName, reason));
                }
            }

            return new InterfaceModel(
                Key: InterfaceKey(iface),
                Namespace: iface.ContainingNamespace.IsGlobalNamespace ? null : iface.ContainingNamespace.ToDisplayString(NamespaceFormat),
                Name: iface.Name,
                TypeParameters: ToTypeParameters(iface.TypeParameters),
                ContainingTypes: containingTypes,
                AdaptersClassName: Sanitize(StripGlobal(InterfaceKey(iface)), allowDots: false) + "_DuckAdapters",
                HintName: BuildHintName(iface),
                Diagnostics: diagnostics,
                CanGenerate: diagnostics.Count == 0);
        }

        private static string? UnsupportedReason(ISymbol member, INamedTypeSymbol iface)
        {
            if (member.Name == DuckCastGenerator.CastMethodName && SymbolEqualityComparer.Default.Equals(member.ContainingType, iface))
            {
                return "it conflicts with the generated static Cast method";
            }

            if (member.IsStatic)
            {
                // Static virtual members have a body; static abstract ones would have to be implemented by the adapter.
                return member.IsAbstract ? "static abstract members cannot be forwarded to an instance" : null;
            }

            if (!member.IsAbstract)
            {
                return null; // default implementation keeps its body
            }

            return member switch
            {
                IMethodSymbol { MethodKind: MethodKind.Ordinary } method when method.ReturnsByRef || method.ReturnsByRefReadonly
                    => "ref returns are not supported",
                IPropertySymbol property when property.ReturnsByRef || property.ReturnsByRefReadonly
                    => "ref returns are not supported",
                _ => null,
            };
        }

        private static bool IsDeclaredPartial(INamedTypeSymbol type, CancellationToken cancellationToken)
            => type.DeclaringSyntaxReferences.Any(reference =>
                reference.GetSyntax(cancellationToken) is TypeDeclarationSyntax declaration
                && declaration.Modifiers.Any(SyntaxKind.PartialKeyword));

        private static string TypeKeyword(INamedTypeSymbol type) => type.TypeKind switch
        {
            TypeKind.Interface => "interface",
            TypeKind.Struct => type.IsRecord ? "record struct" : "struct",
            _ => type.IsRecord ? "record" : "class",
        };

        private static EquatableList<DuckTypeParameter> ToTypeParameters(ImmutableArray<ITypeParameterSymbol> typeParameters)
            => new(typeParameters.Select(tp => new DuckTypeParameter(tp.Name, tp.Variance)));

        /// <summary>Identity of a duck interface shared by both stages: its original definition, fully qualified.</summary>
        public static string InterfaceKey(INamedTypeSymbol iface) => iface.OriginalDefinition.ToDisplayString(PlainTypeFormat);

        private static string BuildHintName(INamedTypeSymbol iface)
        {
            var builder = new StringBuilder();
            if (!iface.ContainingNamespace.IsGlobalNamespace)
            {
                builder.Append(iface.ContainingNamespace.ToDisplayString()).Append('.');
            }

            var chain = new List<INamedTypeSymbol>();
            for (INamedTypeSymbol? type = iface; type is not null; type = type.ContainingType)
            {
                chain.Insert(0, type);
            }

            foreach (INamedTypeSymbol type in chain)
            {
                builder.Append(type.Name);
                if (type.Arity > 0)
                {
                    builder.Append('_').Append(type.Arity);
                }

                builder.Append('.');
            }

            builder.Append("DuckCast.g.cs");
            return Sanitize(builder.ToString(), allowDots: true);
        }

        // ------------------------------------------------------------------
        // Stage 2: IFoo.Cast(x) / IFoo.Cast<T>(x) call sites
        // ------------------------------------------------------------------

        /// <summary>Cheap syntax-only filter: <c>Something.Cast(arg)</c> or <c>Something.Cast&lt;T&gt;(arg)</c>.</summary>
        public static bool IsCastInvocation(SyntaxNode node)
            => node is InvocationExpressionSyntax
            {
                ArgumentList.Arguments.Count: 1,
                Expression: MemberAccessExpressionSyntax
                {
                    Name: IdentifierNameSyntax { Identifier.ValueText: DuckCastGenerator.CastMethodName }
                        or GenericNameSyntax { Identifier.ValueText: DuckCastGenerator.CastMethodName, TypeArgumentList.Arguments.Count: 1 },
                },
            };

        public static CastSiteResult? BuildCastSite(GeneratorSyntaxContext context, CancellationToken cancellationToken)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;
            var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
            SemanticModel semanticModel = context.SemanticModel;

            // The receiver must be an interface type. The generated Cast itself is invisible to the generator
            // (generators do not see their own output), so the receiver is bound, never the method.
            if (semanticModel.GetSymbolInfo(memberAccess.Expression, cancellationToken).Symbol is not INamedTypeSymbol { TypeKind: TypeKind.Interface } iface)
            {
                return null;
            }

            INamedTypeSymbol definition = iface.OriginalDefinition;
            var compilation = (CSharpCompilation)semanticModel.Compilation;
            Location location = invocation.GetLocation();

            // Source type: an explicit type argument wins, otherwise the natural type of the argument.
            ITypeSymbol? sourceType = memberAccess.Name is GenericNameSyntax genericName
                ? semanticModel.GetTypeInfo(genericName.TypeArgumentList.Arguments[0], cancellationToken).Type
                : semanticModel.GetTypeInfo(invocation.ArgumentList.Arguments[0].Expression, cancellationToken).Type;

            if (!SymbolEqualityComparer.Default.Equals(definition.ContainingAssembly, compilation.Assembly))
            {
                // Adapters can only be generated next to the interface. [SupportsDuckCast] is [Conditional] and
                // therefore usually missing from metadata, so a foreign duck interface is recognised by its Cast method.
                if (HasDuckAttribute(definition) || HasGeneratedCast(definition))
                {
                    return new CastSiteResult(null, DiagnosticInfo.Create(
                        DuckDiagnostics.CrossAssemblyCast, location,
                        iface.ToDisplayString(MessageFormat), sourceType?.ToDisplayString(SourceTypeMessageFormat) ?? "?"));
                }

                return null;
            }

            if (!HasDuckAttribute(definition))
            {
                return null;
            }

            if (sourceType is null)
            {
                return Problem("the argument has no natural type; specify the type argument explicitly, for example Cast<MyType>(...)");
            }

            if (sourceType.TypeKind == TypeKind.Error)
            {
                return null; // the compiler already reports this
            }

            // A source that already converts to the interface needs no adapter: Cast returns it as is.
            Conversion existing = compilation.ClassifyConversion(sourceType, iface);
            if (existing.IsImplicit && !existing.IsUserDefined)
            {
                return null;
            }

            string? problem = DescribeUnusableType(sourceType);
            if (problem is null && !IsAccessibleFromGeneratedCode(sourceType, compilation))
            {
                problem = $"the source type '{sourceType.ToDisplayString(SourceTypeMessageFormat)}' is not accessible from generated code; it must be public or internal";
            }

            if (problem is null && AllTypeArguments(iface).Any(ContainsTypeParameter))
            {
                problem = $"'{iface.ToDisplayString(MessageFormat)}' has open type arguments; they must be concrete types";
            }

            if (problem is not null)
            {
                return Problem(problem);
            }

            // Match every abstract instance member of the (constructed) interface against the source type.
            var methods = new EquatableList<DuckMethod>();
            var properties = new EquatableList<DuckProperty>();
            var events = new EquatableList<DuckEvent>();
            var missing = new List<string>();

            foreach (ISymbol member in AbstractInstanceMembers(iface))
            {
                cancellationToken.ThrowIfCancellationRequested();
                switch (member)
                {
                    case IMethodSymbol { MethodKind: MethodKind.Ordinary } method:
                        if (FindMethod(sourceType, method, compilation) is null)
                        {
                            missing.Add(Quote(method));
                        }
                        else
                        {
                            methods.Add(ToDuckMethod(method));
                        }

                        break;

                    case IPropertySymbol property:
                        if (!HasMatchingProperty(sourceType, property, compilation))
                        {
                            missing.Add(Quote(property));
                        }
                        else
                        {
                            properties.Add(ToDuckProperty(property));
                        }

                        break;

                    case IEventSymbol @event:
                        if (!HasMatchingEvent(sourceType, @event, compilation))
                        {
                            missing.Add(Quote(@event));
                        }
                        else
                        {
                            events.Add(new DuckEvent(DeclaringInterface(@event), @event.Name, @event.Type.ToDisplayString(AnnotatedTypeFormat)));
                        }

                        break;
                }
            }

            if (missing.Count > 0)
            {
                return new CastSiteResult(null, DiagnosticInfo.Create(
                    DuckDiagnostics.MemberMismatch, location,
                    sourceType.ToDisplayString(SourceTypeMessageFormat), iface.ToDisplayString(MessageFormat), string.Join(", ", missing)));
            }

            var adapter = new AdapterModel(
                InterfaceKey: InterfaceKey(iface),
                InterfaceType: iface.ToDisplayString(PlainTypeFormat),
                InterfaceTypeArguments: new EquatableList<string>(AllTypeArguments(iface).Select(t => t.ToDisplayString(PlainTypeFormat))),
                SourceType: sourceType.ToDisplayString(PlainTypeFormat),
                AdapterName: BuildAdapterName(sourceType, iface),
                Methods: methods,
                Properties: properties,
                Events: events);

            return new CastSiteResult(adapter, null);

            CastSiteResult Problem(string reason)
                => new(null, DiagnosticInfo.Create(DuckDiagnostics.SourceTypeNotConcrete, location, reason));
        }

        /// <summary>Type arguments of the containing types (outermost first) followed by the interface's own.</summary>
        private static IEnumerable<ITypeSymbol> AllTypeArguments(INamedTypeSymbol type)
        {
            var chain = new List<INamedTypeSymbol>();
            for (INamedTypeSymbol? current = type; current is not null; current = current.ContainingType)
            {
                chain.Insert(0, current);
            }

            return chain.SelectMany(t => t.TypeArguments);
        }

        private static bool HasDuckAttribute(INamedTypeSymbol type)
            => type.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() == DuckCastGenerator.AttributeFullName);

        private static bool HasGeneratedCast(INamedTypeSymbol type)
            => type.GetMembers(DuckCastGenerator.CastMethodName)
                .OfType<IMethodSymbol>()
                .Any(method => method.IsStatic && method.Arity == 1 && method.Parameters.Length == 1);

        private static string? DescribeUnusableType(ITypeSymbol type)
        {
            string display = type.ToDisplayString(SourceTypeMessageFormat);
            if (type.IsAnonymousType)
            {
                return "the source type is anonymous and cannot be referenced from generated code";
            }

            if (type.TypeKind == TypeKind.Dynamic)
            {
                return "the source type is 'dynamic'";
            }

            if (type.TypeKind is TypeKind.Pointer or TypeKind.FunctionPointer)
            {
                return $"the source type '{display}' is a pointer type";
            }

            if (type.IsRefLikeType)
            {
                return $"the source type '{display}' is a ref struct and cannot be stored in an adapter";
            }

            if (ContainsTypeParameter(type))
            {
                return $"the source type '{display}' involves a type parameter; it must be known at compile time";
            }

            if (type is INamedTypeSymbol { IsStatic: true })
            {
                return $"the source type '{display}' is a static class";
            }

            return null;
        }

        /// <summary>The type and every type it is built from must be visible assembly-wide.</summary>
        private static bool IsAccessibleFromGeneratedCode(ITypeSymbol type, CSharpCompilation compilation) => type switch
        {
            IArrayTypeSymbol array => IsAccessibleFromGeneratedCode(array.ElementType, compilation),
            INamedTypeSymbol named => compilation.IsSymbolAccessibleWithin(named, compilation.Assembly)
                && named.TypeArguments.All(argument => IsAccessibleFromGeneratedCode(argument, compilation)),
            _ => true,
        };

        private static bool ContainsTypeParameter(ITypeSymbol type) => type switch
        {
            ITypeParameterSymbol => true,
            IArrayTypeSymbol array => ContainsTypeParameter(array.ElementType),
            IPointerTypeSymbol pointer => ContainsTypeParameter(pointer.PointedAtType),
            INamedTypeSymbol named => named.TypeArguments.Any(ContainsTypeParameter)
                || (named.ContainingType is not null && ContainsTypeParameter(named.ContainingType)),
            _ => false,
        };

        /// <summary>Own and inherited members the adapter has to implement: abstract, non-static.</summary>
        private static IEnumerable<ISymbol> AbstractInstanceMembers(INamedTypeSymbol iface)
            => iface.GetMembers()
                .Concat(iface.AllInterfaces.SelectMany(baseInterface => baseInterface.GetMembers()))
                .Where(member => member.IsAbstract && !member.IsStatic);

        private static string Quote(ISymbol symbol) => "'" + symbol.ToDisplayString(MessageFormat) + "'";

        private static string DeclaringInterface(ISymbol member) => member.ContainingType.ToDisplayString(PlainTypeFormat);

        // ------------------------------------------------------------------
        // Member matching (structural typing)
        // ------------------------------------------------------------------

        private static IMethodSymbol? FindMethod(ITypeSymbol source, IMethodSymbol target, CSharpCompilation compilation)
        {
            IAssemblySymbol within = compilation.Assembly;
            foreach (ISymbol candidate in LookupMembers(source, target.Name))
            {
                if (candidate is not IMethodSymbol { MethodKind: MethodKind.Ordinary, IsStatic: false } method
                    || method.Arity != target.Arity
                    || method.Parameters.Length != target.Parameters.Length
                    || !compilation.IsSymbolAccessibleWithin(method, within))
                {
                    continue;
                }

                // Generic methods are compared after instantiating the candidate over the interface method's type parameters.
                IMethodSymbol comparable = method;
                if (method.Arity > 0)
                {
                    if (!ConstraintsCompatible(method.TypeParameters, target.TypeParameters, compilation))
                    {
                        continue;
                    }

                    comparable = method.Construct(target.TypeParameters.Cast<ITypeSymbol>().ToArray());
                }

                if (!ParametersCompatible(comparable.Parameters, target.Parameters, compilation))
                {
                    continue;
                }

                // A void interface method may forward to anything (the result is discarded).
                if (!target.ReturnsVoid && (comparable.ReturnsVoid || !IsAssignable(comparable.ReturnType, target.ReturnType, compilation)))
                {
                    continue;
                }

                return method;
            }

            return null;
        }

        private static bool ConstraintsCompatible(ImmutableArray<ITypeParameterSymbol> required, ImmutableArray<ITypeParameterSymbol> given, CSharpCompilation compilation)
        {
            for (int i = 0; i < required.Length; i++)
            {
                ITypeParameterSymbol r = required[i];
                ITypeParameterSymbol g = given[i];
                if ((r.HasReferenceTypeConstraint && !g.HasReferenceTypeConstraint)
                    || (r.HasValueTypeConstraint && !g.HasValueTypeConstraint)
                    || (r.HasUnmanagedTypeConstraint && !g.HasUnmanagedTypeConstraint)
                    || (r.HasConstructorConstraint && !g.HasConstructorConstraint && !g.HasValueTypeConstraint))
                {
                    return false;
                }

                foreach (ITypeSymbol constraint in r.ConstraintTypes)
                {
                    if (!g.ConstraintTypes.Any(c => IsAssignable(c, constraint, compilation)))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool ParametersCompatible(ImmutableArray<IParameterSymbol> source, ImmutableArray<IParameterSymbol> target, CSharpCompilation compilation)
        {
            for (int i = 0; i < target.Length; i++)
            {
                IParameterSymbol s = source[i];
                IParameterSymbol t = target[i];

                // 'in' parameters may be forwarded to by-value parameters; everything else must match exactly.
                bool refKindOk = s.RefKind == t.RefKind || (t.RefKind == RefKind.In && s.RefKind == RefKind.None);
                if (!refKindOk)
                {
                    return false;
                }

                bool typeOk = t.RefKind is RefKind.None or RefKind.In
                    ? IsAssignable(t.Type, s.Type, compilation)
                    : SymbolEqualityComparer.Default.Equals(s.Type, t.Type);
                if (!typeOk)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool HasMatchingProperty(ITypeSymbol source, IPropertySymbol target, CSharpCompilation compilation)
        {
            IAssemblySymbol within = compilation.Assembly;
            bool needsGet = target.GetMethod is not null;
            bool needsSet = target.SetMethod is not null;

            foreach (ISymbol candidate in LookupMembers(source, target.Name))
            {
                switch (candidate)
                {
                    case IPropertySymbol { IsStatic: false } property when property.IsIndexer == target.IsIndexer:
                        if (!compilation.IsSymbolAccessibleWithin(property, within))
                        {
                            continue;
                        }

                        if (target.IsIndexer && !ParametersCompatible(property.Parameters, target.Parameters, compilation))
                        {
                            continue;
                        }

                        if (needsGet && (property.GetMethod is null
                            || !compilation.IsSymbolAccessibleWithin(property.GetMethod, within)
                            || !IsAssignable(property.Type, target.Type, compilation)))
                        {
                            continue;
                        }

                        // The adapter assigns after the source object exists, so an init-only or ref readonly source does not do.
                        if (needsSet && (property.SetMethod is null
                            || property.SetMethod.IsInitOnly
                            || property.ReturnsByRefReadonly
                            || !compilation.IsSymbolAccessibleWithin(property.SetMethod, within)
                            || !IsAssignable(target.Type, property.Type, compilation)))
                        {
                            continue;
                        }

                        return true;

                    case IFieldSymbol { IsStatic: false, IsConst: false } field when !target.IsIndexer:
                        if (!compilation.IsSymbolAccessibleWithin(field, within))
                        {
                            continue;
                        }

                        if (needsGet && !IsAssignable(field.Type, target.Type, compilation))
                        {
                            continue;
                        }

                        if (needsSet && (field.IsReadOnly || !IsAssignable(target.Type, field.Type, compilation)))
                        {
                            continue;
                        }

                        return true;
                }
            }

            return false;
        }

        private static bool HasMatchingEvent(ITypeSymbol source, IEventSymbol target, CSharpCompilation compilation)
            => LookupMembers(source, target.Name).Any(candidate =>
                candidate is IEventSymbol { IsStatic: false } @event
                && compilation.IsSymbolAccessibleWithin(@event, compilation.Assembly)
                && SymbolEqualityComparer.Default.Equals(@event.Type, target.Type));

        /// <summary>Members named <paramref name="name"/> on the type, its base types and (for interfaces) base interfaces.</summary>
        private static IEnumerable<ISymbol> LookupMembers(ITypeSymbol type, string name)
        {
            for (ITypeSymbol? current = type; current is not null; current = current.BaseType)
            {
                foreach (ISymbol member in current.GetMembers(name))
                {
                    yield return member;
                }
            }

            if (type.TypeKind == TypeKind.Interface)
            {
                foreach (INamedTypeSymbol baseInterface in type.AllInterfaces)
                {
                    foreach (ISymbol member in baseInterface.GetMembers(name))
                    {
                        yield return member;
                    }
                }
            }
        }

        /// <summary>True when a value of type <paramref name="from"/> can be used where <paramref name="to"/> is expected.</summary>
        private static bool IsAssignable(ITypeSymbol from, ITypeSymbol to, CSharpCompilation compilation)
            => SymbolEqualityComparer.Default.Equals(from, to) || compilation.ClassifyConversion(from, to).IsImplicit;

        // ------------------------------------------------------------------
        // Symbol -> model
        // ------------------------------------------------------------------

        private static DuckMethod ToDuckMethod(IMethodSymbol method) => new(
            DeclaringInterface: DeclaringInterface(method),
            Name: method.Name,
            ReturnType: method.ReturnType.ToDisplayString(AnnotatedTypeFormat),
            ReturnsVoid: method.ReturnsVoid,
            TypeParameters: new EquatableList<string>(method.TypeParameters.Select(tp => tp.Name)),
            Parameters: ToDuckParameters(method.Parameters));

        private static DuckProperty ToDuckProperty(IPropertySymbol property) => new(
            DeclaringInterface: DeclaringInterface(property),
            Name: property.Name,
            Type: property.Type.ToDisplayString(AnnotatedTypeFormat),
            HasGetter: property.GetMethod is not null,
            HasSetter: property.SetMethod is not null,
            IsInitOnly: property.SetMethod?.IsInitOnly == true,
            IsIndexer: property.IsIndexer,
            Parameters: ToDuckParameters(property.Parameters));

        private static EquatableList<DuckParameter> ToDuckParameters(ImmutableArray<IParameterSymbol> parameters)
            => new(parameters.Select(p => new DuckParameter(p.Type.ToDisplayString(AnnotatedTypeFormat), p.Name, p.RefKind)));

        private static string BuildAdapterName(ITypeSymbol source, INamedTypeSymbol iface)
        {
            var builder = new StringBuilder("DuckAdapter_");
            builder.Append(Sanitize(StripGlobal(source.ToDisplayString(PlainTypeFormat)), allowDots: false));
            List<ITypeSymbol> typeArguments = AllTypeArguments(iface).ToList();
            if (typeArguments.Count > 0)
            {
                builder.Append("__");
                builder.Append(Sanitize(StripGlobal(string.Join("_", typeArguments.Select(t => t.ToDisplayString(PlainTypeFormat)))), allowDots: false));
            }

            return builder.ToString();
        }

        private static string StripGlobal(string text) => text.Replace("global::", string.Empty);

        private static string Sanitize(string text, bool allowDots)
        {
            var builder = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                builder.Append(char.IsLetterOrDigit(c) || c == '_' || (allowDots && c == '.') ? c : '_');
            }

            return builder.ToString();
        }

        // ------------------------------------------------------------------
        // Stage 3: join interfaces with their adapters
        // ------------------------------------------------------------------

        /// <summary>
        /// Groups adapters under their interface. The result is one equatable unit per interface, so the
        /// output stage reruns only for interfaces whose own adapters actually changed.
        /// </summary>
        public static ImmutableArray<GenerationUnit> Join(ImmutableArray<InterfaceModel> interfaces, ImmutableArray<AdapterModel> adapters)
        {
            var byInterface = new Dictionary<string, List<AdapterModel>>(StringComparer.Ordinal);
            foreach (AdapterModel adapter in adapters)
            {
                if (!byInterface.TryGetValue(adapter.InterfaceKey, out List<AdapterModel>? list))
                {
                    byInterface[adapter.InterfaceKey] = list = new List<AdapterModel>();
                }

                list.Add(adapter);
            }

            var units = ImmutableArray.CreateBuilder<GenerationUnit>(interfaces.Length);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (InterfaceModel iface in interfaces.OrderBy(i => i.Key, StringComparer.Ordinal))
            {
                if (!seen.Add(iface.Key))
                {
                    continue; // the attribute sits on more than one partial part
                }

                var unitAdapters = new EquatableList<AdapterModel>();
                if (byInterface.TryGetValue(iface.Key, out List<AdapterModel>? list))
                {
                    unitAdapters.AddRange(list
                        .Distinct()
                        .OrderBy(a => a.SourceType, StringComparer.Ordinal)
                        .ThenBy(a => a.InterfaceType, StringComparer.Ordinal));
                    DisambiguateNames(unitAdapters);
                }

                units.Add(new GenerationUnit(iface, unitAdapters));
            }

            return units.ToImmutable();
        }

        private static void DisambiguateNames(EquatableList<AdapterModel> adapters)
        {
            var used = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < adapters.Count; i++)
            {
                string baseName = adapters[i].AdapterName;
                string name = baseName;
                int suffix = 1;
                while (!used.Add(name))
                {
                    name = baseName + "_" + (++suffix);
                }

                if (name != baseName)
                {
                    adapters[i] = adapters[i] with { AdapterName = name };
                }
            }
        }
    }
}
