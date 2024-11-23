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
	public class FormulaEvaluator<C> : FormulaParser<C>
		where C : class
	{
		#region Private fields

		private readonly ConcurrentDictionary<string, Script> formulaScriptsByIdentifiers;

		private readonly IReadOnlyDictionary<string, IFormulaDefinition> formulaDefinitionsByidentifiers;

		#endregion

		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="formulaDefinitions">The definitions of the formulae to evaluate.</param>
		/// <param name="assemblies">Optional additional assemblies to reference for the compilation of the formulae.</param>
		/// <param name="imports">Optional namespace imports.</param>
		/// <param name="excludedNames">Optional namespaces or member names to be blocked from usage.</param>
		/// <param name="roundingOptions">If not null, the rounding options to use for decimal variables, otherwise the decimal variables are assigned with no rounding.</param>
		protected internal FormulaEvaluator(
			IEnumerable<IFormulaDefinition> formulaDefinitions,
			IEnumerable<Assembly>? assemblies,
			IEnumerable<string>? imports,
			IEnumerable<string>? excludedNames,
			RoundingOptions? roundingOptions)
			: base(assemblies, imports, excludedNames)
		{
			formulaDefinitionsByidentifiers = formulaDefinitions.ToDictionary(d => d.Identifier);

			formulaScriptsByIdentifiers = new ConcurrentDictionary<string, Script>();
			
			this.RoundingOptions = roundingOptions;
		}

		#endregion

		#region Protected properties

		/// <summary>
		/// If not null, the rounding options to use for decimal variables, otherwise the decimal variables are assigned with no rounding.
		/// </summary>
		protected RoundingOptions? RoundingOptions { get; }

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

			var script = GetScript(identifier);

			var scriptState = await script.RunAsync(globals: context);

			var variables = from sv in scriptState.Variables
											let definition = TryGetFormulaDefinition(sv.Name)
											select new EvaluationVariable(
												sv.Name, sv.Type, sv.IsReadOnly, sv.Value, definition?.Expression,
												this.RoundingOptions != null && !(definition?.IgnoreRoundingOptions ?? false) && definition?.DataType == typeof(decimal));

			return new EvaluationState(identifier, variables.ToImmutableArray(), ConvertDiagnostics(script.GetCompilation().GetDiagnostics()));
		}

		/// <summary>
		/// Get the identifiers that are required for the evaluation of an identifier.
		/// </summary>
		/// <param name="identifier">The identifier to compute.</param>
		/// <returns>Returns the identifiers required for the evaluation of <paramref name="identifier"/>.</returns>
		/// <exception cref="ArgumentNullException">Thrown when <paramref name="identifier"/> is null.</exception>
		/// <exception cref="FormulaCompilationErrorException">Thrown when there is a compilation error.</exception>
		public IReadOnlyList<Identifier> GetContainedIdentifiers(string identifier)
		{
			if (identifier == null) throw new ArgumentNullException(nameof(identifier));

			var script = GetScript(identifier);

			var identifierNames = GetTotalContainedIdentifierNames(script);

			var identifiers = from identifierName in identifierNames
												orderby identifierName ascending
												let formulaDefinition = formulaDefinitionsByidentifiers.ContainsKey(identifierName) ? formulaDefinitionsByidentifiers[identifierName] : null
												select new Identifier(identifierName, formulaDefinition);

			return identifiers.ToArray();
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

		/// <summary>
		/// Continue a script with the code of another script.
		/// </summary>
		/// <param name="targetScript">The target script to append.</param>
		/// <param name="sourceScript">The source script to continue with.</param>
		protected Script ContinueWithScript(Script targetScript, Script sourceScript)
		{
			if (sourceScript.Previous != null) targetScript = ContinueWithScript(targetScript, sourceScript.Previous);

			if (sourceScript.Code != String.Empty)
			{
				targetScript = targetScript.ContinueWith(sourceScript.Code, this.ScriptOptions);
			}

			return targetScript;
		}

		/// <summary>
		/// Continue a script with the code of another script.
		/// </summary>
		/// <param name="targetScript">The target script to append.</param>
		/// <param name="sourceScript">The source script to continue with.</param>
		/// <param name="excludedIdentifiers">Skip adding scripts that declare identifiers in this set.</param>
		protected Script ContinueWithScript(Script targetScript, Script sourceScript, ISet<string> excludedIdentifiers)
		{
			if (sourceScript.Previous != null) targetScript = ContinueWithScript(targetScript, sourceScript.Previous, excludedIdentifiers);

			if (sourceScript.Code != String.Empty)
			{
				var declaredIdentifiers = sourceScript
					.GetCompilation().SyntaxTrees.SelectMany(st =>
						st.GetRoot().DescendantNodes().OfType<VariableDeclarationSyntax>().SelectMany(d => d.Variables.Select(v => v.Identifier.Text)));

				bool variableIsDeclared = declaredIdentifiers.Any(declaredIdentifier => excludedIdentifiers.Contains(declaredIdentifier));

				if (!variableIsDeclared)
				{
					targetScript = targetScript.ContinueWith(sourceScript.Code, this.ScriptOptions);

					excludedIdentifiers.UnionWith(declaredIdentifiers);
				}
			}

			return targetScript;
		}

		#endregion

		#region Private methods

		private Script GetScript(string identifier)
		{
			if (identifier == null) throw new ArgumentNullException(nameof(identifier));

			return formulaScriptsByIdentifiers.GetOrAdd(identifier, CreateScript);
		}

		private Script CreateScript(string identifier)
		{
			var script = CreateScript(identifier, new HashSet<string>());

			return script;
		}

		private Script CreateScript(string identifier, ISet<string> resolvedIdentifiers)
		{
			if (!formulaDefinitionsByidentifiers.TryGetValue(identifier, out var formulaDefinition))
			{
				throw new FormulaEvaluationException(String.Format(FormulaEvaluatorResources.NO_FORMULA_FOR_IDENTIFIER, identifier));
			}

			var preParseScript = CSharpScript.Create(formulaDefinition.Expression, options: this.ScriptOptions, globalsType: typeof(C));

			IEnumerable<string> containedIdentifiers = GetContainedIdentifierNames(preParseScript);

			Script fullScript = CSharpScript.Create(String.Empty, options: this.ScriptOptions, globalsType: typeof(C));

			foreach (string containedIdentifier in containedIdentifiers)
			{
				if (!formulaDefinitionsByidentifiers.ContainsKey(containedIdentifier)) continue;

				if (resolvedIdentifiers.Contains(containedIdentifier)) continue;

				//bool variableIsDeclared = fullScript
				//	.GetCompilation().SyntaxTrees.Any(st =>
				//		st.GetRoot().DescendantNodes().OfType<VariableDeclarationSyntax>().Any(d => d
				//			.Variables.Any(v => v.Identifier.Text == containedIdentifier)));

				//if (variableIsDeclared) continue;

				var containedScript = GetScript(containedIdentifier);

				fullScript = ContinueWithScript(fullScript, containedScript, resolvedIdentifiers);
			}

			string fullExpression;

			if (AreTypesAssignable(typeof(decimal), formulaDefinition.DataType) && this.RoundingOptions != null && !formulaDefinition.IgnoreRoundingOptions)
			{
				fullExpression = $"Round({formulaDefinition.Expression}, {this.RoundingOptions.RoundedDecimalsCount}, MidpointRounding.{this.RoundingOptions.MidpointRounding})";
			}
			else
			{
				fullExpression = formulaDefinition.Expression;
			}

			fullScript = fullScript.ContinueWith($"{formulaDefinition.DataType} {formulaDefinition.Identifier} = {fullExpression};", this.ScriptOptions);

			fullScript = OnScriptCreated(fullScript);

			EnsureNamespaceUsage(fullScript);

			Compile(fullScript);

			resolvedIdentifiers.Add(identifier);

			return fullScript;
		}

		private static IEnumerable<string> GetContainedIdentifierNames(Script script)
		{
			var identifiers = from syntaxTree in script.GetCompilation().SyntaxTrees
												from identifierNode in syntaxTree.GetRoot().DescendantNodes().OfType<IdentifierNameSyntax>()
												let parentNode = identifierNode.Parent
												where parentNode is not AssignmentExpressionSyntax && parentNode is not MemberAccessExpressionSyntax
												select identifierNode.Identifier.Text;

			return identifiers.Distinct();
		}

		private static IEnumerable<string> GetTotalContainedIdentifierNames(Script script)
		{
			var currentIdentifiers = GetContainedIdentifierNames(script);

			if (script.Previous != null)
			{
				return currentIdentifiers.Union(GetTotalContainedIdentifierNames(script.Previous));
			}
			else
			{
				return currentIdentifiers;
			}
		}

		private IFormulaDefinition? TryGetFormulaDefinition(string identifier)
		{
			if (formulaDefinitionsByidentifiers.TryGetValue(identifier, out var formulaDefinition))
			{
				return formulaDefinition;
			}
			else
			{
				return null;
			}
		}

		private static bool AreTypesAssignable(Type sourceType, Type destinationType)
		{
			if (destinationType.IsAssignableFrom(sourceType)) return true;

			var hasImplicitOperators = from method in destinationType.GetMethods(BindingFlags.Public | BindingFlags.Static)
																 where method.Name == "op_Implicit" && method.GetParameters().Any(parameter => parameter.ParameterType.IsAssignableFrom(sourceType))
																 select method;

			return hasImplicitOperators.Any();
		}

		private ImmutableArray<FormulaDiagnostic> Compile(Script script)
		{
			var diagnostics = ConvertDiagnostics(script.Compile());

			EnsureNoErrorDiagnostics(diagnostics);

			return diagnostics;
		}

		#endregion
	}
}
