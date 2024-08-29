using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Scripting;

namespace Grammophone.Formulae.Evaluation
{
	/// <summary>
	/// The result of an evaluation.
	/// </summary>
	[DebuggerDisplay("{GetDebuggerDisplay(), nq}")]
	public class EvaluationState
	{
		#region Construction

		internal EvaluationState(string identifier, ImmutableArray<EvaluationVariable> variables, ImmutableArray<FormulaDiagnostic> diagnostics)
		{
			this.Identifier = identifier;
			this.Variables = variables;
			this.Diagnostics = diagnostics;
			this.VariablesByName = variables.ToImmutableDictionary(ev => ev.Name);
			this.ReturnValue = this.VariablesByName[identifier].Value;
		}

		#endregion

		#region Public properties

		/// <summary>
		/// The identifier for which the formulae were computed.
		/// </summary>
		public string Identifier { get; }

		/// <summary>
		/// The variables computed in the evaluation.
		/// </summary>
		public IReadOnlyList<EvaluationVariable> Variables { get; }

		/// <summary>
		/// The variables computed in the evaluation, indexed by their name.
		/// </summary>
		public IReadOnlyDictionary<string, EvaluationVariable> VariablesByName { get; }

		/// <summary>
		/// The return value produced by the evaulation.
		/// </summary>
		public object? ReturnValue { get; }

		/// <summary>
		/// The diagnostics of the formula compilation.
		/// </summary>
		public IReadOnlyList<FormulaDiagnostic> Diagnostics { get; }

		#endregion

		#region Private methods

		/// <summary>
		/// Used for debugger display.
		/// </summary>
		private string GetDebuggerDisplay() => $"{this.Identifier}: {this.ReturnValue ?? "<null>"}";

		#endregion
	}
}
