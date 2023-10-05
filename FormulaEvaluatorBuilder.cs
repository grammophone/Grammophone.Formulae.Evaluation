﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Grammophone.Formulae.Evaluation
{
	/// <summary>
	/// Builder for creating <see cref="FormulaEvaluator{C}"/> instances.
	/// </summary>
	public class FormulaEvaluatorBuilder
	{
		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="assemblies">Optional collectoin of additional assembles to be referenced by the evaluators to be created.</param>
		/// <param name="imports">Optional namespace imports. If not specified, it defaults to "System" and "System.Math".</param>
		/// <param name="excludedNames">Optional Namespaces or member names to be blocked from usage.</param>
		public FormulaEvaluatorBuilder(IEnumerable<Assembly>? assemblies = null, IEnumerable<string>? imports = null, IEnumerable<string>? excludedNames = null)
		{
			if (assemblies != null)
				this.Assemblies = assemblies.ToList();
			else
				this.Assemblies = new List<Assembly>();

			if (imports != null)
				this.Imports = imports.ToList();
			else
				this.Imports = new List<string> { "System", "System.Math" };

			if (excludedNames != null)
				this.ExcludedNames = excludedNames.ToList();
			else
				this.ExcludedNames = new List<string>();
		}

		#endregion

		#region Public properties

		/// <summary>
		/// Additional assembles to be referenced by the evaluators to be created.
		/// </summary>
		public ICollection<Assembly> Assemblies { get; }

		/// <summary>
		/// Namespace imports to be used.
		/// </summary>
		public ICollection<string> Imports { get; }

		/// <summary>
		/// Namespaces or member names to be blocked from usage.
		/// </summary>
		public ICollection<string> ExcludedNames { get; }

		#endregion

		#region Public methods

		/// <summary>
		/// Create a <see cref="FormulaEvaluator{C}"/> based on the settings of this builder.
		/// </summary>
		/// <typeparam name="C">The type of the context class.</typeparam>
		/// <param name="formulaDefinitions">The formula definitions to be used for evaluation.</param>
		public virtual FormulaEvaluator<C> CreateEvaluator<C>(IEnumerable<IFormulaDefinition> formulaDefinitions) where C : class
			=> new FormulaEvaluator<C>(formulaDefinitions, this.Assemblies.ToImmutableArray(), this.Imports.ToImmutableArray(), this.ExcludedNames.ToImmutableArray());

		#endregion
	}
}
