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

        foreach (var namedArg in attributeData.NamedArguments)
        {
            bool isTargetValueNull;
            switch (namedArg.Key)
            {
                case "LifetimeScope":
                    lifetimeScope = namedArg.Value.Value?.ToString();
                    break;
                case "LifetimeScopeName":
                    lifetimeScopeName = namedArg.Value.Value?.ToString();
                    break;
                case "InstallerName":
                    installerName = namedArg.Value.Value?.ToString();
                    break;
                case "AsImplementedInterfaces":
                    isTargetValueNull = namedArg.Value.Value == null;
                    asImplementInterfaces = !isTargetValueNull && (bool)namedArg.Value.Value;
                    break;
                case "IsEntryPoint":
                    isTargetValueNull = namedArg.Value.Value == null;
                    isEntryPoint = !isTargetValueNull && (bool)namedArg.Value.Value;
                    break;
                case "AsSelf":
                    isTargetValueNull = namedArg.Value.Value == null;
                    asSelf = !isTargetValueNull && (bool)namedArg.Value.Value;
                    break;
                case "WithKey":
                    withKey = namedArg.Value.Value?.ToString();
                    break;
            }
        }

        return new ServiceRegistrationModel(serviceName, lifetimeScope, lifetimeScopeName, installerName,
            asImplementInterfaces, isEntryPoint, asSelf, withKey);
    }
}