using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
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
	}
}
