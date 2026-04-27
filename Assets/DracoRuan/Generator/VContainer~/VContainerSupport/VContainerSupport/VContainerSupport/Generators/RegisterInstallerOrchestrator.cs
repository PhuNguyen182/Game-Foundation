using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using VContainerSupport.Models;

namespace VContainerSupport.Generators;

[Generator]
public class RegisterInstallerOrchestrator : IIncrementalGenerator
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

        string installerKey = null;
        string lifetimeScopeName = "ProjectLifetimeScope";
        string installerInstanceType = "PlainCSharp";
        string installerDataType = "";

        foreach (var kvp in attributeData.NamedArguments)
        {
            switch (kvp.Key)
            {
                case "InstallerKey":
                    installerKey = kvp.Value.Value?.ToString();
                    break;
                case "LifetimeScopeName":
                    lifetimeScopeName = kvp.Value.Value?.ToString();
                    break;
                case "InstallerInstanceType":
                    installerInstanceType = kvp.Value.Value?.ToString();
                    break;
                case "InstallerDataType":
                    installerDataType = kvp.Value.Value?.ToString();
                    break;
            }
        }

        return new InstallerRegistrationModel(installerKey, installerDataType, lifetimeScopeName, installerInstanceType);
    }
    
    private static void Execute(ImmutableArray<InstallerRegistrationModel> models, SourceProductionContext context)
    {
        if (models.IsDefaultOrEmpty)
            return;

        foreach (IGrouping<string, InstallerRegistrationModel> grouping in models.GroupBy(registrationModel =>
                     registrationModel.LifetimeScopeName))
        {
            foreach (InstallerRegistrationModel model in models)
            {
                StringBuilder stringBuilder = new();

                context.AddSource($"VContainer_{model.InstallerKey}Register.g.cs",
                    SourceText.From(stringBuilder.ToString(), Encoding.UTF8));
            }
        }
    }
}