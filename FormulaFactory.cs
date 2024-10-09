using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Grammophone.Caching;

namespace Grammophone.Formulae.Evaluation
{
	/// <summary>
	/// Factory for creating <see cref="FormulaEvaluator{C}"/> instances.
	/// </summary>
	/// <typeparam name="C">The type of the context class.</typeparam>
	public class FormulaFactory<C>
		where C : class
	{
		#region Auxilliary classes

		private class FormulaDefinitionsKey : IEquatable<FormulaDefinitionsKey>
		{
			public FormulaDefinitionsKey(IEnumerable<IFormulaDefinition> formulaDefinitions)
			{
				this.FormulaDefinitions = formulaDefinitions;

				this.CompositeKey = GetFormulaDefinitionsCompositeKey(formulaDefinitions);
			}

			public IEnumerable<IFormulaDefinition> FormulaDefinitions { get; }

			internal string CompositeKey { get; }

			private static string GetFormulaDefinitionsCompositeKey(IEnumerable<IFormulaDefinition> formulaDefinitions)
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

			public bool Equals(FormulaFactory<C>.FormulaDefinitionsKey? other) => this.CompositeKey == other?.CompositeKey;

			public override int GetHashCode() => this.CompositeKey.GetHashCode();

			public override bool Equals(object obj) => this.Equals(obj as FormulaDefinitionsKey);
		}

		#endregion

		#region Private fields

		private static readonly IReadOnlyCollection<Assembly> defaultAssemblies;

		private static readonly IReadOnlyCollection<string> defaultImports;

		private static readonly IReadOnlyCollection<string> defaultExcludedNames;

		private readonly MRUCache<FormulaDefinitionsKey, FormulaEvaluator<C>> evaluatorsCache;

		private readonly Lazy<FormulaParser<C>> lazyFormulaParser;

		#endregion

		#region Construction

		static FormulaFactory()
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
		/// <param name="roundingOptions">If not null, the rounding options to use for decimal variables, otherwise the decimal variables are assigned with no rounding.</param>
		public FormulaFactory(
			IEnumerable<Assembly>? assemblies = null,
			IEnumerable<string>? imports = null,
			IEnumerable<string>? excludedNames = null,
			RoundingOptions? roundingOptions = null)
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
				this.ExcludedNames = defaultExcludedNames;

			this.evaluatorsCache = new MRUCache<FormulaDefinitionsKey, FormulaEvaluator<C>>(k => CreateEvaluator(k.FormulaDefinitions));

			this.lazyFormulaParser = new Lazy<FormulaParser<C>>(
				() => new FormulaParser<C>(this.Assemblies, this.Imports, this.ExcludedNames), System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);
			
			this.RoundingOptions = roundingOptions;
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

		/// <summary>
		/// If not null, the rounding options to use for decimal variables, otherwise the decimal variables are assigned with no rounding.
		/// </summary>
		public RoundingOptions? RoundingOptions { get; }

		#endregion

		#region Public methods

		/// <summary>
		/// Get or create a <see cref="FormulaEvaluator{C}"/> from the cache based on the settings of this builder and the given formulae.
		/// </summary>
		/// <param name="formulaDefinitions">The formula definitions to be used for evaluation.</param>
		public FormulaEvaluator<C> GetEvaluator(IEnumerable<IFormulaDefinition> formulaDefinitions)
		{
			if (formulaDefinitions == null) throw new ArgumentNullException(nameof(formulaDefinitions));

			var formulaDefinitionsKey = new FormulaDefinitionsKey(formulaDefinitions);

			return evaluatorsCache.Get(formulaDefinitionsKey);
		}

		/// <summary>
		/// Flush the evaluators cache. Call if you change the expression of an existing formula definition.
		/// </summary>
		public void FlushEvaluatorsCache() => evaluatorsCache.Clear();

		/// <summary>
		/// Get a formula parser.
		/// </summary>
		public FormulaParser<C> GetParser() => lazyFormulaParser.Value;

		#endregion

		#region Protected methods

		/// <summary>
		/// Create a <see cref="FormulaEvaluator{C}"/> based on the settings of this builder and the given formulae.
		/// </summary>
		/// <param name="formulaDefinitions">The formula definitions to be used for evaluation.</param>
		protected virtual FormulaEvaluator<C> CreateEvaluator(IEnumerable<IFormulaDefinition> formulaDefinitions)
			=> new(formulaDefinitions, this.Assemblies, this.Imports, this.ExcludedNames, this.RoundingOptions);

		#endregion
	}
}
