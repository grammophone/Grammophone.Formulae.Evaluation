using System;
using System.Collections.Generic;
using System.Text;

namespace Grammophone.Formulae.Evaluation
{
	/// <summary>
	/// A diagnostic produced during the comilation of formulae by a <see cref="FormulaEvaluator{C}"/>.
	/// </summary>
	[Serializable]
	public class FormulaDiagnostic
	{
		#region Construction

		internal FormulaDiagnostic(FormulaDiagnosticSeverity severity, string message)
		{
			this.Severity = severity;
			this.Message = message;
		}

		#endregion

		/// <summary>
		/// The severity of the diagnostic.
		/// </summary>
		public FormulaDiagnosticSeverity Severity { get; }

		/// <summary>
		/// The diagnostic message.
		/// </summary>
		public string Message { get; }
	}
}
