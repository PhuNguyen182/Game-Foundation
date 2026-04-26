using System.Linq;
using System.Text;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using VContainerSupport.Models;

namespace VContainerSupport.Generators;

[Generator]
public class RegisterInstallerGenerator : IIncrementalGenerator
{
    private const string AttributeShortName = "RegisterInstallerAttribute";
    private const string AttributeFullName = "RegisterInstallerAttributeAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValuesProvider<InstallerRegistrationModel> classDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: (syntaxNode, _) => syntaxNode is ClassDeclarationSyntax { AttributeLists.Count: > 0 },
                transform: (syntaxContext, _) => GetRegistrationData(syntaxContext))
            .Where(registrationModel => registrationModel is not null);
        
        IncrementalValueProvider<ImmutableArray<InstallerRegistrationModel>> registrationData = classDeclarations.Collect();
        context.RegisterSourceOutput(registrationData, (spc, source) => Execute(source, spc));
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
    
    private static void Execute(ImmutableArray<InstallerRegistrationModel> models, SourceProductionContext context)
    {
        if (models.IsDefaultOrEmpty)
            return;
        
        if (models.IsDefaultOrEmpty) 
            return;

        foreach (InstallerRegistrationModel grouping in models)
        {
            StringBuilder stringBuilder = new();
            // TODO: Use Addressable to load all Installer in project and initialize with builder

            context.AddSource($"VContainer_{grouping.InstallerName}Register.g.cs",
                SourceText.From(stringBuilder.ToString(), Encoding.UTF8));
        }
    }
}