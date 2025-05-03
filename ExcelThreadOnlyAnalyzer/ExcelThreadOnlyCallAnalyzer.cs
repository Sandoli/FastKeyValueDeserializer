// Roslyn Analyzer that reports calls to methods marked [ExcelThreadOnly]
// when they are invoked from unmarked methods or from within Task.Run

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Linq;

namespace ExcelThreadOnlyAnalyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ExcelThreadOnlyCallAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "EXCEL001";

        private static readonly LocalizableString Title = "Call to a methode ExcelThreadOnly from a non ExcelThreadOnly context";
        private static readonly LocalizableString MessageFormat = "Method '{0}' is tagged [ExcelThreadOnly] but is invoked from '{1}' which is not tagged";
        private const string Category = "Threading";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId, Title, MessageFormat, Category,
            DiagnosticSeverity.Warning, isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
        }

        private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;
            var semanticModel = context.SemanticModel;

            var symbol = semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
            if (symbol == null)
                return;

            var hasExcelThreadOnly = symbol.GetAttributes()
                .Any(attr => attr.AttributeClass?.Name == "ExcelThreadOnlyAttribute");

            if (!hasExcelThreadOnly)
                return;

            var inTaskRun = invocation.Ancestors().OfType<AnonymousFunctionExpressionSyntax>()
                .Any(lambda => lambda.Ancestors().OfType<InvocationExpressionSyntax>()
                    .Any(inv => semanticModel.GetSymbolInfo(inv).Symbol is IMethodSymbol method && method.Name == "Run" && method.ContainingType?.Name == "Task"));

            var enclosingMethod = invocation.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
            var enclosingSymbol = enclosingMethod != null ? semanticModel.GetDeclaredSymbol(enclosingMethod) : null;
            var callerIsMarked = enclosingSymbol != null && enclosingSymbol.GetAttributes()
                .Any(attr => attr.AttributeClass?.Name == "ExcelThreadOnlyAttribute");

            if (!callerIsMarked || inTaskRun)
            {
                var name = enclosingSymbol?.Name ?? "lambda dans Task.Run";
                var diagnostic = Diagnostic.Create(
                    Rule,
                    invocation.GetLocation(),
                    symbol.Name,
                    name);

                context.ReportDiagnostic(diagnostic);
            }
        }
    }
    
    [System.AttributeUsage(System.AttributeTargets.Method)]
    public class ExcelThreadOnlyAttribute : System.Attribute {}
}
