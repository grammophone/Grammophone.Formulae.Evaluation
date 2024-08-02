using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace Grammophone.Formulae.Evaluation
{
	/// <summary>
	/// Parser of formulae that may reference a context class of type <typeparamref name="C"/>.
	/// </summary>
	/// <typeparam name="C">The type of the context class.</typeparam>
	public class FormulaParser<C>
		where C : class
	{
		#region Construction

		internal FormulaParser(
			IEnumerable<Assembly>? assemblies = null,
			IEnumerable<string>? imports = null,
			IEnumerable<string>? excludedNames = null)
		{
			this.ScriptOptions = ScriptOptions.Default
				.WithReferences(typeof(Object).Assembly, typeof(Math).Assembly, typeof(IEnumerable<C>).Assembly)
				.WithAllowUnsafe(false)
				.WithCheckOverflow(true);

			if (assemblies != null) this.ScriptOptions = this.ScriptOptions.AddReferences(assemblies);
			if (imports != null) this.ScriptOptions = this.ScriptOptions.AddImports(imports);

			this.ExcludedNames = new HashSet<string>(excludedNames) ?? Enumerable.Empty<string>();
		}

		#endregion

		#region Protected properties

		/// <summary>
		/// The options for the parser.
		/// </summary>
		protected ScriptOptions ScriptOptions { get; }

		/// <summary>
		/// Any names to be exclused from parsing.
		/// </summary>
		protected IEnumerable<string> ExcludedNames { get; }

		#endregion

		#region Public methods

		/// <summary>
		/// Diagnose an expression.
		/// </summary>
		/// <param name="expression">The expression to parse.</param>
		/// <returns>Returns the list of diagnostics.</returns>
		public IReadOnlyList<FormulaDiagnostic> Validate(string expression)
		{
			if (expression == null) throw new ArgumentNullException(nameof(expression));

			var preParseScript = CSharpScript.Create(expression, options: this.ScriptOptions, globalsType: typeof(C));

			var compilation = preParseScript.GetCompilation();

			var diagnostics = ConvertDiagnostics(compilation.GetParseDiagnostics());

			EnsureNamespaceUsage(preParseScript);

			return diagnostics;
		}

		#endregion

		#region Protected methods

		/// <summary>
		/// Convert the diagnostic errors.
		/// </summary>
		protected ImmutableArray<FormulaDiagnostic> ConvertDiagnostics(ImmutableArray<Diagnostic> diagnostics)
			=> diagnostics.Select(d => new FormulaDiagnostic(ConvertFormulaDiagnosticSeverity(d.Severity), d.ToString())).ToImmutableArray();

		/// <summary>
		/// Ensure that a script uses only the allowed names.
		/// </summary>
		/// <param name="script">The script to test.</param>
		/// <exception cref="FormulaNameAccessException">Thrown when when the script contains an excluded name.</exception>
		protected virtual void EnsureNamespaceUsage(Script script)
		{
			var syntaxTree = script.GetCompilation().SyntaxTrees.FirstOrDefault();

			if (syntaxTree == null) return;

			var nameNodes = from node in syntaxTree.GetRoot().DescendantNodesAndSelf()
											where node.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.SimpleMemberAccessExpression) || node.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.IdentifierName)
											select node;

			foreach (var nameNode in nameNodes)
			{
				string name = nameNode.ToString();

				if (this.ExcludedNames.Contains(name))
				{
					throw new FormulaNameAccessException(String.Format(FormulaParserResources.NAME_ACCESS_DENIED, name), name);
				}
			}
		}

		#endregion

		#region Private methods

		private FormulaDiagnosticSeverity ConvertFormulaDiagnosticSeverity(DiagnosticSeverity severity)
			=> severity switch
			{
				DiagnosticSeverity.Hidden => FormulaDiagnosticSeverity.Hidden,
				DiagnosticSeverity.Info => FormulaDiagnosticSeverity.Info,
				DiagnosticSeverity.Warning => FormulaDiagnosticSeverity.Warning,
				DiagnosticSeverity.Error => FormulaDiagnosticSeverity.Error,
				_ => throw new FormulaEvaluationException(String.Format(FormulaParserResources.UNKNOWN_SEVERITY_TYPE, severity))
			};

		#endregion
	}
}
