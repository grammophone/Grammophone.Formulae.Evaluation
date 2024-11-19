using System;
using System.Collections.Generic;
using System.Text;

namespace Grammophone.Formulae.Evaluation
{
	/// <summary>
	/// An identifier in a script.
	/// </summary>
	public class Identifier
	{
		#region Construction

		internal Identifier(string name, IFormulaDefinition? formulaDefinition)
		{
			if (name == null) throw new ArgumentNullException(nameof(name));

			this.Name = name;
			this.FormulaDefinition = formulaDefinition;
		}

		#endregion

		#region Public property

		/// <summary>
		/// The name of the identifier.
		/// </summary>
		public string Name { get; }

		/// <summary>
		/// Optionally, the formula definition associated with the identifier, or null of not associated with the identifier.
		/// </summary>
		public IFormulaDefinition? FormulaDefinition { get; }

		#endregion
	}
}
