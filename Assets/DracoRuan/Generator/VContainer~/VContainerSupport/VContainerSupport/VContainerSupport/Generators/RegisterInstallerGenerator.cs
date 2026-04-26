using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using VContainerSupport.Models;

namespace VContainerSupport.Generators;

[Generator]
public class RegisterInstallerGenerator : IIncrementalGenerator
{
    private const string AttributeShortName = "RegisterInstallerAttribute";
    private const string AttributeFullName = "RegisterInstallerAttributeAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {

    }

    private static InstallerRegistrationModel GetRegistrationData(GeneratorSyntaxContext context)
    {
        ClassDeclarationSyntax classDeclarationSyntax = (ClassDeclarationSyntax)context.Node;
        INamedTypeSymbol symbol = context.SemanticModel.GetDeclaredSymbol(classDeclarationSyntax) as INamedTypeSymbol;
        AttributeData attributeData = symbol?.GetAttributes().FirstOrDefault(attribute =>
            attribute.AttributeClass?.Name is AttributeFullName or AttributeShortName);

        if (attributeData == null)
            return null;

        string installerName = null;
        string lifetimeScopeName = "ProjectLifetimeScope";

        foreach (var kvp in attributeData.NamedArguments)
        {
            switch (kvp.Key)
            {
                case "InstallerName":
                    installerName = kvp.Value.Value?.ToString();
                    break;
                case "LifetimeScopeName":
                    lifetimeScopeName = kvp.Value.Value?.ToString();
                    break;
            }
        }

        return new InstallerRegistrationModel(installerName, lifetimeScopeName);
    }
}