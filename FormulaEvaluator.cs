using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Scripting;
using System.Collections.Immutable;

namespace Grammophone.Formulae.Evaluation
{
	/// <summary>
	/// Evaluator of formulae that may reference a context class of type <typeparamref name="C"/>.
	/// </summary>
	/// <typeparam name="C">The type of the context class.</typeparam>
	public class FormulaEvaluator<C>
		where C : class
	{
		#region Private fields

		private readonly ConcurrentDictionary<string, Script> formulaScriptsByIdentifiers;

		private readonly IReadOnlyDictionary<string, IFormulaDefinition> formulaDefinitionsByidentifiers;

		private readonly ScriptOptions scriptOptions;
		
		private readonly IEnumerable<string> excludedNames;

		#endregion

		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="formulaDefinitions">The definitions of the formulae to evaluate.</param>
		/// <param name="assemblies">Optional additional assemblies to reference for the compilation of the formulae.</param>
		/// <param name="excludedNames">Optional namespaces or member names to be blocked from usage.</param>
		internal FormulaEvaluator(
			IEnumerable<IFormulaDefinition> formulaDefinitions,
			IEnumerable<Assembly>? assemblies = null,
			IEnumerable<string>? excludedNames = null)
		{
			formulaDefinitionsByidentifiers = formulaDefinitions.ToDictionary(d => d.Identifier);

			formulaScriptsByIdentifiers = new ConcurrentDictionary<string, Script>();

			scriptOptions = ScriptOptions.Default
				.AddImports("System", "System.Math")
				.WithReferences(typeof(Object).Assembly, typeof(Math).Assembly, typeof(IEnumerable<C>).Assembly)
				.WithAllowUnsafe(false)
				.WithCheckOverflow(true);

			if (assemblies != null) scriptOptions = scriptOptions.AddReferences(assemblies);

			this.excludedNames = new HashSet<string>(excludedNames) ?? Enumerable.Empty<string>();
		}

		#endregion

		#region Public methods

		/// <summary>
		/// Compile and run the furmulae on a given <paramref name="context"/> to evaluate <paramref name="identifier"/> and return the <see cref="EvaluationState"/>.
		/// </summary>
		/// <param name="context">The context to pass to the formulae.</param>
		/// <param name="identifier">The identifier to evaluate.</param>
		/// <returns>Returns the evaluation state.</returns>
		/// <exception cref="ArgumentNullException">Thrown when arguments are null.</exception>
		/// <exception cref="FormulaCompilationErrorException">Thrown when there is a compilation error.</exception>
		public async Task<EvaluationState> RunAsync(C context, string identifier)
		{
			if (context == null) throw new ArgumentNullException(nameof(context));
			if (identifier == null) throw new ArgumentNullException(nameof(identifier));

			var script = GetScript(identifier).ContinueWith(identifier, scriptOptions);

			var diagnostics = ConvertDiagnostics(script.Compile());

			var errorDiagnostics = from diagnostic in diagnostics
														 where diagnostic.Severity == FormulaDiagnosticSeverity.Error
														 select diagnostic;

			if (errorDiagnostics.Any())
				throw new FormulaCompilationErrorException(FormulaEvaluatorResources.COMPILATION_FAILED, diagnostics);

			var scriptState = await script.RunAsync(globals: context);

			return new EvaluationState(scriptState);
		}

		/// <summary>
		/// Compile and run the furmulae on a given <paramref name="context"/> to evaluate <paramref name="identifier"/> and return its value.
		/// </summary>
		/// <param name="context">The context to pass to the formulae.</param>
		/// <param name="identifier">The identifier to evaluate.</param>
		/// <returns>Returns the computed value.</returns>
		/// <exception cref="ArgumentNullException">Thrown when arguments are null.</exception>
		public async Task<T?> EvaluateAsync<T>(C context, string identifier)
		{
			var evaluationState = await RunAsync(context, identifier);

			return (T?)evaluationState.ReturnValue;
		}

		#endregion

		#region Protected methods

		/// <summary>
		/// Called when a script fragment is created. Implementations may process it and return a transformed sscript.
		/// </summary>
		/// <remarks>The default implementation just returns <paramref name="script"/>.</remarks>
		protected virtual Script OnScriptCreated(Script script) => script;

		#endregion

		private FormulaDiagnosticSeverity ConvertFormulaDiagnosticSeverity(DiagnosticSeverity severity)
			=> severity switch
			{
				DiagnosticSeverity.Hidden => FormulaDiagnosticSeverity.Hidden,
				DiagnosticSeverity.Info => FormulaDiagnosticSeverity.Info,
				DiagnosticSeverity.Warning => FormulaDiagnosticSeverity.Warning,
				DiagnosticSeverity.Error => FormulaDiagnosticSeverity.Error,
				_ => throw new FormulaEvaluationException(String.Format(FormulaEvaluatorResources.UNKNOWN_SEVERITY_TYPE, severity))
			};

		private ImmutableArray<FormulaDiagnostic> ConvertDiagnostics(ImmutableArray<Diagnostic> diagnostics)
		{
			return diagnostics.Select(d => new FormulaDiagnostic(ConvertFormulaDiagnosticSeverity(d.Severity), d.ToString())).ToImmutableArray();
		}

		#region Private methods

		private Script GetScript(string identifier)
		{
			if (identifier == null) throw new ArgumentNullException(nameof(identifier));

			return formulaScriptsByIdentifiers.GetOrAdd(identifier, CreateScript);
		}

		private Script CreateScript(string identifier)
		{
			if (!formulaDefinitionsByidentifiers.TryGetValue(identifier, out var formulaDefinition))
			{
				throw new FormulaEvaluationException(String.Format(FormulaEvaluatorResources.NO_FORMULA_FOR_IDENTIFIER, identifier));
			}

			var preParseScript = CSharpScript.Create(formulaDefinition.Expression, options: scriptOptions, globalsType: typeof(C));

			var containedIdentifiers = from syntaxTree in preParseScript.GetCompilation().SyntaxTrees
																 from identifierNode in syntaxTree.GetRoot().DescendantNodes().OfType<IdentifierNameSyntax>()
																 let parentNode = identifierNode.Parent
																 where parentNode is not AssignmentExpressionSyntax && parentNode is not MemberAccessExpressionSyntax
																 select identifierNode.Identifier.Text;

			Script fullScript = CSharpScript.Create(String.Empty, options: scriptOptions, globalsType: typeof(C));

			foreach (string containedIdentifier in containedIdentifiers)
			{
				if (!formulaDefinitionsByidentifiers.ContainsKey(containedIdentifier)) continue;

				var containedScript = GetScript(containedIdentifier);

				fullScript = ContinueWithScript(fullScript, containedScript);
			}

			fullScript = fullScript.ContinueWith($"{formulaDefinition.DataType} {formulaDefinition.Identifier} = {formulaDefinition.Expression};", scriptOptions);

			fullScript = OnScriptCreated(fullScript);

			EnsureNamespaceUsage(fullScript);

			return fullScript;
		}

		private Script ContinueWithScript(Script targetScript, Script sourceScript)
		{
			if (sourceScript.Previous != null) targetScript = ContinueWithScript(targetScript, sourceScript.Previous);

			targetScript = targetScript.ContinueWith(sourceScript.Code, scriptOptions);

			return targetScript;
		}

		private void EnsureNamespaceUsage(Script script)
		{
			var syntaxTree = script.GetCompilation().SyntaxTrees.FirstOrDefault();

			if (syntaxTree == null) return;

			var memberAccessExpressions = from node in syntaxTree.GetRoot().DescendantNodesAndSelf().OfType<MemberAccessExpressionSyntax>()
																		select node.Expression;

			foreach (var memberAccessExpression in memberAccessExpressions)
			{
				string memberName = memberAccessExpression.ToString();

				if (excludedNames.Contains(memberName))
				{
					throw new FormulaNameAccessException(String.Format(FormulaEvaluatorResources.NAME_ACCESS_DENIED, memberName), memberName);
				}
			}
		}

		#endregion
	}
}
