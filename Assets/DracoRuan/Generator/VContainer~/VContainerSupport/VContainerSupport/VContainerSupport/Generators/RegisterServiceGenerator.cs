using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using VContainerSupport.Models;

namespace VContainerSupport.Generators;

[Generator]
public class RegisterServiceGenerator : IIncrementalGenerator
{
    private const string AttributeShortName = "RegisterService";
    private const string AttributeFullName = "RegisterServiceAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {

    }

    private static ServiceRegistrationModel GetRegistrationData(GeneratorSyntaxContext context)
    {
        ClassDeclarationSyntax classDeclarationSyntax = (ClassDeclarationSyntax)context.Node;
        INamedTypeSymbol symbol = context.SemanticModel.GetDeclaredSymbol(classDeclarationSyntax) as INamedTypeSymbol;
        AttributeData attributeData = symbol?.GetAttributes().FirstOrDefault(attribute =>
            attribute.AttributeClass?.Name is AttributeFullName or AttributeShortName);

        if (attributeData == null)
            return null;

        string serviceName = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        string lifetimeScope = "Scope";
        string lifetimeScopeName = "ProjectLifetimeScope";
        string installerName = null;
        bool asImplementInterfaces = false;
        bool isEntryPoint = false;
        bool asSelf = false;
        string withKey = null;

        foreach (var kvp in attributeData.NamedArguments)
        {
            bool isTargetValueNull;
            switch (kvp.Key)
            {
                case "LifetimeScope":
                    lifetimeScope = kvp.Value.Value?.ToString();
                    break;
                case "LifetimeScopeName":
                    lifetimeScopeName = kvp.Value.Value?.ToString();
                    break;
                case "InstallerName":
                    installerName = kvp.Value.Value?.ToString();
                    break;
                case "AsImplementedInterfaces":
                    isTargetValueNull = kvp.Value.Value == null;
                    asImplementInterfaces = !isTargetValueNull && (bool)kvp.Value.Value;
                    break;
                case "IsEntryPoint":
                    isTargetValueNull = kvp.Value.Value == null;
                    isEntryPoint = !isTargetValueNull && (bool)kvp.Value.Value;
                    break;
                case "AsSelf":
                    isTargetValueNull = kvp.Value.Value == null;
                    asSelf = !isTargetValueNull && (bool)kvp.Value.Value;
                    break;
                case "WithKey":
                    withKey = kvp.Value.Value?.ToString();
                    break;
            }
        }

        return new ServiceRegistrationModel(serviceName, lifetimeScope, lifetimeScopeName, installerName,
            asImplementInterfaces, isEntryPoint, asSelf, withKey);
    }
}