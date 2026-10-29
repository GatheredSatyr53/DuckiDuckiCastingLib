using Microsoft.CodeAnalysis;
using System;

#pragma warning disable RS2008 // analyzer release tracking is not used by this project

namespace DuckiDuckiCastingLib
{
    internal static class DuckDiagnostics
    {
        private const string Category = "DuckiDuckiCastingLib";

        public static readonly DiagnosticDescriptor InterfaceMustBePartial = new(
            id: "DUCK001",
            title: "Duck interface must be partial",
            messageFormat: "Interface '{0}' is marked with [SupportsDuckCast] and must be declared 'partial' so that the Cast method can be generated into it",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor MemberMismatch = new(
            id: "DUCK002",
            title: "Type cannot be duck-cast",
            messageFormat: "Type '{0}' cannot be duck-cast to '{1}': no accessible instance member matches {2}",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor SourceTypeNotConcrete = new(
            id: "DUCK003",
            title: "Cannot determine the source type of a duck cast",
            messageFormat: "Cannot generate a duck-cast adapter for this call: {0}",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor ContainingTypeMustBePartial = new(
            id: "DUCK004",
            title: "Containing type must be partial",
            messageFormat: "Type '{0}' contains the duck interface '{1}' and must be declared 'partial'",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor UnsupportedMember = new(
            id: "DUCK005",
            title: "Unsupported duck interface member",
            messageFormat: "Member '{0}' of duck interface '{1}' is not supported: {2}",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor CrossAssemblyCast = new(
            id: "DUCK006",
            title: "Duck cast across assemblies",
            messageFormat: "'{0}' is declared in another assembly. Adapters are generated only in the assembly that declares the interface, so this call fails at run time unless that assembly already contains an adapter for '{1}'.",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor InterfaceNotAccessible = new(
            id: "DUCK007",
            title: "Duck interface is not accessible from generated code",
            messageFormat: "Interface '{0}' must be accessible within its assembly (public or internal) because the generated adapters live in the DuckiDuckiCastingLib.Generated namespace",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static DiagnosticDescriptor Get(string id) => id switch
        {
            "DUCK001" => InterfaceMustBePartial,
            "DUCK002" => MemberMismatch,
            "DUCK003" => SourceTypeNotConcrete,
            "DUCK004" => ContainingTypeMustBePartial,
            "DUCK005" => UnsupportedMember,
            "DUCK006" => CrossAssemblyCast,
            "DUCK007" => InterfaceNotAccessible,
            _ => throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown diagnostic id"),
        };
    }
}
