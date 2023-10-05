using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Grammophone.Formulae.Evaluation
{
	/// <summary>
	/// Factory for creating <see cref="FormulaEvaluator{C}"/> instances.
	/// </summary>
	/// <typeparam name="C">The type of the context class.</typeparam>
	public class FormulaEvaluatorFactory<C>
		where C : class
	{
		#region Private fields

		private static readonly IReadOnlyCollection<Assembly> defaultAssemblies;

		private static readonly IReadOnlyCollection<string> defaultImports;

		private static readonly IReadOnlyCollection<string> defaultExcludedNames;

		#endregion

		#region Construction

		static FormulaEvaluatorFactory()
		{
			defaultAssemblies = new Assembly[] { };
			defaultImports = new string[] { "System", "System.Math" };
			defaultExcludedNames = new string[] { };
		}

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="assemblies">Optional collectoin of additional assembles to be referenced by the evaluators to be created.</param>
		/// <param name="imports">Optional namespace imports. If not specified, it defaults to "System" and "System.Math".</param>
		/// <param name="excludedNames">Optional Namespaces or member names to be blocked from usage.</param>
		public FormulaEvaluatorFactory(IEnumerable<Assembly>? assemblies = null, IEnumerable<string>? imports = null, IEnumerable<string>? excludedNames = null)
		{
			if (assemblies != null)
				this.Assemblies = assemblies.ToImmutableArray();
			else
				this.Assemblies = defaultAssemblies;

			if (imports != null)
				this.Imports = imports.ToImmutableArray();
			else
				this.Imports = defaultImports;

			if (excludedNames != null)
				this.ExcludedNames = excludedNames.ToImmutableArray();
			else
				this.ExcludedNames =defaultExcludedNames;
		}

		#endregion

		#region Public properties

		/// <summary>
		/// Additional assembles to be referenced by the evaluators to be created.
		/// </summary>
		public IReadOnlyCollection<Assembly> Assemblies { get; }

		/// <summary>
		/// Namespace imports to be used.
		/// </summary>
		public IReadOnlyCollection<string> Imports { get; }

		/// <summary>
		/// Namespaces or member names to be blocked from usage.
		/// </summary>
		public IReadOnlyCollection<string> ExcludedNames { get; }

		#endregion

		#region Public methods

		/// <summary>
		/// Create a <see cref="FormulaEvaluator{C}"/> based on the settings of this builder.
		/// </summary>
		/// <param name="formulaDefinitions">The formula definitions to be used for evaluation.</param>
		public virtual FormulaEvaluator<C> CreateEvaluator(IEnumerable<IFormulaDefinition> formulaDefinitions)
			=> new(formulaDefinitions, this.Assemblies, this.Imports, this.ExcludedNames);

		#endregion

		#region Private methods

		private string GetFormulaDefinitionsCompositeKey(IEnumerable<IFormulaDefinition> formulaDefinitions)
		{
			var compositeKeyBuilder = new StringBuilder();

			var keys = from formulaDefinition in formulaDefinitions
								 let key = formulaDefinition.GetFormulaID()
								 orderby key ascending
								 select key;

			foreach (var key in keys)
			{
				compositeKeyBuilder.Append(key);
				compositeKeyBuilder.Append('|');
			}

			return compositeKeyBuilder.ToString();
		}

		#endregion
	}
}
