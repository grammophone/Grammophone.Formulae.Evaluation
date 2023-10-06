using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Grammophone.Formulae.Evaluation
{
	/// <summary>
	/// A variable computed in an evaluation.
	/// </summary>
	[DebuggerDisplay("{GetDebuggerDisplay(), nq}")]
	public class EvaluationVariable
	{
		#region Construction

		internal EvaluationVariable(string name, Type type, bool isReadOnly, object? value, string? formulaExpression)
		{
			if (name is null)
			{
				throw new ArgumentNullException(nameof(name));
			}

			if (type is null)
			{
				throw new ArgumentNullException(nameof(type));
			}

			this.Name = name;
			this.Type = type;
			this.IsReadOnly = isReadOnly;
			this.Value = value;
			this.FormulaExpression = formulaExpression;
		}

		#endregion

		#region Public properties

		/// <summary>
		/// The name of the variable.
		/// </summary>
		public string Name { get; }

		/// <summary>
		/// The type of the variable.
		/// </summary>
		public Type Type { get; }

		/// <summary>
		/// True if the variable can't be written to (it's declared as readonly or a constant).
		/// </summary>
		public bool IsReadOnly { get; }

		/// <summary>
		/// The value of the variable after the evaluation.
		/// </summary>
		public object? Value { get; }

		/// <summary>
		/// Formula expression associated with the variable, if any, else null.
		/// </summary>
		public string? FormulaExpression { get; }

		#endregion

		#region Private methods

		/// <summary>
		/// Used for debugger display.
		/// </summary>
		private string GetDebuggerDisplay() => $"{this.Name}: {this.Value ?? "<null>"}";

		#endregion
	}
}
