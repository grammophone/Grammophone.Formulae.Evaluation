﻿using System;
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

			var diagnostics = ConvertDiagnostics(script.Compile());

			var errorDiagnostics = from diagnostic in diagnostics
														 where diagnostic.Severity == FormulaDiagnosticSeverity.Error
														 select diagnostic;

			if (errorDiagnostics.Any())
				throw new FormulaCompilationErrorException(FormulaEvaluatorResources.COMPILATION_FAILED, diagnostics);

			var scriptState = await script.RunAsync(globals: context);

			var variables = from sv in scriptState.Variables
											let definition = TryGetFormulaDefinition(sv.Name)
											select new EvaluationVariable(
												sv.Name, sv.Type, sv.IsReadOnly, sv.Value, definition?.Expression,
												this.RoundingOptions != null && !(definition?.IgnoreRoundingOptions ?? false) && definition?.DataType == typeof(decimal));

			return new EvaluationState(identifier, variables.ToImmutableArray(), diagnostics);
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

			var preParseScript = CSharpScript.Create(formulaDefinition.Expression, options: this.ScriptOptions, globalsType: typeof(C));

			var containedIdentifiers = from syntaxTree in preParseScript.GetCompilation().SyntaxTrees
																 from identifierNode in syntaxTree.GetRoot().DescendantNodes().OfType<IdentifierNameSyntax>()
																 let parentNode = identifierNode.Parent
																 where parentNode is not AssignmentExpressionSyntax && parentNode is not MemberAccessExpressionSyntax
																 select identifierNode.Identifier.Text;

			Script fullScript = CSharpScript.Create(String.Empty, options: this.ScriptOptions, globalsType: typeof(C));

			foreach (string containedIdentifier in containedIdentifiers)
			{
				if (!formulaDefinitionsByidentifiers.ContainsKey(containedIdentifier)) continue;

				bool variableIsDeclared = fullScript
					.GetCompilation().SyntaxTrees.Any(st =>
						st.GetRoot().DescendantNodes().OfType<VariableDeclarationSyntax>().Any(d => d
							.Variables.Any(v => v.Identifier.Text == containedIdentifier)));

				if (variableIsDeclared) continue;

				var containedScript = GetScript(containedIdentifier);

				fullScript = ContinueWithScript(fullScript, containedScript);
			}

			string fullExpression;

			if (formulaDefinition.DataType == typeof(decimal) && this.RoundingOptions != null && !formulaDefinition.IgnoreRoundingOptions)
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

			return fullScript;
		}

		private Script ContinueWithScript(Script targetScript, Script sourceScript)
		{
			if (sourceScript.Previous != null) targetScript = ContinueWithScript(targetScript, sourceScript.Previous);

			targetScript = targetScript.ContinueWith(sourceScript.Code, this.ScriptOptions);

			return targetScript;
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

		#endregion
	}
}
